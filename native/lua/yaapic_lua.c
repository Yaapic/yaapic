/*
 * Yaapic Lua shim: non-raising entry points over Lua 5.4 for the .NET bindings.
 *
 * Lua reports errors with longjmp, and a longjmp across a managed frame is undefined behaviour (it crashes under Native AOT).
 * Every function below therefore runs its Lua work inside lua_pcall and returns a status; the only managed code called back
 * is the host callback, and it has returned before the shim raises anything (luaL_error runs from C only).
 */
#define LUA_LIB
#define _POSIX_C_SOURCE 199309L

#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
#else
#include <time.h>
#endif

#include "lua.h"
#include "lauxlib.h"
#include "lualib.h"

typedef int (*yl_host_cb)(const char *op, const char *args, char **result, int *result_len);

typedef struct YL {
    size_t used;
    size_t limit; /* 0 = unlimited */
    int oom;      /* the allocator refused a request */
    int abort_kind; /* 0 none, 1 timeout, 2 canceled */
    int has_limits;
    volatile long long *deadline; /* milliseconds of yl_now_ms, 0 = none */
    volatile int *cancel;
    yl_host_cb host;
} YL;

LUA_API void yl_free(void *p) { free(p); }

LUA_API void *yl_alloc(size_t size) { return malloc(size ? size : 1); }

/* Monotonic milliseconds; the managed side builds deadlines from this same clock. */
LUA_API long long yl_now_ms(void) {
#ifdef _WIN32
    LARGE_INTEGER f, c;
    QueryPerformanceFrequency(&f);
    QueryPerformanceCounter(&c);
    return c.QuadPart * 1000 / f.QuadPart;
#else
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (long long)ts.tv_sec * 1000 + ts.tv_nsec / 1000000;
#endif
}

static void *limited_alloc(void *ud, void *ptr, size_t osize, size_t nsize) {
    YL *y = (YL *)ud;
    if (ptr == NULL) osize = 0; /* osize is a type tag for new blocks */
    if (nsize == 0) {
        free(ptr);
        y->used -= osize;
        return NULL;
    }
    if (y->limit != 0 && nsize > osize && y->used - osize + nsize > y->limit) {
        y->oom = 1;
        return NULL; /* Lua raises its own out-of-memory error */
    }
    void *p = realloc(ptr, nsize);
    if (p == NULL) {
        if (nsize <= osize) return ptr; /* shrinking must not fail */
        y->oom = 1;
        return NULL;
    }
    y->used = y->used - osize + nsize;
    return p;
}

static YL *state_of(lua_State *L) {
    void *ud;
    lua_getallocf(L, &ud);
    return (YL *)ud;
}

LUA_API lua_State *yl_new(long long memory_limit) {
    YL *y = (YL *)calloc(1, sizeof(YL));
    if (y == NULL) return NULL;
    y->limit = memory_limit > 0 ? (size_t)memory_limit : 0;
    lua_State *L = lua_newstate(limited_alloc, y);
    if (L == NULL) free(y);
    return L;
}

LUA_API void yl_close(lua_State *L) {
    if (L == NULL) return;
    YL *y = state_of(L);
    lua_close(L); /* frees through the allocator, which still needs y */
    free(y);
}

static int collect_count_only(lua_State *L) {
    const char *opt = luaL_optstring(L, 1, "collect");
    if (strcmp(opt, "count") != 0) return luaL_error(L, "collectgarbage only supports \"count\"");
    lua_pushnumber(L, (lua_Number)lua_gc(L, LUA_GCCOUNT) + (lua_Number)lua_gc(L, LUA_GCCOUNTB) / 1024.0);
    return 1;
}

static int open_libs_body(lua_State *L) {
    static const luaL_Reg libs[] = {
        {LUA_GNAME, luaopen_base},     {LUA_STRLIBNAME, luaopen_string}, {LUA_TABLIBNAME, luaopen_table},
        {LUA_MATHLIBNAME, luaopen_math}, {LUA_UTF8LIBNAME, luaopen_utf8},  {LUA_COLIBNAME, luaopen_coroutine},
        {NULL, NULL}};
    static const char *const removed[] = {"dofile", "loadfile", "load", "loadstring", "require", "print", "warn", NULL};
    for (const luaL_Reg *lib = libs; lib->func != NULL; lib++) {
        luaL_requiref(L, lib->name, lib->func, 1);
        lua_pop(L, 1);
    }
    for (int i = 0; removed[i] != NULL; i++) {
        lua_pushnil(L);
        lua_setglobal(L, removed[i]);
    }
    lua_pushcfunction(L, collect_count_only);
    lua_setglobal(L, "collectgarbage");
    lua_getglobal(L, LUA_STRLIBNAME);
    lua_pushnil(L);
    lua_setfield(L, -2, "dump");
    return 0;
}

/* base, string, table, math, utf8 and coroutine only; no io, os, package, debug, no loaders, no print. */
LUA_API int yl_open_libs(lua_State *L) {
    lua_pushcfunction(L, open_libs_body);
    int st = lua_pcall(L, 0, 0, 0);
    lua_settop(L, 0);
    return st;
}

static int host_fn(lua_State *L) {
    const char *op = luaL_checkstring(L, 1);
    const char *args = luaL_optstring(L, 2, "");
    YL *y = state_of(L);
    char *res = NULL;
    int len = 0;
    int rc = y->host(op, args, &res, &len);
    if (res != NULL) {
        lua_pushlstring(L, res, (size_t)len);
        free(res);
    } else {
        lua_pushstring(L, rc != 0 ? "host call failed" : "");
    }
    if (rc != 0) return luaL_error(L, "%s", lua_tostring(L, -1));
    return 1;
}

/* Registers the global __host(op, argsJson). */
LUA_API int yl_set_host(lua_State *L, yl_host_cb cb) {
    state_of(L)->host = cb;
    lua_pushcfunction(L, host_fn);
    lua_setglobal(L, "__host");
    return 0;
}

static void abort_hook(lua_State *L, lua_Debug *ar) {
    YL *y = state_of(L);
    (void)ar;
    if (y->abort_kind == 0) {
        if (y->cancel != NULL && *y->cancel != 0) {
            y->abort_kind = 2;
        } else if (y->deadline != NULL && *y->deadline != 0 && yl_now_ms() > *y->deadline) {
            y->abort_kind = 1;
        }
        /* From now on fire at every instruction so a script that catches the error with pcall cannot go on. */
        if (y->abort_kind != 0) lua_sethook(L, abort_hook, LUA_MASKCOUNT, 1);
    }
    if (y->abort_kind != 0) luaL_error(L, y->abort_kind == 1 ? "script timed out" : "script canceled");
}

/* Installs the count hook (every 1000 instructions) reading a deadline (yl_now_ms units, 0 = none) and a cancel flag. */
LUA_API int yl_set_limits(lua_State *L, volatile long long *deadline, volatile int *cancel) {
    YL *y = state_of(L);
    y->deadline = deadline;
    y->cancel = cancel;
    y->has_limits = 1;
    lua_sethook(L, abort_hook, LUA_MASKCOUNT, 1000);
    return 0;
}

/* 0 none, 1 timeout, 2 canceled. */
LUA_API int yl_last_abort(lua_State *L) { return state_of(L)->abort_kind; }

typedef struct RunArgs {
    const char *chunk;
    size_t len;
    const char *name;
    int status;
    char *msg;
} RunArgs;

static int msg_handler(lua_State *L) {
    const char *m = lua_tostring(L, 1);
    if (m == NULL) {
        if (luaL_callmeta(L, 1, "__tostring") && lua_type(L, -1) == LUA_TSTRING) return 1;
        m = lua_pushfstring(L, "(error object is a %s value)", luaL_typename(L, 1));
    }
    luaL_traceback(L, L, m, 1);
    return 1;
}

static int run_body(lua_State *L) {
    RunArgs *a = (RunArgs *)lua_touserdata(L, 1);
    lua_pushcfunction(L, msg_handler);
    int handler = lua_gettop(L);
    int st = luaL_loadbufferx(L, a->chunk, a->len, a->name, "t"); /* text chunks only */
    if (st == LUA_OK) st = lua_pcall(L, 0, 0, handler);
    a->status = st;
    if (st != LUA_OK) {
        size_t n;
        const char *m = lua_tolstring(L, -1, &n);
        if (m == NULL) {
            m = "(error object is not a string)";
            n = strlen(m);
        }
        a->msg = (char *)malloc(n + 1);
        if (a->msg != NULL) {
            memcpy(a->msg, m, n);
            a->msg[n] = '\0';
        }
    }
    return 0;
}

/*
 * Runs a text chunk. Returns 0 ok, 1 runtime error, 2 syntax error, 3 out of memory, 4 timeout, 5 canceled.
 * On failure *err_msg holds the message and traceback (free it with yl_free) and *err_line the 1-based line, 0 if unknown.
 */
LUA_API int yl_run(lua_State *L, const char *chunk, size_t len, const char *chunk_name, char **err_msg, int *err_line) {
    YL *y = state_of(L);
    *err_msg = NULL;
    *err_line = 0;
    y->abort_kind = 0;
    y->oom = 0;
    if (y->has_limits) lua_sethook(L, abort_hook, LUA_MASKCOUNT, 1000);

    size_t nlen = strlen(chunk_name);
    char *name = (char *)malloc(nlen + 2);
    if (name == NULL) return 3;
    name[0] = '=';
    memcpy(name + 1, chunk_name, nlen + 1);

    RunArgs a = {chunk, len, name, -1, NULL};
    lua_pushcfunction(L, run_body);
    lua_pushlightuserdata(L, &a);
    int outer = lua_pcall(L, 1, 0, 0);
    lua_settop(L, 0);
    int st = a.status >= 0 ? a.status : outer;

    int result;
    if (y->abort_kind == 1) result = 4;
    else if (y->abort_kind == 2) result = 5;
    else if (st == LUA_OK) result = 0;
    else if (st == LUA_ERRMEM || y->oom) result = 3;
    else if (st == LUA_ERRSYNTAX) result = 2;
    else result = 1;

    if (a.msg != NULL) {
        if (result != 0 && strncmp(a.msg, chunk_name, nlen) == 0 && a.msg[nlen] == ':') *err_line = atoi(a.msg + nlen + 1);
        if (result != 0) *err_msg = a.msg; else free(a.msg);
    }
    free(name);
    return result;
}

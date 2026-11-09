cmake_minimum_required(VERSION 3.20)
project(lua54 C)

# Usage: cmake -S native/lua -B <build> -DLUA_SRC_DIR=<extracted lua-5.4.x/src>
# Compiles every core and library source except the `lua` and `luac` executables. LUA_COMPAT_5_3 is not defined.
set(LUA_SRC_DIR "" CACHE PATH "Directory holding the Lua 5.4 sources (the src folder of the tarball)")
if(NOT EXISTS "${LUA_SRC_DIR}/lua.h")
    message(FATAL_ERROR "LUA_SRC_DIR must point to the Lua src folder (lua.h not found in '${LUA_SRC_DIR}').")
endif()

file(GLOB LUA_SOURCES CONFIGURE_DEPENDS "${LUA_SRC_DIR}/*.c")
list(FILTER LUA_SOURCES EXCLUDE REGEX "/(lua|luac)\\.c$")

add_library(lua54 SHARED ${LUA_SOURCES})
target_include_directories(lua54 PUBLIC "${LUA_SRC_DIR}")
set_target_properties(lua54 PROPERTIES OUTPUT_NAME lua54 C_STANDARD 99)

if(WIN32)
    target_compile_definitions(lua54 PRIVATE LUA_BUILD_AS_DLL)
elseif(APPLE)
    target_compile_definitions(lua54 PRIVATE LUA_USE_MACOSX)
else()
    target_compile_definitions(lua54 PRIVATE LUA_USE_LINUX)
    target_link_libraries(lua54 PRIVATE m ${CMAKE_DL_LIBS})
endif()

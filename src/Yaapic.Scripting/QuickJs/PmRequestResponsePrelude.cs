namespace Yaapic.Scripting.QuickJs;

/// <summary>The JavaScript <c>pm</c> prelude, part 2: <c>pm.request</c> (read and, in pre-request, write) and <c>pm.response</c> (post-response).</summary>
internal static class PmRequestResponsePrelude
{
    /// <summary>The prelude source, evaluated as its own script after <see cref="PmVariablesPrelude.Source"/>.</summary>
    internal const string Source = """
        (() => {
          'use strict';
          const host = globalThis.__host;
          const pm = globalThis.pm;
          const info = host('info.get');

          const define = (target, name, value) => Object.defineProperty(target, name, { value, enumerable: true });
          const accessor = (target, name, get, set) => Object.defineProperty(target, name, { get, set, enumerable: true });

          const unsupported = (path) => {
            const fail = (member) => {
              throw new Error(path + (member === undefined ? '' : '.' + String(member)) + ' is not supported in Yaapic');
            };
            return new Proxy(function () {}, {
              get: (target, member) => fail(member),
              set: (target, member) => fail(member),
              has: (target, member) => fail(member),
              apply: () => fail(),
              construct: () => fail(),
            });
          };

          const toItem = (row) => (row.enabled === false ? { key: row.key, value: row.value, disabled: true } : { key: row.key, value: row.value });
          const toRow = (item) => ({ key: item.key, value: item.value, enabled: item.disabled !== true });
          const checkItem = (label, item) => {
            if (typeof item !== 'object' || item === null || typeof item.key !== 'string') {
              throw new TypeError(label + ': item must be an object with a string key');
            }
            const value = item.value === undefined || item.value === null ? '' : String(item.value);
            return item.disabled === true ? { key: item.key, value, disabled: true } : { key: item.key, value };
          };

          // Every call reads the host again (no cached copy); a list without a store is read-only.
          class PropertyList {
            #label;
            #load;
            #store;
            #fold;
            constructor(label, load, store, fold) {
              this.#label = label;
              this.#load = load;
              this.#store = store;
              this.#fold = fold;
            }
            #same(a, b) {
              return this.#fold ? a.toLowerCase() === b.toLowerCase() : a === b;
            }
            #find(items, key) {
              return items.findIndex((item) => !item.disabled && this.#same(item.key, key));
            }
            #write(items) {
              if (this.#store === null) {
                throw new Error(this.#label + ' is read-only');
              }
              this.#store(items);
            }
            get(key) {
              const items = this.#load();
              const index = this.#find(items, key);
              return index < 0 ? undefined : items[index].value;
            }
            has(key) {
              return this.#find(this.#load(), key) >= 0;
            }
            add(item) {
              const items = this.#load();
              items.push(checkItem(this.#label + '.add', item));
              this.#write(items);
            }
            upsert(item) {
              const items = this.#load();
              const next = checkItem(this.#label + '.upsert', item);
              const index = this.#find(items, next.key);
              if (index < 0) {
                items.push(next);
              } else {
                items[index] = next;
              }
              this.#write(items);
            }
            remove(key) {
              this.#write(this.#load().filter((item) => !this.#same(item.key, key)));
            }
            each(fn) {
              this.#load().forEach((item) => fn(item));
            }
            count() {
              return this.#load().length;
            }
            all() {
              return this.#load();
            }
            idx(index) {
              return this.#load()[index];
            }
            toObject() {
              const result = {};
              for (const item of this.#load()) {
                if (!item.disabled && !(item.key in result)) {
                  result[item.key] = item.value;
                }
              }
              return result;
            }
          }

          const mutate = (change) => {
            const dto = host('req.get');
            change(dto);
            host('req.set', dto);
          };

          // ---- URL: a small splitter, variables stay untouched (split on the first '#', '?' and '://').
          const decode = (text) => {
            try {
              return decodeURIComponent(text);
            } catch (e) {
              return text;
            }
          };
          // Escapes like the application does: {{name}} tokens stay readable.
          const escape = (text) => text.split(/(\{\{[^{}]*\}\})/).map((part, i) => (i % 2 === 1 ? part : encodeURIComponent(part))).join('');
          const readQuery = (raw) => raw.split('&').filter((segment) => segment !== '').map((segment) => {
            const eq = segment.indexOf('=');
            return eq < 0 ? { key: decode(segment), value: '' } : { key: decode(segment.slice(0, eq)), value: decode(segment.slice(eq + 1)) };
          });
          const writeQuery = (items) => items
            .filter((item) => !item.disabled && item.key !== '')
            .map((item) => escape(item.key) + '=' + escape(item.value))
            .join('&');

          const parse = (text) => {
            let rest = text;
            let hash = '';
            let query = '';
            const hashAt = rest.indexOf('#');
            if (hashAt >= 0) {
              hash = rest.slice(hashAt + 1);
              rest = rest.slice(0, hashAt);
            }
            const queryAt = rest.indexOf('?');
            if (queryAt >= 0) {
              query = rest.slice(queryAt + 1);
              rest = rest.slice(0, queryAt);
            }
            let protocol = '';
            const schemeAt = rest.indexOf('://');
            if (schemeAt >= 0) {
              protocol = rest.slice(0, schemeAt);
              rest = rest.slice(schemeAt + 3);
            }
            const slash = rest.indexOf('/');
            const path = slash < 0 ? '' : rest.slice(slash);
            let authority = slash < 0 ? rest : rest.slice(0, slash);
            const atAt = authority.lastIndexOf('@');
            const auth = atAt < 0 ? '' : authority.slice(0, atAt);
            authority = authority.slice(atAt + 1);
            const portMatch = /:(\d+)$/.exec(authority);
            const port = portMatch === null ? '' : portMatch[1];
            return { protocol, auth, host: portMatch === null ? authority : authority.slice(0, portMatch.index), port, path, query, hash };
          };
          const build = (p) => (p.protocol === '' ? '' : p.protocol + '://')
            + (p.auth === '' ? '' : p.auth + '@')
            + p.host
            + (p.port === '' ? '' : ':' + p.port)
            + p.path
            + (p.query === '' ? '' : '?' + p.query)
            + (p.hash === '' ? '' : '#' + p.hash);

          class Url {
            #text;
            #flush;
            constructor(text, flush) {
              this.#text = text;
              this.#flush = flush;
            }
            #edit(change) {
              const parts = parse(this.#text);
              change(parts);
              this.#set(build(parts));
            }
            #set(text) {
              this.#flush(text);
              this.#text = text;
            }
            toString() {
              return this.#text;
            }
            update(text) {
              if (typeof text !== 'string') {
                throw new TypeError('pm.request.url.update: url must be a string');
              }
              this.#set(text);
            }
            getHost() {
              return parse(this.#text).host;
            }
            getPath() {
              const path = parse(this.#text).path;
              return path === '' ? '/' : path;
            }
            getQueryString() {
              return parse(this.#text).query;
            }
            getPathWithQuery() {
              const query = this.getQueryString();
              return this.getPath() + (query === '' ? '' : '?' + query);
            }
            get protocol() {
              return parse(this.#text).protocol;
            }
            set protocol(value) {
              this.#edit((p) => { p.protocol = String(value); });
            }
            get host() {
              const host = parse(this.#text).host;
              return host === '' ? [] : host.split('.');
            }
            set host(value) {
              this.#edit((p) => { p.host = Array.isArray(value) ? value.join('.') : String(value); });
            }
            get port() {
              return parse(this.#text).port;
            }
            set port(value) {
              this.#edit((p) => { p.port = String(value); });
            }
            get path() {
              return parse(this.#text).path.split('/').filter((segment) => segment !== '');
            }
            set path(value) {
              this.#edit((p) => {
                const joined = Array.isArray(value) ? value.join('/') : String(value);
                p.path = joined === '' ? '' : (joined.startsWith('/') ? joined : '/' + joined);
              });
            }
            get hash() {
              return parse(this.#text).hash;
            }
            set hash(value) {
              this.#edit((p) => { p.hash = String(value); });
            }
            get query() {
              return new PropertyList(
                'pm.request.url.query',
                () => readQuery(parse(this.#text).query),
                (items) => this.#edit((p) => { p.query = writeQuery(items); }),
                false);
            }
          }

          // ---- pm.request
          const headers = () => new PropertyList(
            'pm.request.headers',
            () => host('req.get').headers.map(toItem),
            (items) => mutate((dto) => { dto.headers = items.map(toRow); }),
            true);

          const settableModes = ['none', 'raw', 'urlencoded'];
          const body = {};
          accessor(
            body,
            'mode',
            () => host('req.get').bodyMode,
            (mode) => {
              if (mode === 'formdata' || mode === 'binary') {
                throw new Error("pm.request.body mode '" + mode + "' cannot be set from a script");
              }
              if (!settableModes.includes(mode)) {
                throw new Error("pm.request.body mode '" + String(mode) + "' is not valid");
              }
              mutate((dto) => { dto.bodyMode = mode; });
            });
          accessor(
            body,
            'raw',
            () => host('req.get').bodyText,
            (text) => {
              if (typeof text !== 'string') {
                throw new TypeError('pm.request.body.raw must be a string');
              }
              mutate((dto) => { dto.bodyText = text; });
            });
          accessor(
            body,
            'urlencoded',
            () => new PropertyList(
              'pm.request.body.urlencoded',
              () => host('req.get').urlEncoded.map(toItem),
              (items) => mutate((dto) => { dto.urlEncoded = items.map(toRow); }),
              false));
          accessor(
            body,
            'formdata',
            () => new PropertyList('pm.request.body.formdata', () => (host('req.get').formData ?? []).map(toItem), null, false));
          define(body, 'toString', () => host('req.get').bodyText);

          const request = {};
          define(request, 'id', info.requestId);
          define(request, 'name', info.requestName);
          accessor(
            request,
            'method',
            () => host('req.get').method,
            (method) => {
              if (typeof method !== 'string') {
                throw new TypeError('pm.request.method must be a string');
              }
              mutate((dto) => { dto.method = method.toUpperCase(); });
            });
          const flushUrl = (text) => mutate((dto) => { dto.url = text; });
          accessor(
            request,
            'url',
            () => new Url(host('req.get').url, flushUrl),
            (value) => {
              if (typeof value !== 'string' && !(value instanceof Url)) {
                throw new TypeError('pm.request.url must be a string or a Url');
              }
              flushUrl(String(value));
            });
          accessor(request, 'headers', headers);
          define(request, 'body', body);
          define(request, 'auth', unsupported('pm.request.auth'));
          define(request, 'certificate', unsupported('pm.request.certificate'));
          define(pm, 'request', request);

          // ---- pm.response (post-response only; the snapshot is immutable, so it is built once)
          let response;
          const buildResponse = (dto) => {
            const result = {};
            define(result, 'code', dto.code);
            define(result, 'status', dto.status);
            define(result, 'responseTime', dto.time);
            define(result, 'responseSize', dto.size);
            define(result, 'headers', new PropertyList('pm.response.headers', () => dto.headers.map(toItem), null, true));
            define(result, 'text', () => dto.text);
            define(result, 'json', (reviver) => {
              try {
                return JSON.parse(dto.text, reviver);
              } catch (e) {
                throw new Error('Response body is not valid JSON: ' + e.message);
              }
            });
            define(result, 'reason', () => dto.status);
            Object.defineProperty(result, 'to', { enumerable: true, get: () => globalThis.__responseTo(dto) });
            define(result, 'size', () => ({ body: dto.size, header: 0, total: dto.size }));
            Object.defineProperty(result, 'cookies', { enumerable: true, get: () => globalThis.__cookieList(dto.cookies ?? []) });
            return Object.freeze(result);
          };
          Object.defineProperty(globalThis, '__buildResponse', { value: buildResponse });
          Object.defineProperty(pm, 'response', {
            enumerable: true,
            get() {
              if (response === undefined) {
                const dto = host('res.get');
                if (dto === null) {
                  throw new Error('pm.response is only available in post-response scripts');
                }
                response = buildResponse(dto);
              }
              return response;
            },
          });
        })();
        """;
}

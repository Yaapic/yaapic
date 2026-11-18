namespace Yaapic.Scripting.QuickJs;

/// <summary>The JavaScript <c>yaapic</c> prelude, part 4: <c>yaapic.sendRequest</c> and <c>yaapic.cookies</c> (and the cookie list behind <c>res.cookies</c>).</summary>
internal static class PmSendPrelude
{
    /// <summary>The prelude source, evaluated as its own script after <see cref="PmTestPrelude.Source"/>.</summary>
    internal const string Source = """
        (() => {
          'use strict';
          const host = globalThis.__host;
          const yaapic = globalThis.yaapic;

          // Read-only cookie access over a list of host cookies (the current response, or a sendRequest response).
          class CookieList {
            #load;
            constructor(load) {
              this.#load = load;
            }
            #find(name) {
              return this.#load().find((cookie) => cookie.name === name);
            }
            get(name) {
              return this.#find(name)?.value;
            }
            has(name) {
              return this.#find(name) !== undefined;
            }
            toObject() {
              const result = {};
              for (const cookie of this.#load()) {
                if (!(cookie.name in result)) {
                  result[cookie.name] = cookie.value;
                }
              }
              return result;
            }
            each(fn) {
              this.all().forEach((cookie) => fn(cookie));
            }
            count() {
              return this.#load().length;
            }
            all() {
              return this.#load().map((c) => ({
                name: c.name,
                value: c.value,
                domain: c.domain ?? undefined,
                path: c.path ?? undefined,
                expires: c.expires ?? undefined,
                httpOnly: c.httpOnly === true,
                secure: c.secure === true,
              }));
            }
            jar() {
              throw new Error('The cookie jar is not available in Yaapic yet');
            }
          }
          Object.defineProperty(globalThis, '__cookieList', { value: (cookies) => new CookieList(() => cookies) });
          Object.defineProperty(yaapic, 'cookies', { value: new CookieList(() => host('cookies')), enumerable: true });

          const toRows = (headers) => {
            if (headers === undefined || headers === null) {
              return [];
            }
            if (Array.isArray(headers)) {
              return headers.map((h) => ({ key: String(h.key), value: String(h.value ?? ''), enabled: h.disabled !== true }));
            }
            return Object.keys(headers).map((key) => ({ key, value: String(headers[key]), enabled: true }));
          };
          const toPairs = (items) => (items ?? []).map((i) => ({ key: String(i.key), value: String(i.value ?? ''), enabled: i.disabled !== true }));

          const toDto = (input) => {
            const options = typeof input === 'string' ? { url: input } : input;
            if (typeof options !== 'object' || options === null || options.url === undefined || options.url === null) {
              throw new TypeError('yaapic.sendRequest: expected a URL or an options object with a url');
            }
            const dto = {
              method: String(options.method ?? 'GET').toUpperCase(),
              url: String(options.url),
              headers: toRows(options.header ?? options.headers),
              bodyMode: 'none',
              bodyText: '',
              bodyContentType: '',
              urlEncoded: [],
            };
            const body = options.body;
            if (body !== undefined && body !== null) {
              const mode = body.mode ?? 'raw';
              if (mode === 'raw') {
                dto.bodyMode = 'raw';
                dto.bodyText = String(body.raw ?? '');
                dto.bodyContentType = String(body.options?.raw?.language ?? '');
              } else if (mode === 'urlencoded') {
                dto.bodyMode = 'urlencoded';
                dto.urlEncoded = toPairs(body.urlencoded);
              } else {
                throw new Error("yaapic.sendRequest body mode '" + mode + "' is not supported");
              }
            }
            return dto;
          };

          Object.defineProperty(yaapic, 'sendRequest', {
            enumerable: true,
            value: (input, callback) => {
              if (callback !== undefined && typeof callback !== 'function') {
                throw new TypeError('yaapic.sendRequest: callback must be a function');
              }
              const dto = toDto(input);
              // The blocking host call happens now; only the delivery is asynchronous.
              let reply;
              try {
                reply = host('send', dto);
              } catch (e) {
                reply = { error: e.message };
              }
              const error = reply.response ? null : new Error(reply.error ?? 'Request failed');
              const response = reply.response ? globalThis.__buildResponse(reply.response) : undefined;
              if (callback === undefined) {
                return error === null ? Promise.resolve(response) : Promise.reject(error);
              }
              Promise.resolve().then(() => {
                try {
                  callback(error, response);
                } catch (e) {
                  globalThis.__yaapic.errors.push(e);
                }
              });
              return undefined;
            },
          });
        })();
        """;
}

namespace Yaapic.Scripting.QuickJs;

/// <summary>The JavaScript <c>pm</c> prelude, part 1: <c>pm.info</c>, the variable scopes, the legacy <c>postman</c> aliases and the unsupported stubs.</summary>
internal static class PmVariablesPrelude
{
    /// <summary>The prelude source, evaluated as its own script after <see cref="JsPrelude.Core"/>.</summary>
    internal const string Source = """
        (() => {
          'use strict';
          const host = globalThis.__host;

          const coerce = (value) => {
            switch (typeof value) {
              case 'string':
                return value;
              case 'number':
              case 'boolean':
              case 'bigint':
                return String(value);
              default:
                break;
            }
            if (value === null || value === undefined) {
              return '';
            }
            let json;
            try {
              json = JSON.stringify(value);
            } catch (e) {
              return '[object Object]';
            }
            return json === undefined ? String(value) : json;
          };

          const checkName = (label, method, name) => {
            if (typeof name !== 'string') {
              throw new TypeError(label + '.' + method + ': name must be a string');
            }
          };

          // One class for the four scopes: "scope" is read, "writeScope" is written and cleared ("local" for the layered view).
          class VariableScope {
            #label;
            #scope;
            #writeScope;
            constructor(label, scope, writeScope) {
              this.#label = label;
              this.#scope = scope;
              this.#writeScope = writeScope;
            }
            get(name) {
              checkName(this.#label, 'get', name);
              const value = host('var.get', { scope: this.#scope, name });
              return value === null ? undefined : value;
            }
            has(name) {
              checkName(this.#label, 'has', name);
              return host('var.has', { scope: this.#scope, name });
            }
            set(name, value) {
              checkName(this.#label, 'set', name);
              host('var.set', { scope: this.#writeScope, name, value: coerce(value) });
            }
            unset(name) {
              checkName(this.#label, 'unset', name);
              host('var.unset', { scope: this.#writeScope, name });
            }
            clear() {
              host('var.clear', { scope: this.#writeScope });
            }
            toObject() {
              return Object.fromEntries(host('var.list', { scope: this.#scope }).map((row) => [row.key, row.value]));
            }
            replaceIn(text) {
              if (this.#scope !== 'layered') {
                throw new TypeError(this.#label + '.replaceIn is not a function');
              }
              if (typeof text !== 'string') {
                throw new TypeError(this.#label + '.replaceIn: text must be a string');
              }
              return host('var.replace', { scope: 'layered', value: text });
            }
          }
          Object.freeze(VariableScope.prototype);

          // A stub throws on any use: calling it, reading, writing or probing a member.
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

          const scope = (label, name, write) => Object.freeze(new VariableScope(label, name, write));
          const info = host('info.get');
          const pm = {};
          const define = (target, name, value) =>
            Object.defineProperty(target, name, { value, enumerable: true });

          define(pm, 'info', Object.freeze({
            eventName: info.eventName,
            requestName: info.requestName,
            requestId: info.requestId,
            iteration: 0,
            iterationCount: 1,
          }));
          define(pm, 'variables', scope('pm.variables', 'layered', 'local'));
          const environment = scope('pm.environment', 'environment', 'environment');
          const collectionVariables = scope('pm.collectionVariables', 'collection', 'collection');
          const globals = scope('pm.globals', 'globals', 'globals');
          // pm.environment.name is not exposed in v1: wrap the frozen scope so that the member throws.
          define(pm, 'environment', new Proxy(environment, {
            get: (target, member, receiver) => {
              if (member === 'name') {
                throw new Error('pm.environment.name is not supported in Yaapic');
              }
              const value = Reflect.get(target, member, target);
              return typeof value === 'function' ? value.bind(target) : value;
            },
          }));
          define(pm, 'collectionVariables', collectionVariables);
          define(pm, 'globals', globals);
          for (const name of ['execution', 'iterationData', 'vault', 'visualizer', 'require']) {
            define(pm, name, unsupported('pm.' + name));
          }
          Object.defineProperty(globalThis, 'pm', { value: pm, writable: false, configurable: false });
          Object.defineProperty(globalThis, 'yaapic', { value: pm, writable: false, configurable: false });

          const postman = {};
          define(postman, 'setEnvironmentVariable', (key, value) => environment.set(key, value));
          define(postman, 'getEnvironmentVariable', (key) => environment.get(key));
          define(postman, 'clearEnvironmentVariable', (key) => environment.unset(key));
          define(postman, 'setGlobalVariable', (key, value) => globals.set(key, value));
          define(postman, 'getGlobalVariable', (key) => globals.get(key));
          define(postman, 'clearGlobalVariable', (key) => globals.unset(key));
          define(postman, 'setNextRequest', unsupported('postman.setNextRequest'));
          define(postman, 'visualizer', unsupported('postman.visualizer'));
          Object.defineProperty(globalThis, 'postman', { value: Object.freeze(postman), writable: false, configurable: false });
          // Node-style imports and the bundled libraries are not available: fail loudly instead of "x is not defined".
          for (const name of ['require', 'CryptoJS']) {
            Object.defineProperty(globalThis, name, { value: unsupported(name), writable: false, configurable: false });
          }
        })();
        """;
}

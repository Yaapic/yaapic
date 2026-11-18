namespace Yaapic.Scripting.QuickJs;

/// <summary>The JavaScript <c>yaapic</c> prelude, part 3: <c>yaapic.test</c>, <c>AssertionError</c> and the core Chai-style <c>yaapic.expect</c> assertions.</summary>
internal static class PmTestPrelude
{
    /// <summary>The prelude source, evaluated as its own script after <see cref="PmRequestResponsePrelude.Source"/>.</summary>
    internal const string Source = """
        (() => {
          'use strict';
          const host = globalThis.__host;
          const yaapic = globalThis.yaapic;
          const define = (target, name, value) => Object.defineProperty(target, name, { value, enumerable: true });

          // ---- inspect: Chai-like value rendering, depth 2, 80 characters at most ----
          const MAX_DEPTH = 2;
          const MAX_LENGTH = 80;
          const typeOf = (value) => Object.prototype.toString.call(value).slice(8, -1).toLowerCase();
          const key = (name) => (/^[A-Za-z_$][\w$]*$/.test(name) ? name : "'" + name + "'");
          const render = (value, depth, seen) => {
            switch (typeof value) {
              case 'string':
                return "'" + value.replace(/\\/g, '\\\\').replace(/'/g, "\\'") + "'";
              case 'number':
                return Object.is(value, -0) ? '-0' : String(value);
              case 'bigint':
                return value + 'n';
              case 'function':
                return value.name ? '[Function: ' + value.name + ']' : '[Function (anonymous)]';
              case 'object':
                break;
              default:
                return String(value);
            }
            if (value === null) {
              return 'null';
            }
            if (seen.includes(value)) {
              return '[Circular]';
            }
            const type = typeOf(value);
            if (type === 'date') {
              return Number.isNaN(value.getTime()) ? 'Invalid Date' : value.toISOString();
            }
            if (type === 'regexp') {
              return String(value);
            }
            if (value instanceof Error) {
              return '[' + value.name + (value.message ? ': ' + value.message : '') + ']';
            }
            const nested = [...seen, value];
            const wrap = (open, items, close, label) => {
              const text = items.length === 0 ? open + close : open + ' ' + items.join(', ') + ' ' + close;
              return label ? label + ' ' + text : text;
            };
            if (Array.isArray(value)) {
              return depth > MAX_DEPTH ? '[Array]' : wrap('[', value.map((v) => render(v, depth + 1, nested)), ']');
            }
            if (type === 'map') {
              return depth > MAX_DEPTH ? '[Map]' : wrap('{', [...value].map(([k, v]) => render(k, depth + 1, nested) + ' => ' + render(v, depth + 1, nested)), '}', 'Map(' + value.size + ')');
            }
            if (type === 'set') {
              return depth > MAX_DEPTH ? '[Set]' : wrap('{', [...value].map((v) => render(v, depth + 1, nested)), '}', 'Set(' + value.size + ')');
            }
            if (depth > MAX_DEPTH) {
              return '[Object]';
            }
            return wrap('{', Object.keys(value).map((k) => key(k) + ': ' + render(value[k], depth + 1, nested)), '}');
          };
          const inspect = (value) => {
            const text = render(value, 0, []);
            return text.length > MAX_LENGTH ? text.slice(0, MAX_LENGTH) + '...' : text;
          };

          // ---- deep equality (Chai's eql, simplified) ----
          const deepEqual = (a, b, seen = []) => {
            if (Object.is(a, b)) {
              return true;
            }
            if (typeof a !== 'object' || typeof b !== 'object' || a === null || b === null) {
              return a === b;
            }
            const type = typeOf(a);
            if (type !== typeOf(b)) {
              return false;
            }
            if (seen.some(([x, y]) => x === a && y === b)) {
              return true;
            }
            const next = [...seen, [a, b]];
            switch (type) {
              case 'date':
                return Object.is(a.getTime(), b.getTime());
              case 'regexp':
                return String(a) === String(b);
              case 'map':
                return a.size === b.size && [...a].every(([k, v]) => b.has(k) && deepEqual(v, b.get(k), next));
              case 'set':
                return a.size === b.size && [...a].every((v) => [...b].some((w) => deepEqual(v, w, next)));
              default:
                break;
            }
            if (Array.isArray(a)) {
              return a.length === b.length && a.every((v, i) => deepEqual(v, b[i], next));
            }
            if (Object.getPrototypeOf(a) !== Object.getPrototypeOf(b)) {
              return false;
            }
            const keys = Object.keys(a);
            return keys.length === Object.keys(b).length && keys.every((k) => Object.prototype.hasOwnProperty.call(b, k) && deepEqual(a[k], b[k], next));
          };

          // ---- AssertionError ----
          class AssertionError extends Error {
            constructor(message, props) {
              super(message);
              Object.defineProperty(this, 'name', { value: 'AssertionError', writable: true, configurable: true });
              this.actual = props?.actual;
              this.expected = props?.expected;
              this.showDiff = props?.showDiff === true;
            }
          }
          Object.defineProperty(globalThis, 'AssertionError', { value: AssertionError, writable: true, configurable: true });

          // ---- Assertion ----
          const flagsKey = Symbol('flags');
          const fill = (template, obj, exp, act) =>
            template.replace('#{this}', () => inspect(obj)).replace('#{exp}', () => inspect(exp)).replace('#{act}', () => inspect(act));
          const flagOf = (self) => self[flagsKey];
          const objectOf = (self) => self[flagsKey].object;

          // The one place a result is judged: "ok" is the un-negated outcome, "not" flips it and picks the negated text.
          const check = (self, ok, positive, negative, exp, act, showDiff) => {
            const flags = flagOf(self);
            if (flags.not ? ok : !ok) {
              const prefix = flags.message ? flags.message + ': ' : '';
              throw new AssertionError(prefix + fill(flags.not ? negative : positive, flags.object, exp, act), { actual: flags.object, expected: exp, showDiff: showDiff === true });
            }
          };

          class Assertion {
            constructor(object, message) {
              Object.defineProperty(this, flagsKey, { value: { object, message, not: false, deep: false, lengthOf: false } });
            }
          }
          const proto = Assertion.prototype;
          const getter = (names, get) => {
            for (const name of names) {
              Object.defineProperty(proto, name, { get, configurable: true });
            }
          };
          const method = (names, fn) => {
            for (const name of names) {
              Object.defineProperty(proto, name, { value: fn, configurable: true, writable: true });
            }
          };

          getter(['to', 'be', 'been', 'is', 'that', 'which', 'and', 'has', 'have', 'with', 'at', 'of', 'same', 'but', 'does', 'still', 'also'], function () {
            return this;
          });
          getter(['not'], function () {
            this[flagsKey].not = true;
            return this;
          });
          getter(['deep'], function () {
            this[flagsKey].deep = true;
            return this;
          });

          method(['equal', 'equals', 'eq'], function (value) {
            const flags = flagOf(this);
            if (flags.deep) {
              check(this, deepEqual(flags.object, value), 'expected #{this} to deeply equal #{exp}', 'expected #{this} to not deeply equal #{exp}', value, flags.object, true);
            } else {
              check(this, flags.object === value, 'expected #{this} to equal #{exp}', 'expected #{this} to not equal #{exp}', value, flags.object, true);
            }
            return this;
          });
          method(['eql', 'eqls'], function (value) {
            check(this, deepEqual(objectOf(this), value), 'expected #{this} to deeply equal #{exp}', 'expected #{this} to not deeply equal #{exp}', value, objectOf(this), true);
            return this;
          });

          const literal = (name, test, positive, negative) =>
            getter([name], function () {
              check(this, test(objectOf(this)), positive, negative);
              return this;
            });
          literal('ok', (v) => Boolean(v), 'expected #{this} to be truthy', 'expected #{this} to be falsy');
          literal('true', (v) => v === true, 'expected #{this} to be true', 'expected #{this} to be false');
          literal('false', (v) => v === false, 'expected #{this} to be false', 'expected #{this} to be true');
          literal('null', (v) => v === null, 'expected #{this} to be null', 'expected #{this} not to be null');
          literal('undefined', (v) => v === undefined, 'expected #{this} to be undefined', 'expected #{this} not to be undefined');
          literal('NaN', (v) => Number.isNaN(v), 'expected #{this} to be NaN', 'expected #{this} not to be NaN');
          literal('exist', (v) => v !== null && v !== undefined, 'expected #{this} to exist', 'expected #{this} to not exist');
          literal(
            'empty',
            (v) => {
              switch (typeOf(v)) {
                case 'string':
                case 'array':
                  return v.length === 0;
                case 'map':
                case 'set':
                  return v.size === 0;
                case 'object':
                  return Object.keys(v).length === 0;
                default:
                  throw new TypeError('.empty was passed ' + (v === null || v === undefined ? inspect(v) : 'non-string primitive ' + inspect(v)));
              }
            },
            'expected #{this} to be empty',
            'expected #{this} not to be empty');

          const typeCheck = function (type) {
            const name = String(type).toLowerCase();
            const article = 'aeiou'.includes(name.charAt(0)) ? 'an ' : 'a ';
            check(this, typeOf(objectOf(this)) === name, 'expected #{this} to be ' + article + name, 'expected #{this} not to be ' + article + name);
            return this;
          };
          // "a" and "an" are calls and chains (an.instanceOf): the getter returns a callable assertion.
          for (const name of ['a', 'an']) {
            Object.defineProperty(proto, name, {
              get() {
                return callableChain(this, typeCheck);
              },
              configurable: true,
            });
          }

          const lengthOfValue = (value) => {
            const type = typeOf(value);
            if (type === 'map' || type === 'set') {
              return value.size;
            }
            if (value === null || value === undefined || value.length === undefined) {
              throw new AssertionError('expected ' + inspect(value) + ' to have a length');
            }
            return value.length;
          };
          const number = (self) => {
            const value = objectOf(self);
            if (typeof value !== 'number' && typeOf(value) !== 'date') {
              throw new AssertionError((flagOf(self).message ? flagOf(self).message + ': ' : '') + 'expected ' + inspect(value) + ' to be a number or a date');
            }
            return value;
          };
          // Chai's comparison table: the text for the value, and for the length after "lengthOf".
          const compare = (names, test, word, negatedWord) =>
            method(names, function (limit) {
              const flags = flagOf(this);
              if (flags.lengthOf) {
                const length = lengthOfValue(flags.object);
                check(this, test(length, limit), 'expected #{this} to have a length ' + word + ' #{exp} but got #{act}', 'expected #{this} to have a length ' + negatedWord + ' #{exp}', limit, length);
              } else {
                check(this, test(number(this), limit), 'expected #{this} to be ' + word + ' #{exp}', 'expected #{this} to be ' + negatedWord + ' #{exp}', limit);
              }
              return this;
            });
          compare(['above', 'gt', 'greaterThan'], (v, n) => v > n, 'above', 'at most');
          compare(['least', 'gte'], (v, n) => v >= n, 'at least', 'below');
          compare(['below', 'lt', 'lessThan'], (v, n) => v < n, 'below', 'at least');
          compare(['most', 'lte'], (v, n) => v <= n, 'at most', 'above');

          method(['within'], function (start, finish) {
            const flags = flagOf(this);
            const range = start + '..' + finish;
            if (flags.lengthOf) {
              const length = lengthOfValue(flags.object);
              check(this, length >= start && length <= finish, 'expected #{this} to have a length within ' + range, 'expected #{this} to not have a length within ' + range);
            } else {
              const value = number(this);
              check(this, value >= start && value <= finish, 'expected #{this} to be within ' + range, 'expected #{this} to not be within ' + range);
            }
            return this;
          });
          method(['closeTo', 'approximately'], function (expected, delta) {
            const value = number(this);
            check(this, Math.abs(value - expected) <= delta, 'expected #{this} to be close to ' + expected + ' +/- ' + delta, 'expected #{this} not to be close to ' + expected + ' +/- ' + delta);
            return this;
          });

          // "lengthOf" is a call (lengthOf(3)) and a chain (lengthOf.above(2)): the getter arms the flag and returns a callable assertion.
          const lengthCall = (self) => {
            const callable = (expected) => {
              const flags = flagOf(self);
              flags.lengthOf = false;
              const length = lengthOfValue(flags.object);
              check(self, length === expected, 'expected #{this} to have a length of #{exp} but got #{act}', 'expected #{this} to not have a length of #{act}', expected, length);
              return self;
            };
            Object.setPrototypeOf(callable, self);
            return callable;
          };
          for (const name of ['length', 'lengthOf']) {
            Object.defineProperty(proto, name, {
              get() {
                flagOf(this).lengthOf = true;
                return lengthCall(this);
              },
              configurable: true,
            });
          }
          // ---- flags and chains of the second half ----
          for (const name of ['own', 'nested', 'ordered', 'any', 'all']) {
            Object.defineProperty(proto, name, {
              get() {
                this[flagsKey][name] = true;
                return this;
              },
              configurable: true,
            });
          }
          const prefixOf = (self) => (flagOf(self).message ? flagOf(self).message + ': ' : '');
          const hasOwn = (target, name) => Object.prototype.hasOwnProperty.call(target, name);
          // shortcut: no backslash escapes in nested paths ('a\\.b'), upgrade when a key containing a dot is needed.
          const parsePath = (path) => [...String(path).matchAll(/\[(\d+)\]|([^.[\]]+)/g)].map((m) => m[1] ?? m[2]);
          const walk = (root, parts) => {
            let current = root;
            for (const part of parts) {
              if (current === null || current === undefined || !(part in Object(current))) {
                return { found: false };
              }
              current = current[part];
            }
            return { found: true, value: current };
          };

          // "include" is a call and a chain (include.members): the getter arms "contains" and returns a callable assertion.
          const callableChain = (self, fn) => {
            const callable = (...args) => fn.apply(self, args);
            Object.setPrototypeOf(callable, self);
            return callable;
          };

          const propertyOf = (self, name, hasValue, value) => {
            const flags = flagOf(self);
            const target = flags.object;
            if (target === null || target === undefined) {
              throw new AssertionError(prefixOf(self) + 'Target cannot be null or undefined.');
            }
            let found;
            let actual;
            if (flags.nested) {
              ({ found, value: actual } = walk(target, parsePath(name)));
            } else {
              const wrapped = Object(target);
              found = flags.own ? hasOwn(wrapped, name) : name in wrapped;
              actual = found ? wrapped[name] : undefined;
            }
            const descriptor = (flags.deep ? 'deep ' : '') + (flags.own ? 'own ' : '') + (flags.nested ? 'nested ' : '') + 'property ' + inspect(name);
            if (!flags.not || !hasValue) {
              check(self, found, 'expected #{this} to have ' + descriptor, 'expected #{this} to not have ' + descriptor);
            }
            if (hasValue) {
              check(self, found && (flags.deep ? deepEqual(actual, value) : actual === value), 'expected #{this} to have ' + descriptor + ' of #{exp}, but got #{act}', 'expected #{this} to not have ' + descriptor + ' of #{act}', value, actual);
            }
            flags.object = actual;
          };
          method(['property'], function (name, ...value) {
            propertyOf(this, name, value.length > 0, value[0]);
            return this;
          });
          method(['ownProperty', 'haveOwnProperty'], function (name) {
            const target = objectOf(this);
            if (target === null || target === undefined) {
              throw new AssertionError(prefixOf(this) + 'Target cannot be null or undefined.');
            }
            check(this, hasOwn(Object(target), name), 'expected #{this} to have own property ' + inspect(name), 'expected #{this} to not have own property ' + inspect(name));
            return this;
          });

          const includeOf = function (val) {
            const flags = flagOf(this);
            const target = flags.object;
            const type = typeOf(target);
            const same = (a, b) => (flags.deep ? deepEqual(a, b) : a === b);
            let included;
            if (type === 'string') {
              included = target.indexOf(val) !== -1;
            } else if (type === 'array') {
              included = flags.deep ? target.some((item) => deepEqual(item, val)) : target.indexOf(val) !== -1;
            } else if (type === 'map' || type === 'set') {
              included = [...target.values()].some((item) => same(item, val));
            } else {
              if (target === null || target === undefined) {
                throw new AssertionError(prefixOf(this) + 'object tested must be an array, a map, an object, a set, a string, or a weakset, but ' + typeOf(target) + ' given');
              }
              if (val !== Object(val)) {
                throw new AssertionError(prefixOf(this) + 'the given combination of arguments (' + type + ' and ' + typeOf(val) + ') is invalid for this assertion. You can use an array, a map, an object, a set, a string, or a weakset instead of a ' + typeOf(val));
              }
              const names = Object.keys(val);
              let first = null;
              let failures = 0;
              for (const name of names) {
                const sub = new Assertion(target, flags.message);
                Object.assign(sub[flagsKey], flags);
                try {
                  propertyOf(sub, name, true, val[name]);
                } catch (error) {
                  if (!flags.not || names.length === 1) {
                    throw error;
                  }
                  first ??= error;
                  failures++;
                }
              }
              if (flags.not && names.length > 1 && failures === names.length) {
                throw first;
              }
              return this;
            }
            check(this, included, 'expected #{this} to include #{exp}', 'expected #{this} to not include #{exp}', val);
            return this;
          };
          for (const name of ['include', 'includes', 'contain', 'contains']) {
            Object.defineProperty(proto, name, {
              get() {
                flagOf(this).contains = true;
                return callableChain(this, includeOf);
              },
              configurable: true,
            });
          }

          const requireArray = (self, value) => {
            if (!Array.isArray(value)) {
              throw new AssertionError(prefixOf(self) + 'expected ' + inspect(value) + ' to be an array');
            }
          };
          method(['members'], function (subset) {
            const flags = flagOf(this);
            requireArray(this, flags.object);
            requireArray(this, subset);
            const same = (a, b) => (flags.deep ? deepEqual(a, b) : a === b);
            let superset = flags.object;
            let ok = true;
            if (!flags.contains) {
              ok = subset.length === superset.length;
              superset = superset.slice();
            }
            ok = ok && subset.every((item, index) => {
              if (flags.ordered) {
                return same(item, superset[index]);
              }
              const at = superset.findIndex((other) => same(item, other));
              if (at === -1) {
                return false;
              }
              if (!flags.contains) {
                superset.splice(at, 1);
              }
              return true;
            });
            const subject = flags.contains ? (flags.ordered ? 'an ordered superset' : 'a superset') : flags.ordered ? 'ordered members' : 'members';
            const positive = flags.contains ? 'expected #{this} to be ' + subject + ' of #{exp}' : 'expected #{this} to have the same ' + subject + ' as #{exp}';
            const negative = flags.contains ? 'expected #{this} to not be ' + subject + ' of #{exp}' : 'expected #{this} to not have the same ' + subject + ' as #{exp}';
            check(this, ok, positive, negative, subset, flags.object, true);
            return this;
          });

          method(['keys', 'key'], function (...given) {
            const flags = flagOf(this);
            const target = flags.object;
            const type = typeOf(target);
            const actual = type === 'map' ? [...target.keys()] : type === 'set' ? [...target] : Object.keys(Object(target));
            const expected = given.length === 1 && typeof given[0] === 'object' && given[0] !== null ? (Array.isArray(given[0]) ? given[0] : Object.keys(given[0])) : given;
            if (expected.length === 0) {
              throw new AssertionError(prefixOf(this) + 'keys required');
            }
            const any = flags.any === true;
            const present = (k) => actual.some((a) => (flags.deep ? deepEqual(k, a) : k === a));
            const ok = any ? expected.some(present) : expected.every(present) && (flags.contains === true || expected.length === actual.length);
            let text;
            if (expected.length > 1) {
              const rendered = expected.map((k) => inspect(k));
              const last = rendered.pop();
              text = 'keys ' + rendered.join(', ') + (any ? ', or ' : ', and ') + last;
            } else {
              text = 'key ' + inspect(expected[0]);
            }
            text = (flags.contains ? 'contain ' : 'have ') + text;
            const order = (list) => [...list].sort((a, b) => (inspect(a) < inspect(b) ? -1 : inspect(a) > inspect(b) ? 1 : 0));
            check(this, ok, 'expected #{this} to ' + text, 'expected #{this} to not ' + text, order(expected), order(actual), true);
            return this;
          });

          method(['match', 'matches'], function (re) {
            check(this, re.exec(objectOf(this)) !== null, 'expected #{this} to match #{exp}', 'expected #{this} not to match #{exp}', re);
            return this;
          });
          method(['string'], function (text) {
            const value = objectOf(this);
            if (typeof value !== 'string') {
              throw new AssertionError(prefixOf(this) + 'expected ' + inspect(value) + ' to be a string');
            }
            check(this, value.indexOf(text) !== -1, 'expected #{this} to contain #{exp}', 'expected #{this} to not contain #{exp}', text);
            return this;
          });
          method(['oneOf'], function (list) {
            requireArray(this, list);
            check(this, list.indexOf(objectOf(this)) !== -1, 'expected #{this} to be one of #{exp}', 'expected #{this} to not be one of #{exp}', list);
            return this;
          });
          method(['instanceOf', 'instanceof'], function (ctor) {
            const name = typeof ctor === 'function' ? ctor.name : inspect(ctor);
            check(this, objectOf(this) instanceof ctor, 'expected #{this} to be an instance of ' + name, 'expected #{this} to not be an instance of ' + name);
            return this;
          });
          method(['satisfy', 'satisfies'], function (matcher) {
            const result = matcher(objectOf(this));
            check(this, Boolean(result), 'expected #{this} to satisfy ' + inspect(matcher), 'expected #{this} to not satisfy ' + inspect(matcher), true, result);
            return this;
          });
          method(['throw', 'throws', 'Throw'], function (errorLike, matcher) {
            const flags = flagOf(this);
            const fn = flags.object;
            if (typeof fn !== 'function') {
              throw new AssertionError(prefixOf(this) + 'expected ' + inspect(fn) + ' to be a function');
            }
            if (errorLike instanceof RegExp || typeof errorLike === 'string') {
              matcher = errorLike;
              errorLike = null;
            }
            let thrown = false;
            let caught;
            try {
              fn();
            } catch (error) {
              thrown = true;
              caught = error;
            }
            const message = caught instanceof Error ? caught.message : String(caught);
            const described = caught instanceof Error ? String(caught) : typeof caught === 'string' ? caught : inspect(caught);
            const name = typeof errorLike === 'function' ? errorLike.name : errorLike ? inspect(errorLike) : 'an error';
            if ((errorLike === undefined && matcher === undefined) || !flags.not) {
              check(this, thrown, 'expected #{this} to throw ' + name, 'expected #{this} to not throw an error but #{act} was thrown', undefined, described);
            }
            if (!thrown || (!errorLike && matcher === undefined)) {
              return this;
            }
            const compatible = typeof errorLike === 'function' ? caught instanceof errorLike : errorLike instanceof Error ? caught === errorLike : true;
            const kind = matcher instanceof RegExp ? 'matching' : 'including';
            const matches = matcher === undefined || (matcher instanceof RegExp ? matcher.test(message) : message.indexOf(matcher) !== -1);
            if (flags.not) {
              check(this, compatible && matches, '', errorLike ? 'expected #{this} to not throw ' + name : 'expected #{this} to throw error not ' + kind + ' #{exp}', matcher);
            } else {
              check(this, compatible, 'expected #{this} to throw ' + name + ' but #{act} was thrown', '', undefined, described);
              check(this, matches, 'expected #{this} to throw error ' + kind + ' #{exp} but got #{act}', '', matcher, message);
            }
            return this;
          });

          // ---- yaapic.response.to: Postman's response assertions, one fresh chain per access ----
          class ResponseAssertion {
            constructor(dto) {
              Object.defineProperty(this, flagsKey, { value: { dto, not: false } });
            }
          }
          const rproto = ResponseAssertion.prototype;
          for (const name of ['to', 'be', 'been', 'is', 'and', 'have', 'has', 'with', 'that', 'a', 'an']) {
            Object.defineProperty(rproto, name, {
              get() {
                return this;
              },
            });
          }
          Object.defineProperty(rproto, 'not', {
            get() {
              this[flagsKey].not = true;
              return this;
            },
          });
          const rcheck = (self, ok, positive, negative) => {
            const flags = self[flagsKey];
            if (flags.not ? ok : !ok) {
              throw new AssertionError(flags.not ? negative : positive);
            }
          };
          const rflag = (name, test, positive) =>
            Object.defineProperty(rproto, name, {
              get() {
                const dto = this[flagsKey].dto;
                rcheck(this, test(dto), positive(dto), 'expected response to not be ' + name);
                return this;
              },
            });
          const contentType = (dto) => (dto.headers.find((h) => h.key.toLowerCase() === 'content-type')?.value ?? '').toLowerCase();
          const byCode = (name, test) => rflag(name, (dto) => test(dto.code), (dto) => 'expected response to be ' + name + ' but got ' + dto.code);
          byCode('ok', (c) => c === 200);
          byCode('success', (c) => c >= 200 && c < 300);
          byCode('info', (c) => c >= 100 && c < 200);
          byCode('redirection', (c) => c >= 300 && c < 400);
          byCode('clientError', (c) => c >= 400 && c < 500);
          byCode('serverError', (c) => c >= 500 && c < 600);
          byCode('error', (c) => c >= 400 && c < 600);
          byCode('accepted', (c) => c === 202);
          byCode('badRequest', (c) => c === 400);
          byCode('unauthorized', (c) => c === 401);
          byCode('forbidden', (c) => c === 403);
          byCode('notFound', (c) => c === 404);
          byCode('rateLimited', (c) => c === 429);
          Object.defineProperty(rproto, 'withBody', {
            get() {
              rcheck(this, this[flagsKey].dto.text.length > 0, 'expected response to have a body', 'expected response to not have a body');
              return this;
            },
          });
          const byType = (name, test) =>
            rflag(name, (dto) => test(contentType(dto)), (dto) => 'expected response to be ' + name + ' but got ' + (contentType(dto) === '' ? 'no content type' : 'content type ' + inspect(contentType(dto))));
          byType('json', (t) => /^application\/(.+\+)?json\b/.test(t) || t.startsWith('text/json'));
          byType('html', (t) => t.startsWith('text/html'));
          byType('text', (t) => t.startsWith('text/'));

          const rmethod = (name, fn) => Object.defineProperty(rproto, name, { value: fn });
          rmethod('status', function (expected) {
            const dto = this[flagsKey].dto;
            if (typeof expected === 'string') {
              rcheck(this, dto.status === expected, 'expected response to have status reason ' + inspect(expected) + ' but got ' + inspect(dto.status), 'expected response to not have status reason ' + inspect(expected));
            } else {
              rcheck(this, dto.code === expected, 'expected response to have status code ' + expected + ' but got ' + dto.code, 'expected response to not have status code ' + expected);
            }
            return this;
          });
          rmethod('header', function (name, ...value) {
            const dto = this[flagsKey].dto;
            const wanted = String(name).toLowerCase();
            const rows = dto.headers.filter((h) => h.key.toLowerCase() === wanted);
            if (value.length === 0) {
              rcheck(this, rows.length > 0, 'expected response to have header with key ' + inspect(name), 'expected response to not have header with key ' + inspect(name));
            } else {
              rcheck(
                this,
                rows.some((h) => h.value === value[0]),
                'expected response to have header ' + inspect(name) + ' with value ' + inspect(value[0]) + (rows.length > 0 ? ' but got ' + inspect(rows[0].value) : ''),
                'expected response to not have header ' + inspect(name) + ' with value ' + inspect(value[0]));
            }
            return this;
          });
          rmethod('body', function (expected) {
            const text = this[flagsKey].dto.text;
            if (expected === undefined) {
              rcheck(this, text.length > 0, 'expected response to have a body', 'expected response to not have a body');
            } else if (expected instanceof RegExp) {
              rcheck(this, expected.test(text), 'expected response body to match ' + expected + ' but got ' + inspect(text), 'expected response body to not match ' + expected);
            } else {
              rcheck(this, text === String(expected), 'expected response to have body ' + inspect(String(expected)) + ' but got ' + inspect(text), 'expected response to not have body ' + inspect(String(expected)));
            }
            return this;
          });
          rmethod('jsonBody', function (path, ...value) {
            let parsed;
            let valid = true;
            try {
              parsed = JSON.parse(this[flagsKey].dto.text);
            } catch {
              valid = false;
            }
            if (path === undefined) {
              rcheck(this, valid, 'expected response to have a JSON body but the body is not valid JSON', 'expected response to not have a JSON body');
              return this;
            }
            const at = valid ? walk(parsed, parsePath(path)) : { found: false };
            if (value.length === 0) {
              rcheck(this, at.found, 'expected response JSON body to have path ' + inspect(path), 'expected response JSON body to not have path ' + inspect(path));
            } else {
              rcheck(
                this,
                at.found && deepEqual(at.value, value[0]),
                'expected response JSON body path ' + inspect(path) + ' to equal ' + inspect(value[0]) + (at.found ? ' but got ' + inspect(at.value) : ' but the path does not exist'),
                'expected response JSON body path ' + inspect(path) + ' to not equal ' + inspect(value[0]));
            }
            return this;
          });
          rmethod('jsonSchema', () => {
            throw new Error('yaapic.response.to.have.jsonSchema is not supported in Yaapic');
          });
          Object.freeze(rproto);
          Object.defineProperty(globalThis, '__responseTo', { value: (dto) => new ResponseAssertion(dto) });
          Object.freeze(proto);

          // ---- yaapic.expect ----
          const expect = (value, message) => new Assertion(value, message === undefined ? undefined : String(message));
          expect.fail = (message) => {
            throw new AssertionError(message === undefined ? 'expect.fail()' : String(message));
          };
          define(yaapic, 'expect', Object.freeze(expect));

          // ---- yaapic.test ----
          let recorded = 0;
          let pending = 0;
          // The engine reads this after the block: a test that has not settled by then can never settle (no timers).
          Object.defineProperty(globalThis, '__pendingTests', { value: () => pending });

          const describe = (thrown) => (thrown instanceof Error ? thrown.name + ': ' + thrown.message : String(thrown));
          const test = (name, fn) => {
            const label = String(name);
            const started = Date.now();
            let settled = false;
            let counted = false;
            const record = (passed, message) => {
              if (settled) {
                return;
              }
              settled = true;
              if (counted) {
                pending--;
              }
              recorded++;
              host('test', { name: label, passed, message: message ?? null, ms: Date.now() - started });
            };
            const finish = (error) => (error === undefined || error === null ? record(true) : record(false, describe(error)));
            if (fn === undefined || fn === null) {
              record(true);
              return;
            }
            if (typeof fn !== 'function') {
              record(false, 'TypeError: ' + inspect(fn) + ' is not a function');
              return;
            }
            try {
              if (fn.length >= 1) {
                counted = true;
                pending++;
                fn((error) => finish(error === undefined || error === null || error instanceof Error ? error : new Error(String(error))));
                return;
              }
              const result = fn();
              if (result !== null && typeof result === 'object' && typeof result.then === 'function') {
                counted = true;
                pending++;
                result.then(() => finish(undefined), (error) => finish(error ?? new Error('Promise rejected')));
                return;
              }
              record(true);
            } catch (error) {
              record(false, describe(error));
            }
          };
          test.skip = (name) => {
            host('console', { level: 'info', text: 'skipped ' + String(name) });
          };
          test.index = () => recorded;
          define(yaapic, 'test', Object.freeze(test));
        })();
        """;
}

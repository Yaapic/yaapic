namespace Yaapic.Scripting.QuickJs;

/// <summary>The JavaScript <c>pm</c> prelude, part 3: <c>pm.test</c>, <c>AssertionError</c> and the core Chai-style <c>pm.expect</c> assertions.</summary>
internal static class PmTestPrelude
{
    /// <summary>The prelude source, evaluated as its own script after <see cref="PmRequestResponsePrelude.Source"/>.</summary>
    internal const string Source = """
        (() => {
          'use strict';
          const host = globalThis.__host;
          const pm = globalThis.pm;
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

          method(['a', 'an'], function (type) {
            const name = String(type).toLowerCase();
            const article = 'aeiou'.includes(name.charAt(0)) ? 'an ' : 'a ';
            check(this, typeOf(objectOf(this)) === name, 'expected #{this} to be ' + article + name, 'expected #{this} not to be ' + article + name);
            return this;
          });

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
          Object.freeze(proto);

          // ---- pm.expect ----
          const expect = (value, message) => new Assertion(value, message === undefined ? undefined : String(message));
          expect.fail = (message) => {
            throw new AssertionError(message === undefined ? 'expect.fail()' : String(message));
          };
          define(pm, 'expect', Object.freeze(expect));

          // ---- pm.test ----
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
          define(pm, 'test', Object.freeze(test));
        })();
        """;
}

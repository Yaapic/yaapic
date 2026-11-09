namespace Yaapic.Scripting.QuickJs;

/// <summary>The JavaScript side of the engine: preludes are constants, nothing is read from disk (ADR 0009).</summary>
internal static class JsPrelude
{
    /// <summary>The bridge wrapper <c>__host(op, args)</c> and the <c>console</c> object.</summary>
    internal const string Core = """
        (() => {
          'use strict';
          const raw = globalThis.__hostRaw;
          delete globalThis.__hostRaw;

          // Calls the host: the result is {"ok":...} or {"err":"message"}, an err becomes a thrown Error.
          const host = (op, args) => {
            const reply = JSON.parse(raw(op, JSON.stringify(args === undefined ? null : args)));
            if (reply.err !== undefined) {
              throw new Error(reply.err);
            }
            return reply.ok;
          };
          Object.defineProperty(globalThis, '__host', { value: host });

          const circular = () => {
            const seen = new Set();
            return (key, value) => {
              if (typeof value === 'object' && value !== null) {
                if (seen.has(value)) {
                  return '[Circular]';
                }
                seen.add(value);
              }
              return value;
            };
          };

          const format = (value) => {
            if (typeof value === 'string') {
              return value;
            }
            if (value === undefined) {
              return 'undefined';
            }
            if (value instanceof Error) {
              const frame = typeof value.stack === 'string' ? value.stack.split('\n')[0].trim() : '';
              return frame ? value.message + ' ' + frame : String(value.message);
            }
            if (typeof value === 'object' && value !== null) {
              let json;
              try {
                json = JSON.stringify(value);
              } catch (e) {
                json = JSON.stringify(value, circular());
              }
              return json === undefined ? String(value) : json;
            }
            return String(value);
          };

          const write = (level) => (...args) => {
            host('console', { level, text: args.map(format).join(' ') });
          };
          const console = {};
          for (const level of ['log', 'info', 'warn', 'error', 'debug']) {
            Object.defineProperty(console, level, { value: write(level), enumerable: true });
          }
          Object.defineProperty(globalThis, 'console', { value: Object.freeze(console), configurable: true });
        })();
        """;

    /// <summary>Lists the prelude parts in their fixed order; each is evaluated as its own script (later cards append the other <c>pm</c> parts).</summary>
    /// <returns>The prelude sources.</returns>
    internal static string[] Build() => [Core, PmVariablesPrelude.Source, PmRequestResponsePrelude.Source, PmTestPrelude.Source];
}

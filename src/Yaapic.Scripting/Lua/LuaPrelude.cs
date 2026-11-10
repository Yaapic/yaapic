using System.Globalization;
using System.Text;

namespace Yaapic.Scripting.Lua;

/// <summary>The Lua side of the engine: preludes are constants, nothing is read from disk (ADR 0009).</summary>
internal static class LuaPrelude
{
    /// <summary>
    /// Wraps the raw <c>__host(op, argsJson)</c> so a failure raises a Lua error positioned at the caller, and defines
    /// <c>console.*</c> and <c>print</c>.
    /// </summary>
    internal const string Core = """
        do
          local rawHost = __host
          local concat, sort, format = table.concat, table.sort, string.format
          local type, tostring, pairs, ipairs, select, error, pcall, getmetatable =
            type, tostring, pairs, ipairs, select, error, pcall, getmetatable

          -- The host raises through the C function; re-raise at level 2 so the message carries the script line.
          local function host(op, args)
            local ok, reply = pcall(rawHost, op, args)
            if not ok then
              error(reply, 2)
            end
            return reply
          end
          __host = host

          local MAX_DEPTH = 3

          local function keyText(k)
            if type(k) == 'string' then
              if k:find('^[%a_][%w_]*$') then
                return k
              end
              return '["' .. k .. '"]'
            end
            return '[' .. tostring(k) .. ']'
          end

          local function keyLess(a, b)
            local ta, tb = type(a), type(b)
            if ta ~= tb then
              return ta < tb
            end
            if ta == 'number' or ta == 'string' then
              return a < b
            end
            return tostring(a) < tostring(b)
          end

          local fmt

          -- depth is the nesting level of t (the printed value is level 1); path holds the tables being printed (cycles).
          local function fmtTable(t, depth, path)
            if path[t] then
              return '<cycle>'
            end
            if depth > MAX_DEPTH then
              return '...'
            end
            path[t] = true
            local parts, n = {}, #t
            for i = 1, n do
              parts[#parts + 1] = fmt(t[i], depth + 1, path, true)
            end
            local keys = {}
            for k in pairs(t) do
              if not (type(k) == 'number' and k >= 1 and k <= n and k % 1 == 0) then
                keys[#keys + 1] = k
              end
            end
            sort(keys, keyLess)
            for _, k in ipairs(keys) do
              parts[#parts + 1] = keyText(k) .. ' = ' .. fmt(t[k], depth + 1, path, true)
            end
            path[t] = nil
            if #parts == 0 then
              return '{}'
            end
            return '{ ' .. concat(parts, ', ') .. ' }'
          end

          fmt = function(v, depth, path, nested)
            local kind = type(v)
            if kind == 'string' then
              return nested and ('"' .. v .. '"') or v
            end
            if kind == 'table' then
              local mt = getmetatable(v)
              if type(mt) == 'table' and mt.__tostring ~= nil then
                return tostring(v)
              end
              return fmtTable(v, depth, path)
            end
            return tostring(v)
          end

          local function writer(level)
            return function(...)
              local parts = {}
              for i = 1, select('#', ...) do
                parts[i] = fmt((select(i, ...)), 1, {}, false)
              end
              host('console.plain', level .. '\n' .. concat(parts, ' '))
            end
          end

          console = {}
          for _, level in ipairs({ 'log', 'info', 'warn', 'error', 'debug' }) do
            console[level] = writer(level)
          end
          print = console.log
        end
        """;

    /// <summary>Builds the chunk defining <c>__yaapic</c> for a run.</summary>
    /// <param name="phase">The phase name as scripts see it (<c>preRequest</c> or <c>postResponse</c>).</param>
    /// <param name="origin">The origin text of the run.</param>
    /// <returns>The Lua source.</returns>
    internal static string Context(string phase, string origin) =>
        $"__yaapic = {{ phase = {Literal(phase)}, origin = {Literal(origin)} }}";

    private static string Literal(string text)
    {
        StringBuilder sb = new("\"");
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case < ' ': sb.Append('\\').Append(((int)c).ToString("D3", CultureInfo.InvariantCulture)); break;
                default: sb.Append(c); break;
            }
        }

        return sb.Append('"').ToString();
    }
}

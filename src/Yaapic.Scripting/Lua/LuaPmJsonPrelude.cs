namespace Yaapic.Scripting.Lua;

/// <summary>The Lua <c>yaapic</c> prelude, part 1: <c>yaapic.null</c>, the JSON codec <c>yaapic.json</c> and the typed host wrapper <c>__hostCall</c>.</summary>
internal static class LuaPmJsonPrelude
{
    /// <summary>The prelude source, evaluated as its own chunk after <see cref="LuaPrelude.Core"/>.</summary>
    internal const string Source = """
        do
          local rawHost = __host
          local type, tostring, tonumber, pairs, next, error, pcall, setmetatable, getmetatable, rawequal =
            type, tostring, tonumber, pairs, next, error, pcall, setmetatable, getmetatable, rawequal
          local find, sub, byte, match, format, gsub = string.find, string.sub, string.byte, string.match, string.format, string.gsub
          local concat, sort = table.concat, table.sort
          local mtype, utf8len, utf8char = math.type, utf8.len, utf8.char

          local MAX_DEPTH = 100

          -- The JSON null sentinel: frozen, prints as "null".
          local NULL = setmetatable({}, {
            __tostring = function() return 'null' end,
            __newindex = function() error('yaapic.null is read-only', 2) end,
          })

          -- Marks tables that are JSON arrays even when empty; decoded arrays carry it.
          local ARRAY = {}

          local json = {}

          function json.array(t)
            return setmetatable(t or {}, ARRAY)
          end

          local escapes = {
            ['"'] = '\\"', ['\\'] = '\\\\', ['\b'] = '\\b', ['\f'] = '\\f', ['\n'] = '\\n', ['\r'] = '\\r', ['\t'] = '\\t',
          }

          local function escapeChar(c)
            return escapes[c] or format('\\u%04x', byte(c))
          end

          local function numberText(v)
            if mtype(v) == 'integer' then
              return format('%d', v)
            end
            if v ~= v or v == math.huge or v == -math.huge then
              error('cannot encode ' .. tostring(v), 0)
            end
            for precision = 15, 17 do
              local text = format('%.' .. precision .. 'g', v)
              if tonumber(text) == v or precision == 17 then
                return text
              end
            end
          end

          local encodeValue

          local function encodeArray(t, n, depth, buf)
            buf[#buf + 1] = '['
            for i = 1, n do
              if i > 1 then
                buf[#buf + 1] = ','
              end
              encodeValue(t[i], depth + 1, buf)
            end
            buf[#buf + 1] = ']'
          end

          local function encodeTable(t, depth, buf)
            if depth > MAX_DEPTH then
              error('cannot encode: nesting deeper than ' .. MAX_DEPTH, 0)
            end
            local n = #t
            if getmetatable(t) == ARRAY then
              return encodeArray(t, n, depth, buf)
            end
            local count, strings = 0, 0
            for k in pairs(t) do
              count = count + 1
              if type(k) == 'string' then
                strings = strings + 1
              end
            end
            if n > 0 and count == n then
              return encodeArray(t, n, depth, buf)
            end
            if n > 0 and strings > 0 then
              error('cannot encode mixed table', 0)
            end
            local keys = {}
            for k in pairs(t) do
              if type(k) == 'number' then
                k = tostring(k)
              elseif type(k) ~= 'string' then
                error('cannot encode ' .. type(k) .. ' key', 0)
              end
              keys[#keys + 1] = k
            end
            sort(keys)
            buf[#buf + 1] = '{'
            for i, k in ipairs(keys) do
              local v = t[k]
              if v == nil then
                v = t[tonumber(k)]
              end
              if i > 1 then
                buf[#buf + 1] = ','
              end
              buf[#buf + 1] = '"' .. gsub(k, '[\0-\31"\\]', escapeChar) .. '":'
              encodeValue(v, depth + 1, buf)
            end
            buf[#buf + 1] = '}'
          end

          encodeValue = function(v, depth, buf)
            local kind = type(v)
            if kind == 'string' then
              if not utf8len(v) then
                error('cannot encode invalid UTF-8', 0)
              end
              buf[#buf + 1] = '"' .. gsub(v, '[\0-\31"\\]', escapeChar) .. '"'
            elseif kind == 'number' then
              buf[#buf + 1] = numberText(v)
            elseif kind == 'boolean' then
              buf[#buf + 1] = v and 'true' or 'false'
            elseif v == nil or rawequal(v, NULL) then
              buf[#buf + 1] = 'null'
            elseif kind == 'table' then
              encodeTable(v, depth, buf)
            else
              error('cannot encode ' .. kind, 0)
            end
          end

          function json.encode(value)
            local buf = {}
            encodeValue(value, 1, buf)
            return concat(buf)
          end

          local unescapes = {
            [34] = '"', [92] = '\\', [47] = '/', [98] = '\b', [102] = '\f', [110] = '\n', [114] = '\r', [116] = '\t',
          }

          function json.decode(text)
            if type(text) ~= 'string' then
              error('json.decode expects a string, got ' .. type(text), 2)
            end
            local pos, len = 1, #text

            local function fail(reason, at)
              error('invalid JSON at position ' .. (at or pos) .. ': ' .. reason, 0)
            end

            local function skip()
              pos = find(text, '[^ \t\r\n]', pos) or (len + 1)
            end

            -- pos is at the opening quote.
            local function decodeString()
              local opening = pos
              local i = pos + 1
              local buf, n = {}, 0
              while true do
                local j = find(text, '["\\\0-\31]', i)
                if not j then
                  fail('unterminated string', opening)
                end
                if j > i then
                  n = n + 1
                  buf[n] = sub(text, i, j - 1)
                end
                local c = byte(text, j)
                if c == 34 then
                  pos = j + 1
                  return concat(buf)
                elseif c ~= 92 then
                  fail('control character in string', j)
                end
                local e = byte(text, j + 1)
                if e == 117 then
                  local hex = match(text, '^%x%x%x%x', j + 2)
                  if not hex then
                    fail('invalid unicode escape', j)
                  end
                  local cp = tonumber(hex, 16)
                  i = j + 6
                  if cp >= 0xD800 and cp <= 0xDBFF then
                    local low = match(text, '^\\u(%x%x%x%x)', i)
                    low = low and tonumber(low, 16)
                    if not low or low < 0xDC00 or low > 0xDFFF then
                      fail('invalid surrogate pair', j)
                    end
                    cp = 0x10000 + ((cp - 0xD800) << 10) + (low - 0xDC00)
                    i = i + 6
                  elseif cp >= 0xDC00 and cp <= 0xDFFF then
                    fail('lone low surrogate', j)
                  end
                  n = n + 1
                  buf[n] = utf8char(cp)
                else
                  local replacement = e and unescapes[e]
                  if not replacement then
                    fail('invalid escape', j)
                  end
                  n = n + 1
                  buf[n] = replacement
                  i = j + 2
                end
              end
            end

            local function decodeNumber()
              local start = pos
              local int = match(text, '^-?%d+', pos)
              if not int then
                fail('unexpected character')
              end
              local digits = sub(int, 1, 1) == '-' and sub(int, 2) or int
              if #digits > 1 and sub(digits, 1, 1) == '0' then
                fail('leading zeros are not allowed')
              end
              pos = pos + #int
              local isFloat = false
              if byte(text, pos) == 46 then
                local frac = match(text, '^%.%d+', pos)
                if not frac then
                  fail('digits expected after the decimal point', pos + 1)
                end
                pos = pos + #frac
                isFloat = true
              end
              local e = byte(text, pos)
              if e == 101 or e == 69 then
                local exp = match(text, '^[eE][+-]?%d+', pos)
                if not exp then
                  fail('digits expected in the exponent', pos + 1)
                end
                pos = pos + #exp
                isFloat = true
              end
              -- Beyond int64 tonumber already yields a float.
              return tonumber(sub(text, start, pos - 1))
            end

            local value

            local function decodeObject(depth)
              pos = pos + 1
              local obj = {}
              skip()
              if byte(text, pos) == 125 then
                pos = pos + 1
                return obj
              end
              while true do
                skip()
                if byte(text, pos) ~= 34 then
                  fail('expected a string key')
                end
                local key = decodeString()
                skip()
                if byte(text, pos) ~= 58 then
                  fail("expected ':'")
                end
                pos = pos + 1
                obj[key] = value(depth + 1)
                skip()
                local c = byte(text, pos)
                pos = pos + 1
                if c == 125 then
                  return obj
                elseif c ~= 44 then
                  fail("expected ',' or '}'", pos - 1)
                end
              end
            end

            local function decodeArray(depth)
              pos = pos + 1
              local arr, n = setmetatable({}, ARRAY), 0
              skip()
              if byte(text, pos) == 93 then
                pos = pos + 1
                arr.n = 0
                return arr
              end
              while true do
                n = n + 1
                arr[n] = value(depth + 1)
                skip()
                local c = byte(text, pos)
                pos = pos + 1
                if c == 93 then
                  arr.n = n
                  return arr
                elseif c ~= 44 then
                  fail("expected ',' or ']'", pos - 1)
                end
              end
            end

            value = function(depth)
              skip()
              local c = byte(text, pos)
              if not c then
                fail('unexpected end of input')
              end
              if c == 123 or c == 91 then
                if depth > MAX_DEPTH then
                  fail('nesting deeper than ' .. MAX_DEPTH)
                end
                return c == 123 and decodeObject(depth) or decodeArray(depth)
              elseif c == 34 then
                return decodeString()
              elseif c == 45 or (c >= 48 and c <= 57) then
                return decodeNumber()
              end
              for literal, result in pairs({ ['true'] = true, ['false'] = false, ['null'] = NULL }) do
                if sub(text, pos, pos + #literal - 1) == literal then
                  pos = pos + #literal
                  return result
                end
              end
              fail('unexpected character')
            end

            local result = value(1)
            skip()
            if pos <= len then
              fail('trailing characters')
            end
            return result
          end

          -- Calls the host with a Lua table and returns the decoded result (arrays come back with an n field).
          -- pcall keeps the raw message: the core wrapper would otherwise prefix it with a prelude position.
          local function hostCall(op, args)
            local ok, reply = pcall(rawHost, op, json.encode(args == nil and {} or args))
            if not ok then
              error(reply, 0)
            end
            local envelope = json.decode(reply)
            if envelope.err ~= nil then
              error(envelope.err, 0)
            end
            return envelope.ok
          end

          __hostCall = hostCall
          yaapic = { null = NULL, json = { encode = json.encode, decode = json.decode, array = json.array } }
        end
        """;
}

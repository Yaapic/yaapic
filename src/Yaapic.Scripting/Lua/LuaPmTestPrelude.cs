namespace Yaapic.Scripting.Lua;

/// <summary>
/// The Lua <c>pm</c> prelude, part 4: <c>pm.test</c>, <c>AssertionError</c> values and the core <c>pm.expect</c> assertions.
/// The Lua twin of <see cref="QuickJs.PmTestPrelude"/>: failure messages are the same text in both languages.
/// </summary>
internal static class LuaPmTestPrelude
{
    /// <summary>The prelude source, evaluated as its own chunk after <see cref="LuaPmRequestResponsePrelude.Source"/>.</summary>
    internal const string Source = """
        do
          local hostCall, define = __hostCall, __pmDefine
          local NULL, json = pm.null, pm.json
          local ARRAY = getmetatable(json.decode('[]'))
          local type, error, pcall, setmetatable, getmetatable, tostring, rawequal, rawget, rawlen, next, ipairs =
            type, error, pcall, setmetatable, getmetatable, tostring, rawequal, rawget, rawlen, next, ipairs
          local find, sub, gsub, format, lower = string.find, string.sub, string.gsub, string.format, string.lower
          local concat, sort = table.concat, table.sort
          local mtype, floor, huge = math.type, math.floor, math.huge

          -- ---- inspect: JavaScript-like value rendering (so messages match across languages), depth 2, 80 characters ----
          local MAX_DEPTH, MAX_LENGTH = 2, 80

          -- Integral floats print without ".0", like JavaScript numbers.
          local function num(v)
            if mtype(v) == 'integer' then
              return format('%d', v)
            elseif v ~= v then
              return 'NaN'
            elseif v == huge then
              return 'Infinity'
            elseif v == -huge then
              return '-Infinity'
            elseif v == 0 then
              return 1 / v < 0 and '-0' or '0'
            elseif v == floor(v) and v > -1e15 and v < 1e15 then
              return format('%d', v)
            end
            for precision = 15, 17 do
              local text = format('%.' .. precision .. 'g', v)
              if tonumber(text) == v or precision == 17 then
                return text
              end
            end
          end

          -- The length of a table that is an array (decoded JSON, pm.json.array, or keys 1..n), nil for any other table.
          local function arrayLength(t)
            local mt = getmetatable(t)
            if mt == ARRAY then
              local n = rawget(t, 'n')
              return mtype(n) == 'integer' and n >= 0 and n or rawlen(t)
            elseif mt ~= nil then
              return nil
            end
            local n = rawlen(t)
            if n == 0 then
              return nil
            end
            for k in next, t do
              if mtype(k) ~= 'integer' or k < 1 or k > n then
                return nil
              end
            end
            return n
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

          local function key(k)
            if type(k) == 'string' and find(k, '^[%a_$][%w_$]*$') then
              return k
            end
            return "'" .. tostring(k) .. "'"
          end

          local function wrap(open, items, close)
            if #items == 0 then
              return open .. close
            end
            return open .. ' ' .. concat(items, ', ') .. ' ' .. close
          end

          local render
          render = function(v, depth, seen)
            local kind = type(v)
            if kind == 'string' then
              return "'" .. (gsub(gsub(v, '\\', '\\\\'), "'", "\\'")) .. "'"
            elseif kind == 'number' then
              return num(v)
            elseif kind == 'nil' then
              return 'undefined'
            elseif kind == 'function' then
              return '[Function (anonymous)]'
            elseif kind ~= 'table' then
              return tostring(v)
            elseif rawequal(v, NULL) then
              return 'null'
            elseif seen[v] then
              return '[Circular]'
            end
            local mt = getmetatable(v)
            if type(mt) == 'table' and mt ~= ARRAY and mt.__tostring ~= nil then
              return tostring(v)
            end
            local n = arrayLength(v)
            if depth > MAX_DEPTH then
              return n and '[Array]' or '[Object]'
            end
            seen[v] = true
            local items = {}
            if n then
              for i = 1, n do
                items[i] = render(rawget(v, i), depth + 1, seen)
              end
            else
              local keys = {}
              for k in next, v do
                keys[#keys + 1] = k
              end
              sort(keys, keyLess)
              for i, k in ipairs(keys) do
                items[i] = key(k) .. ': ' .. render(rawget(v, k), depth + 1, seen)
              end
            end
            seen[v] = nil
            if n then
              return wrap('[', items, ']')
            end
            return wrap('{', items, '}')
          end

          local function inspect(v)
            local text = render(v, 0, {})
            if #text > MAX_LENGTH then
              return sub(text, 1, MAX_LENGTH) .. '...'
            end
            return text
          end

          -- ---- deep equality: arrays by length, objects by keys, pm.null by identity, NaN is not equal to NaN ----
          local function deepEqual(a, b, seen)
            if type(a) ~= 'table' or type(b) ~= 'table' then
              return a == b
            elseif rawequal(a, b) then
              return true
            elseif rawequal(a, NULL) or rawequal(b, NULL) then
              return false
            end
            local pairsSeen = seen[a]
            if pairsSeen and pairsSeen[b] then
              return true
            end
            pairsSeen = pairsSeen or {}
            pairsSeen[b] = true
            seen[a] = pairsSeen
            local na, nb = arrayLength(a), arrayLength(b)
            if (na == nil) ~= (nb == nil) then
              return false
            elseif na then
              if na ~= nb then
                return false
              end
              for i = 1, na do
                if not deepEqual(rawget(a, i), rawget(b, i), seen) then
                  return false
                end
              end
              return true
            end
            local count = 0
            for k, v in next, a do
              count = count + 1
              if not deepEqual(v, rawget(b, k), seen) then
                return false
              end
            end
            for _ in next, b do
              count = count - 1
            end
            return count == 0
          end

          -- ---- AssertionError: a table raised with error(e, 0) ----
          local errorMeta = { __tostring = function(e) return e.name .. ': ' .. e.message end }
          local function assertionError(message, actual, expected)
            return setmetatable({ name = 'AssertionError', message = message, actual = actual, expected = expected }, errorMeta)
          end

          -- ---- Assertion ----
          local function fill(template, obj, exp, act)
            template = gsub(template, '#{this}', function() return inspect(obj) end)
            template = gsub(template, '#{exp}', function() return inspect(exp) end)
            return (gsub(template, '#{act}', function() return inspect(act) end))
          end

          -- The one place a result is judged: ok is the un-negated outcome, negation flips it and picks the negated text.
          local function check(flags, ok, positive, negative, exp, act)
            if flags.negate == ok then
              local prefix = flags.message ~= nil and (tostring(flags.message) .. ': ') or ''
              error(assertionError(prefix .. fill(flags.negate and negative or positive, flags.object, exp, act), flags.object, exp), 0)
            end
          end

          local chain = {}
          for _, word in ipairs({ 'to', 'be', 'been', 'is', 'that', 'which', 'has', 'have', 'with', 'at', 'of', 'same', 'but', 'does', 'still', 'also' }) do
            chain[word] = true
          end

          local impl = {}
          local function def(names, fn)
            for _, name in ipairs(names) do
              impl[name] = fn
            end
          end

          local function numArg(value, name, position)
            if type(value) ~= 'number' then
              error(format("bad argument #%d to '%s' (number expected, got %s)", position or 1, name, type(value)), 4)
            end
          end

          def({ 'equal', 'equals', 'eq' }, function(flags, value)
            if flags.deep then
              check(flags, deepEqual(flags.object, value, {}), 'expected #{this} to deeply equal #{exp}', 'expected #{this} to not deeply equal #{exp}', value, flags.object)
            else
              check(flags, flags.object == value, 'expected #{this} to equal #{exp}', 'expected #{this} to not equal #{exp}', value, flags.object)
            end
          end)
          def({ 'eql', 'eqls' }, function(flags, value)
            check(flags, deepEqual(flags.object, value, {}), 'expected #{this} to deeply equal #{exp}', 'expected #{this} to not deeply equal #{exp}', value, flags.object)
          end)

          local function literal(names, test, positive, negative)
            def(names, function(flags)
              check(flags, test(flags.object), positive, negative)
            end)
          end
          literal({ 'ok' }, function(v) return v ~= nil and v ~= false end, 'expected #{this} to be truthy', 'expected #{this} to be falsy')
          literal({ 'is_true' }, function(v) return v == true end, 'expected #{this} to be true', 'expected #{this} to be false')
          literal({ 'is_false' }, function(v) return v == false end, 'expected #{this} to be false', 'expected #{this} to be true')
          literal({ 'is_null' }, function(v) return rawequal(v, NULL) end, 'expected #{this} to be null', 'expected #{this} not to be null')
          literal({ 'is_nil' }, function(v) return v == nil end, 'expected #{this} to be undefined', 'expected #{this} not to be undefined')
          literal({ 'is_nan' }, function(v) return v ~= v end, 'expected #{this} to be NaN', 'expected #{this} not to be NaN')
          literal({ 'exist' }, function(v) return v ~= nil and not rawequal(v, NULL) end, 'expected #{this} to exist', 'expected #{this} to not exist')
          literal(
            { 'empty' },
            function(v)
              if type(v) == 'string' then
                return #v == 0
              elseif type(v) == 'table' and not rawequal(v, NULL) then
                local n = arrayLength(v)
                if n then
                  return n == 0
                end
                return next(v) == nil
              end
              error('.empty was passed ' .. (v == nil and 'undefined' or rawequal(v, NULL) and 'null' or 'non-string primitive ' .. inspect(v)), 4)
            end,
            'expected #{this} to be empty',
            'expected #{this} not to be empty')

          local function typeIs(v, name)
            local kind = type(v)
            if name == 'nil' then
              return v == nil
            elseif name == 'null' then
              return rawequal(v, NULL)
            elseif name == 'integer' or name == 'float' then
              return mtype(v) == name
            elseif name == 'array' then
              return kind == 'table' and arrayLength(v) ~= nil
            elseif name == 'object' then
              return kind == 'table' and not rawequal(v, NULL) and arrayLength(v) == nil
            elseif name == 'table' then
              return kind == 'table' and not rawequal(v, NULL)
            end
            return kind == name
          end
          def({ 'a', 'an' }, function(flags, name)
            local text = lower(tostring(name))
            local article = find(text, '^[aeiou]') and 'an ' or 'a '
            check(flags, typeIs(flags.object, text), 'expected #{this} to be ' .. article .. text, 'expected #{this} not to be ' .. article .. text)
          end)

          local function lengthOf(value)
            if type(value) == 'string' then
              return #value
            elseif type(value) == 'table' and not rawequal(value, NULL) then
              return arrayLength(value) or rawlen(value)
            end
            error(assertionError('expected ' .. inspect(value) .. ' to have a length'), 0)
          end
          local function number(flags)
            local value = flags.object
            if type(value) ~= 'number' then
              local prefix = flags.message ~= nil and (tostring(flags.message) .. ': ') or ''
              error(assertionError(prefix .. 'expected ' .. inspect(value) .. ' to be a number or a date'), 0)
            end
            return value
          end

          local function compare(names, test, word, negated)
            def(names, function(flags, limit)
              numArg(limit, names[1])
              check(flags, test(number(flags), limit), 'expected #{this} to be ' .. word .. ' #{exp}', 'expected #{this} to be ' .. negated .. ' #{exp}', limit)
            end)
          end
          compare({ 'above', 'gt' }, function(v, n) return v > n end, 'above', 'at most')
          compare({ 'least', 'gte' }, function(v, n) return v >= n end, 'at least', 'below')
          compare({ 'below', 'lt' }, function(v, n) return v < n end, 'below', 'at least')
          compare({ 'most', 'lte' }, function(v, n) return v <= n end, 'at most', 'above')

          def({ 'within' }, function(flags, start, finish)
            numArg(start, 'within', 1)
            numArg(finish, 'within', 2)
            local value = number(flags)
            local range = num(start) .. '..' .. num(finish)
            check(flags, value >= start and value <= finish, 'expected #{this} to be within ' .. range, 'expected #{this} to not be within ' .. range)
          end)
          def({ 'close_to', 'closeTo' }, function(flags, expected, delta)
            numArg(expected, 'close_to', 1)
            numArg(delta, 'close_to', 2)
            local value = number(flags)
            local text = num(expected) .. ' +/- ' .. num(delta)
            check(flags, math.abs(value - expected) <= delta, 'expected #{this} to be close to ' .. text, 'expected #{this} not to be close to ' .. text)
          end)
          def({ 'length', 'length_of', 'lengthOf' }, function(flags, expected)
            numArg(expected, 'length')
            local length = lengthOf(flags.object)
            check(flags, length == expected, 'expected #{this} to have a length of #{exp} but got #{act}', 'expected #{this} to not have a length of #{act}', expected, length)
          end)

          local function newAssertion(value, message)
            local flags = { object = value, message = message, negate = false, deep = false }
            local self = {}
            setmetatable(self, {
              __index = function(_, member)
                if chain[member] then
                  return self
                elseif member == 'is_not' then
                  flags.negate = true
                  return self
                elseif member == 'deep' then
                  flags.deep = true
                  return self
                end
                local fn = impl[member]
                if fn == nil then
                  error('pm.expect(...).' .. tostring(member) .. ' is not supported in Yaapic', 2)
                end
                return function(...)
                  fn(flags, ...)
                  return self
                end
              end,
              __newindex = function(_, member) error('pm.expect(...).' .. tostring(member) .. ' cannot be assigned', 2) end,
              __metatable = 'assertion is protected',
            })
            return self
          end

          local expect = setmetatable({
            fail = function(message)
              error(assertionError(message ~= nil and tostring(message) or 'expect.fail()'), 0)
            end,
          }, {
            __call = function(_, value, message) return newAssertion(value, message) end,
            __newindex = function() error('pm.expect is read-only', 2) end,
            __metatable = 'pm.expect is protected',
          })
          define('expect', expect)

          -- ---- pm.test ----
          local recorded = 0

          -- The message of a failed test: an AssertionError reads "AssertionError: ...", any other error loses its "chunk:line: " prefix.
          local function describe(e)
            if type(e) == 'table' then
              local mt = getmetatable(e)
              return type(mt) == 'table' and mt.__tostring ~= nil and tostring(e) or inspect(e)
            end
            local text = tostring(e)
            return (gsub(text, '^[^\n]-:%d+: ', '', 1)), text
          end

          -- shortcut: Lua has no clock, so the duration is 0; upgrade with a host clock op when tests need timings.
          local function record(label, passed, message)
            recorded = recorded + 1
            hostCall('test', { name = label, passed = passed, message = message, ms = 0 })
          end

          local function run(name, fn)
            local label = tostring(name)
            if fn == nil then
              return record(label, true)
            elseif type(fn) ~= 'function' then
              return record(label, false, 'TypeError: ' .. inspect(fn) .. ' is not a function')
            end
            local ok, e = pcall(fn)
            if ok then
              return record(label, true)
            end
            if type(e) == 'string' and (find(e, 'script timed out', 1, true) or find(e, 'script canceled', 1, true)) then
              error(e, 0) -- limits are not test failures
            end
            local message, original = describe(e)
            if original ~= nil and original ~= message then
              hostCall('console', { level = 'error', text = "test '" .. label .. "' failed at " .. original })
            end
            return record(label, false, message)
          end

          local test = setmetatable({
            skip = function(name)
              hostCall('console', { level = 'info', text = 'skipped ' .. tostring(name) })
            end,
            index = function() return recorded end,
          }, {
            __call = function(_, name, fn) run(name, fn) end,
            __newindex = function() error('pm.test is read-only', 2) end,
            __metatable = 'pm.test is protected',
          })
          define('test', test)
        end
        """;
}

namespace Yaapic.Scripting.Lua;

/// <summary>
/// The Lua <c>yaapic</c> prelude, part 4: <c>yaapic.test</c>, <c>AssertionError</c> values and the core <c>yaapic.expect</c> assertions.
/// The Lua twin of <see cref="QuickJs.PmTestPrelude"/>: failure messages are the same text in both languages.
/// </summary>
internal static class LuaPmTestPrelude
{
    /// <summary>The prelude source, evaluated as its own chunk after <see cref="LuaPmRequestResponsePrelude.Source"/>.</summary>
    internal const string Source = """
        do
          local hostCall, define = __hostCall, __pmDefine
          local NULL, json = yaapic.null, yaapic.json
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

          -- The length of a table that is an array (decoded JSON, yaapic.json.array, or keys 1..n), nil for any other table.
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

          -- ---- deep equality: arrays by length, objects by keys, yaapic.null by identity, NaN is not equal to NaN ----
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

          -- ---- collections, strings, functions: the second half of the Chai-style assertions ----
          local function prefixOf(flags)
            return flags.message ~= nil and (tostring(flags.message) .. ': ') or ''
          end
          local function fail(flags, text)
            error(assertionError(prefixOf(flags) .. text, flags.object), 0)
          end
          local function isTable(v)
            return type(v) == 'table' and not rawequal(v, NULL)
          end
          local function typeName(v)
            if v == nil then
              return 'undefined'
            elseif rawequal(v, NULL) then
              return 'null'
            elseif type(v) == 'table' then
              return arrayLength(v) and 'array' or 'object'
            end
            return type(v)
          end
          local function same(flags, a, b)
            if flags.deep then
              return deepEqual(a, b, {})
            end
            return a == b
          end
          local function sortedKeys(t)
            local keys = {}
            for k in next, t do
              keys[#keys + 1] = k
            end
            sort(keys, keyLess)
            return keys
          end
          local function copy(t)
            local result = {}
            for k, v in next, t do
              result[k] = v
            end
            return result
          end

          -- Paths address the JSON document, so "[n]" is a 0-based index (as in JavaScript) whatever the Lua table holds.
          local function parsePath(path)
            local parts, text, i = {}, tostring(path), 1
            while i <= #text do
              local a, b, digits = find(text, '^%[(%d+)%]', i)
              if a then
                parts[#parts + 1] = { index = tonumber(digits) + 1 }
                i = b + 1
              else
                a, b = find(text, '^[^.%[%]]+', i)
                if a then
                  parts[#parts + 1] = { name = sub(text, a, b) }
                  i = b + 1
                else
                  i = i + 1
                end
              end
            end
            return parts
          end
          local function walk(root, parts)
            local current = root
            for _, part in ipairs(parts) do
              if not isTable(current) then
                return false
              end
              local key = part.index or part.name
              if part.name ~= nil and arrayLength(current) and find(part.name, '^%d+$') then
                key = tonumber(part.name) + 1
              end
              current = rawget(current, key)
              if current == nil then
                return false
              end
            end
            return true, current
          end

          local function propertyOf(flags, name, hasValue, value)
            local target = flags.object
            if target == nil or rawequal(target, NULL) then
              fail(flags, 'Target cannot be null or undefined.')
            end
            local found, actual = false, nil
            if flags.nested then
              found, actual = walk(target, parsePath(name))
            elseif type(target) == 'table' then
              actual = rawget(target, name)
              found = actual ~= nil
            end
            local descriptor = (flags.deep and 'deep ' or '') .. (flags.own and 'own ' or '') .. (flags.nested and 'nested ' or '') .. 'property ' .. inspect(name)
            if not flags.negate or not hasValue then
              check(flags, found, 'expected #{this} to have ' .. descriptor, 'expected #{this} to not have ' .. descriptor)
            end
            if hasValue then
              check(flags, found and same(flags, actual, value), 'expected #{this} to have ' .. descriptor .. ' of #{exp}, but got #{act}', 'expected #{this} to not have ' .. descriptor .. ' of #{act}', value, actual)
            end
            flags.object = actual
          end
          def({ 'property' }, function(flags, name, ...)
            propertyOf(flags, name, select('#', ...) > 0, (...))
          end)
          def({ 'own_property', 'ownProperty' }, function(flags, name)
            if flags.object == nil or rawequal(flags.object, NULL) then
              fail(flags, 'Target cannot be null or undefined.')
            end
            local found = type(flags.object) == 'table' and rawget(flags.object, name) ~= nil
            check(flags, found, 'expected #{this} to have own property ' .. inspect(name), 'expected #{this} to not have own property ' .. inspect(name))
          end)

          local function includeOf(flags, val)
            local target = flags.object
            local kind = typeName(target)
            local included
            if kind == 'string' then
              included = find(target, type(val) == 'string' and val or inspect(val), 1, true) ~= nil
            elseif kind == 'array' then
              included = false
              for i = 1, arrayLength(target) do
                if same(flags, rawget(target, i), val) then
                  included = true
                  break
                end
              end
            else
              if target == nil or rawequal(target, NULL) then
                fail(flags, 'object tested must be an array, a map, an object, a set, a string, or a weakset, but ' .. kind .. ' given')
              end
              if not isTable(val) then
                fail(flags, 'the given combination of arguments (' .. kind .. ' and ' .. typeName(val) .. ') is invalid for this assertion. You can use an array, a map, an object, a set, a string, or a weakset instead of a ' .. typeName(val))
              end
              local names = sortedKeys(val)
              local first, failures = nil, 0
              for _, name in ipairs(names) do
                local ok, e = pcall(propertyOf, copy(flags), name, true, rawget(val, name))
                if not ok then
                  if not flags.negate or #names == 1 then
                    error(e, 0)
                  end
                  first = first or e
                  failures = failures + 1
                end
              end
              if flags.negate and #names > 1 and failures == #names then
                error(first, 0)
              end
              return
            end
            check(flags, included, 'expected #{this} to include #{exp}', 'expected #{this} to not include #{exp}', val)
          end

          local function isList(v)
            return isTable(v) and (arrayLength(v) ~= nil or next(v) == nil)
          end
          local function requireList(flags, v)
            if not isList(v) then
              fail(flags, 'expected ' .. inspect(v) .. ' to be an array')
            end
          end
          def({ 'members' }, function(flags, subset)
            requireList(flags, flags.object)
            requireList(flags, subset)
            local superset, count = {}, arrayLength(flags.object) or 0
            for i = 1, count do
              superset[i] = rawget(flags.object, i)
            end
            local wanted = arrayLength(subset) or 0
            local ok = flags.contains or wanted == count
            for index = 1, ok and wanted or 0 do
              local item = rawget(subset, index)
              if flags.ordered then
                ok = same(flags, item, superset[index])
              else
                local at
                for i = 1, #superset do
                  if same(flags, item, superset[i]) then
                    at = i
                    break
                  end
                end
                ok = at ~= nil
                if ok and not flags.contains then
                  table.remove(superset, at)
                end
              end
              if not ok then
                break
              end
            end
            local subject = flags.contains and (flags.ordered and 'an ordered superset' or 'a superset') or flags.ordered and 'ordered members' or 'members'
            local positive = flags.contains and 'expected #{this} to be ' .. subject .. ' of #{exp}' or 'expected #{this} to have the same ' .. subject .. ' as #{exp}'
            local negative = flags.contains and 'expected #{this} to not be ' .. subject .. ' of #{exp}' or 'expected #{this} to not have the same ' .. subject .. ' as #{exp}'
            check(flags, ok, positive, negative, subset, flags.object)
          end)

          def({ 'keys', 'key' }, function(flags, ...)
            local target = flags.object
            local actual = {}
            if isTable(target) then
              local n = arrayLength(target)
              if n then
                for i = 1, n do
                  actual[i] = i
                end
              else
                actual = sortedKeys(target)
              end
            end
            local expected, wanted = { ... }, select('#', ...)
            if wanted == 1 and isTable(expected[1]) then
              local list = expected[1]
              expected = arrayLength(list) and copy(list) or sortedKeys(list)
              wanted = arrayLength(list) or #expected
            end
            if wanted == 0 then
              fail(flags, 'keys required')
            end
            local function present(k)
              for _, a in ipairs(actual) do
                if same(flags, k, a) then
                  return true
                end
              end
              return false
            end
            local ok
            if flags.any then
              ok = false
              for i = 1, wanted do
                ok = ok or present(expected[i])
              end
            else
              ok = flags.contains == true or wanted == #actual
              for i = 1, wanted do
                ok = ok and present(expected[i])
              end
            end
            local text
            if wanted > 1 then
              local rendered = {}
              for i = 1, wanted - 1 do
                rendered[i] = inspect(expected[i])
              end
              text = 'keys ' .. concat(rendered, ', ') .. (flags.any and ', or ' or ', and ') .. inspect(expected[wanted])
            else
              text = 'key ' .. inspect(expected[1])
            end
            text = (flags.contains and 'contain ' or 'have ') .. text
            check(flags, ok, 'expected #{this} to ' .. text, 'expected #{this} to not ' .. text)
          end)

          local function stringSubject(flags)
            if type(flags.object) ~= 'string' then
              fail(flags, 'expected ' .. inspect(flags.object) .. ' to be a string')
            end
            return flags.object
          end
          -- shortcut: match takes a Lua pattern, not a regex; upgrade only if scripts need regular expressions.
          def({ 'match', 'matches' }, function(flags, pattern)
            if type(pattern) ~= 'string' then
              error(format("bad argument #1 to 'match' (string expected, got %s)", type(pattern)), 3)
            end
            local ok, found = pcall(find, stringSubject(flags), pattern)
            if not ok then
              error(tostring(found), 0)
            end
            check(flags, found ~= nil, 'expected #{this} to match #{exp}', 'expected #{this} not to match #{exp}', pattern)
          end)
          def({ 'string' }, function(flags, text)
            local value = stringSubject(flags)
            check(flags, find(value, tostring(text), 1, true) ~= nil, 'expected #{this} to contain #{exp}', 'expected #{this} to not contain #{exp}', text)
          end)
          def({ 'one_of', 'oneOf' }, function(flags, list)
            requireList(flags, list)
            local found = false
            for i = 1, arrayLength(list) or 0 do
              found = found or rawget(list, i) == flags.object
            end
            check(flags, found, 'expected #{this} to be one of #{exp}', 'expected #{this} to not be one of #{exp}', list)
          end)
          def({ 'satisfy', 'satisfies' }, function(flags, matcher)
            local result = matcher(flags.object)
            check(flags, result ~= nil and result ~= false, 'expected #{this} to satisfy ' .. inspect(matcher), 'expected #{this} to not satisfy ' .. inspect(matcher), true, result)
          end)
          def({ 'instance_of', 'instanceOf', 'instanceof' }, function()
            error('yaapic.expect(...).instance_of is not supported in Lua', 3)
          end)

          -- The text of an error raised by a function under test: an AssertionError keeps its message, a string loses "chunk:line: ".
          local function errorMessage(e)
            if type(e) == 'table' and type(rawget(e, 'message')) == 'string' then
              return e.message, tostring(e)
            end
            local plain = gsub(tostring(e), '^[^\n]-:%d+: ', '', 1)
            return plain, plain
          end
          def({ 'throw', 'throws', 'Throw' }, function(flags, matcher)
            local fn = flags.object
            if type(fn) ~= 'function' then
              fail(flags, 'expected ' .. inspect(fn) .. ' to be a function')
            end
            if matcher ~= nil and type(matcher) ~= 'string' then
              error(format("bad argument #1 to 'throw' (string expected, got %s)", type(matcher)), 3)
            end
            local ok, e = pcall(fn)
            if not ok and type(e) == 'string' and (find(e, 'script timed out', 1, true) or find(e, 'script canceled', 1, true)) then
              error(e, 0) -- limits are not assertion failures
            end
            local thrown = not ok
            local message, described
            if thrown then
              message, described = errorMessage(e)
            end
            if matcher == nil or not flags.negate then
              check(flags, thrown, 'expected #{this} to throw an error', 'expected #{this} to not throw an error but #{act} was thrown', nil, described)
            end
            if not thrown or matcher == nil then
              return
            end
            local matches = find(message, matcher, 1, true) ~= nil
            if flags.negate then
              check(flags, matches, '', 'expected #{this} to throw error not including #{exp}', matcher)
            else
              check(flags, matches, 'expected #{this} to throw error including #{exp} but got #{act}', '', matcher, message)
            end
          end)

          local flagWords = { own = true, nested = true, ordered = true, any = true, all = true }
          local includeWords = { include = true, includes = true, contain = true, contains = true }

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
                elseif flagWords[member] then
                  flags[member] = true
                  return self
                elseif includeWords[member] then
                  -- "include" is a call and a chain (include.members): arm the flag, return a callable assertion.
                  flags.contains = true
                  return setmetatable({}, {
                    __call = function(_, v)
                      includeOf(flags, v)
                      return self
                    end,
                    __index = function(_, name) return self[name] end,
                  })
                end
                local fn = impl[member]
                if fn == nil then
                  error('yaapic.expect(...).' .. tostring(member) .. ' is not supported in Yaapic', 2)
                end
                return function(...)
                  fn(flags, ...)
                  return self
                end
              end,
              __newindex = function(_, member) error('yaapic.expect(...).' .. tostring(member) .. ' cannot be assigned', 2) end,
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
            __newindex = function() error('yaapic.expect is read-only', 2) end,
            __metatable = 'yaapic.expect is protected',
          })
          define('expect', expect)

          -- ---- yaapic.response.to: Postman's response assertions, call-style, one fresh chain per access ----
          local rimpl = {}
          local function rdef(names, fn)
            for _, name in ipairs(names) do
              rimpl[name] = fn
            end
          end
          local function rcheck(flags, ok, positive, negative)
            if flags.negate == ok then
              error(assertionError(flags.negate and negative or positive), 0)
            end
          end
          local function rows(dto)
            local headers = dto.headers
            return type(headers) == 'table' and not rawequal(headers, NULL) and headers or {}
          end
          local function contentType(dto)
            for _, h in ipairs(rows(dto)) do
              if lower(h.key) == 'content-type' then
                return lower(h.value)
              end
            end
            return ''
          end
          local function flag(names, test, positive, negative)
            rdef(names, function(flags) rcheck(flags, test(flags.dto), positive(flags.dto), negative) end)
          end
          local function byCode(names, test)
            local name = names[1]
            flag(names, function(dto) return test(dto.code) end, function(dto) return 'expected response to be ' .. name .. ' but got ' .. num(dto.code) end, 'expected response to not be ' .. name)
          end
          byCode({ 'ok' }, function(c) return c == 200 end)
          byCode({ 'success' }, function(c) return c >= 200 and c < 300 end)
          byCode({ 'info' }, function(c) return c >= 100 and c < 200 end)
          byCode({ 'redirection' }, function(c) return c >= 300 and c < 400 end)
          byCode({ 'clientError', 'client_error' }, function(c) return c >= 400 and c < 500 end)
          byCode({ 'serverError', 'server_error' }, function(c) return c >= 500 and c < 600 end)
          byCode({ 'error' }, function(c) return c >= 400 and c < 600 end)
          byCode({ 'accepted' }, function(c) return c == 202 end)
          byCode({ 'badRequest', 'bad_request' }, function(c) return c == 400 end)
          byCode({ 'unauthorized' }, function(c) return c == 401 end)
          byCode({ 'forbidden' }, function(c) return c == 403 end)
          byCode({ 'notFound', 'not_found' }, function(c) return c == 404 end)
          byCode({ 'rateLimited', 'rate_limited' }, function(c) return c == 429 end)
          rdef({ 'withBody', 'with_body' }, function(flags)
            rcheck(flags, #flags.dto.text > 0, 'expected response to have a body', 'expected response to not have a body')
          end)
          local function byType(name, test)
            flag(
              { name },
              function(dto) return test(contentType(dto)) end,
              function(dto)
                local t = contentType(dto)
                return 'expected response to be ' .. name .. ' but got ' .. (t == '' and 'no content type' or 'content type ' .. inspect(t))
              end,
              'expected response to not be ' .. name)
          end
          byType('json', function(t)
            local rest = t:match('^application/(.*)')
            return find(t, '^text/json') ~= nil or rest ~= nil and (find(rest, '^json%f[%W]') ~= nil or find(rest, '^[^;]+%+json%f[%W]') ~= nil)
          end)
          byType('html', function(t) return find(t, '^text/html') ~= nil end)
          byType('text', function(t) return find(t, '^text/') ~= nil end)

          rdef({ 'status' }, function(flags, expected)
            local dto = flags.dto
            if type(expected) == 'string' then
              rcheck(flags, dto.status == expected, 'expected response to have status reason ' .. inspect(expected) .. ' but got ' .. inspect(dto.status), 'expected response to not have status reason ' .. inspect(expected))
            else
              rcheck(flags, dto.code == expected, 'expected response to have status code ' .. inspect(expected) .. ' but got ' .. num(dto.code), 'expected response to not have status code ' .. inspect(expected))
            end
          end)
          rdef({ 'header' }, function(flags, name, ...)
            local wanted, found = lower(tostring(name)), {}
            for _, h in ipairs(rows(flags.dto)) do
              if lower(h.key) == wanted then
                found[#found + 1] = h.value
              end
            end
            if select('#', ...) == 0 then
              rcheck(flags, #found > 0, 'expected response to have header with key ' .. inspect(name), 'expected response to not have header with key ' .. inspect(name))
              return
            end
            local value, hit = (...), false
            for _, v in ipairs(found) do
              hit = hit or v == value
            end
            rcheck(
              flags,
              hit,
              'expected response to have header ' .. inspect(name) .. ' with value ' .. inspect(value) .. (#found > 0 and ' but got ' .. inspect(found[1]) or ''),
              'expected response to not have header ' .. inspect(name) .. ' with value ' .. inspect(value))
          end)
          -- shortcut: a body string is compared exactly, or matched as a Lua pattern; there is no regex object, upgrade with a flag if that is ambiguous.
          rdef({ 'body' }, function(flags, expected)
            local text = flags.dto.text
            if expected == nil then
              rcheck(flags, #text > 0, 'expected response to have a body', 'expected response to not have a body')
              return
            end
            local wanted = tostring(expected)
            local ok, hit = pcall(find, text, wanted)
            rcheck(flags, text == wanted or ok and hit ~= nil, 'expected response to have body ' .. inspect(wanted) .. ' but got ' .. inspect(text), 'expected response to not have body ' .. inspect(wanted))
          end)
          -- Paths use JavaScript-style 0-based indices ("data[0].id") because they address the JSON document, not a Lua table.
          rdef({ 'jsonBody', 'json_body' }, function(flags, path, ...)
            local valid, parsed = pcall(json.decode, flags.dto.text)
            if path == nil then
              rcheck(flags, valid, 'expected response to have a JSON body but the body is not valid JSON', 'expected response to not have a JSON body')
              return
            end
            local found, value = false, nil
            if valid then
              found, value = walk(parsed, parsePath(path))
            end
            if select('#', ...) == 0 then
              rcheck(flags, found, 'expected response JSON body to have path ' .. inspect(path), 'expected response JSON body to not have path ' .. inspect(path))
              return
            end
            local expected = (...)
            rcheck(
              flags,
              found and deepEqual(value, expected, {}),
              'expected response JSON body path ' .. inspect(path) .. ' to equal ' .. inspect(expected) .. (found and ' but got ' .. inspect(value) or ' but the path does not exist'),
              'expected response JSON body path ' .. inspect(path) .. ' to not equal ' .. inspect(expected))
          end)
          rdef({ 'jsonSchema', 'json_schema' }, function()
            error('yaapic.response.to.have.jsonSchema is not supported in Yaapic', 3)
          end)

          local rchain = {}
          for _, word in ipairs({ 'to', 'be', 'been', 'is', 'have', 'has', 'with', 'that', 'a', 'an', 'also' }) do
            rchain[word] = true
          end
          -- Read by yaapic.response.to (part 3), which is built before this prelude runs.
          function __responseTo(dto)
            local flags = { dto = dto, negate = false }
            local self = {}
            setmetatable(self, {
              __index = function(_, member)
                if rchain[member] then
                  return self
                elseif member == 'is_not' then
                  flags.negate = true
                  return self
                end
                local fn = rimpl[member]
                if fn == nil then
                  error('yaapic.response.to.' .. tostring(member) .. ' is not supported in Yaapic', 2)
                end
                return function(...)
                  fn(flags, ...)
                  return self
                end
              end,
              __newindex = function(_, member) error('yaapic.response.to.' .. tostring(member) .. ' cannot be assigned', 2) end,
              __metatable = 'yaapic.response.to is protected',
            })
            return self
          end

          -- ---- yaapic.test ----
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
            __newindex = function() error('yaapic.test is read-only', 2) end,
            __metatable = 'yaapic.test is protected',
          })
          define('test', test)
        end
        """;
}

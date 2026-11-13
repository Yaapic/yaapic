namespace Yaapic.Scripting.Lua;

/// <summary>
/// The Lua <c>pm</c> prelude, part 2: <c>pm.info</c>, the variable scopes, the unsupported stubs and the read-only
/// <c>pm</c> proxy. Later parts add their members through <c>__pmDefine</c>.
/// </summary>
internal static class LuaPmVariablesPrelude
{
    /// <summary>The prelude source, evaluated as its own chunk after <see cref="LuaPmJsonPrelude.Source"/>.</summary>
    internal const string Source = """
        do
          local hostCall = __hostCall
          local target = pm
          local encode, NULL = target.json.encode, target.null
          local type, error, ipairs, next, setmetatable, tostring, rawequal = type, error, ipairs, next, setmetatable, tostring, rawequal
          local format, mtype, huge, floor = string.format, math.type, math.huge, math.floor

          -- Raised at level 3: the script line that called the closure.
          local function checkString(value, method)
            if type(value) ~= 'string' then
              error(format("bad argument #1 to '%s' (string expected, got %s)", method, type(value)), 3)
            end
          end

          -- Integral floats print without ".0" like the JavaScript engine does; shortcut: no exponent form below 2^53.
          local function coerce(value)
            local kind = type(value)
            if kind == 'string' then
              return value
            elseif kind == 'number' then
              if mtype(value) == 'integer' then
                return format('%d', value)
              elseif value ~= value then
                return 'NaN'
              elseif value == huge then
                return 'Infinity'
              elseif value == -huge then
                return '-Infinity'
              elseif value == floor(value) and value > -9007199254740992 and value < 9007199254740992 then
                return format('%d', value)
              end
              return tostring(value)
            elseif kind == 'boolean' then
              return tostring(value)
            elseif value == nil or rawequal(value, NULL) then
              return ''
            elseif kind == 'table' then
              return encode(value)
            end
            error('cannot store value of type ' .. kind, 3)
          end

          -- "scope" is read, "writeScope" is written and cleared ("local" for the layered view).
          local function newScope(scope, writeScope, layered)
            local s = {}
            function s.get(name)
              checkString(name, 'get')
              local value = hostCall('var.get', { scope = scope, name = name })
              if rawequal(value, NULL) then
                return nil
              end
              return value
            end
            function s.has(name)
              checkString(name, 'has')
              return hostCall('var.has', { scope = scope, name = name })
            end
            function s.set(name, value)
              checkString(name, 'set')
              hostCall('var.set', { scope = writeScope, name = name, value = coerce(value) })
            end
            function s.unset(name)
              checkString(name, 'unset')
              hostCall('var.unset', { scope = writeScope, name = name })
            end
            function s.clear()
              hostCall('var.clear', { scope = writeScope })
            end
            function s.to_object()
              local out = {}
              for _, row in ipairs(hostCall('var.list', { scope = scope })) do
                out[row.key] = row.value
              end
              return out
            end
            s.toObject = s.to_object
            if layered then
              function s.replace_in(text)
                checkString(text, 'replace_in')
                return hostCall('var.replace', { scope = 'layered', value = text })
              end
              s.replaceIn = s.replace_in
            end
            return s
          end

          -- A stub raises on any use: reading, writing or calling it.
          local function unsupported(path)
            local function fail(_, member)
              error(path .. (type(member) == 'string' and '.' .. member or '') .. ' is not supported in Yaapic', 2)
            end
            return setmetatable({}, { __index = fail, __newindex = fail, __call = function() fail() end })
          end

          local info = hostCall('info.get')
          target.info = {
            event_name = info.eventName, eventName = info.eventName,
            request_name = info.requestName, requestName = info.requestName,
            request_id = info.requestId, requestId = info.requestId,
            iteration = 0,
            iteration_count = 1, iterationCount = 1,
          }
          target.variables = newScope('layered', 'local', true)
          local environment = newScope('environment', 'environment', false)
          -- pm.environment.name is not exposed in v1.
          target.environment = setmetatable(environment, {
            __index = function(_, member)
              if member == 'name' then
                error('pm.environment.name is not supported in Yaapic', 2)
              end
            end,
          })
          target.collectionVariables = newScope('collection', 'collection', false)
          target.collection_variables = target.collectionVariables
          target.globals = newScope('globals', 'globals', false)
          for _, name in ipairs({ 'execution', 'iterationData', 'vault', 'visualizer', 'require' }) do
            target[name] = unsupported('pm.' .. name)
          end
          target.iteration_data = target.iterationData

          -- pm is a read-only proxy; a script that rawsets _G.pm only hurts itself.
          pm = setmetatable({}, {
            __index = target,
            __newindex = function() error('pm is read-only', 2) end,
            __pairs = function() return next, target end,
            __metatable = 'pm is protected',
          })
          yaapic = pm -- same proxy: the native name, pm stays for Postman scripts
          function __pmDefine(name, value)
            target[name] = value
          end
        end
        """;
}

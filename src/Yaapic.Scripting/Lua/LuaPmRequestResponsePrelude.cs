namespace Yaapic.Scripting.Lua;

/// <summary>
/// The Lua <c>pm</c> prelude, part 3: <c>pm.request</c> (read and, in pre-request, write through setter functions) and
/// <c>pm.response</c> (post-response). The Lua twin of the JavaScript request/response prelude.
/// </summary>
internal static class LuaPmRequestResponsePrelude
{
    /// <summary>The prelude source, evaluated as its own chunk after <see cref="LuaPmVariablesPrelude.Source"/>.</summary>
    internal const string Source = """
        do
          local hostCall, define = __hostCall, __pmDefine
          local NULL, json = pm.null, pm.json
          local type, error, ipairs, setmetatable, tostring, tonumber, rawequal, pcall = type, error, ipairs, setmetatable, tostring, tonumber, rawequal, pcall
          local find, sub, match, gsub, format, lower, upper, char, byte = string.find, string.sub, string.match, string.gsub, string.format, string.lower, string.upper, string.char, string.byte
          local concat = table.concat
          local mtype, floor, utf8len = math.type, math.floor, utf8.len

          -- Raised at level 3: the script line that called the closure.
          local function checkString(value, method)
            if type(value) ~= 'string' then
              error(format("bad argument #1 to '%s' (string expected, got %s)", method, type(value)), 3)
            end
          end

          local function text(value)
            local kind = type(value)
            if kind == 'string' then
              return value
            elseif kind == 'number' then
              if mtype(value) == 'float' and value == floor(value) and value > -9007199254740992 and value < 9007199254740992 then
                return format('%d', value)
              end
              return tostring(value)
            elseif kind == 'boolean' then
              return tostring(value)
            end
            error('cannot store value of type ' .. kind, 4)
          end

          local function unsupported(path)
            local function fail(_, member)
              error(path .. (type(member) == 'string' and '.' .. member or '') .. ' is not supported in Yaapic', 2)
            end
            return setmetatable({}, { __index = fail, __newindex = fail, __call = function() fail() end })
          end

          local function toItem(row)
            local item = { key = row.key, value = row.value }
            if row.enabled == false then
              item.disabled = true
            end
            return item
          end
          local function toRow(item)
            return { key = item.key, value = item.value, enabled = item.disabled ~= true }
          end
          local function checkItem(label, item)
            if type(item) ~= 'table' or type(item.key) ~= 'string' then
              error(label .. ': item must be a table with a string key', 3)
            end
            local value = item.value
            local out = { key = item.key, value = (value == nil or rawequal(value, NULL)) and '' or text(value) }
            if item.disabled == true then
              out.disabled = true
            end
            return out
          end

          -- Every call reads the host again (no cached copy); a list without a store is read-only.
          local function newList(label, load, store, fold)
            local list = {}
            local function same(a, b)
              if fold then
                return lower(a) == lower(b)
              end
              return a == b
            end
            local function locate(items, key)
              for i, item in ipairs(items) do
                if not item.disabled and same(item.key, key) then
                  return i
                end
              end
            end
            local function writable()
              if store == nil then
                error(label .. ' is read-only', 3)
              end
            end
            function list.get(key)
              checkString(key, 'get')
              local items = load()
              local index = locate(items, key)
              return index and items[index].value or nil
            end
            function list.has(key)
              checkString(key, 'has')
              return locate(load(), key) ~= nil
            end
            function list.add(item)
              writable()
              local items = load()
              items[#items + 1] = checkItem(label .. '.add', item)
              store(items)
            end
            function list.upsert(item)
              writable()
              local items = load()
              local next_ = checkItem(label .. '.upsert', item)
              items[locate(items, next_.key) or #items + 1] = next_
              store(items)
            end
            function list.remove(key)
              writable()
              checkString(key, 'remove')
              local kept = {}
              for _, item in ipairs(load()) do
                if not same(item.key, key) then
                  kept[#kept + 1] = item
                end
              end
              store(kept)
            end
            function list.each(fn)
              for _, item in ipairs(load()) do
                fn(item)
              end
            end
            function list.count()
              return #load()
            end
            function list.all()
              return json.array(load())
            end
            -- 1-based.
            function list.idx(index)
              return load()[index]
            end
            function list.to_object()
              local out = {}
              for _, item in ipairs(load()) do
                if not item.disabled and out[item.key] == nil then
                  out[item.key] = item.value
                end
              end
              return out
            end
            list.toObject = list.to_object
            return list
          end

          local function mutate(change)
            local dto = hostCall('req.get')
            change(dto)
            hostCall('req.set', dto)
          end

          local function rows(list)
            local out = {}
            for i, item in ipairs(list) do
              out[i] = toRow(item)
            end
            return json.array(out)
          end
          local function items(dtoRows)
            local out = {}
            if dtoRows ~= nil and not rawequal(dtoRows, NULL) then
              for i, row in ipairs(dtoRows) do
                out[i] = toItem(row)
              end
            end
            return out
          end

          local function readOnly(path, member, setter)
            local hint = setter and ('; use ' .. path .. '.' .. setter .. '()') or ''
            error(path .. '.' .. tostring(member) .. ' cannot be assigned in Lua' .. hint, 3)
          end

          -- ---- URL: a small splitter, variables stay untouched (split on the first '#', '?' and '://').
          local function decode(value)
            if not find(value, '%', 1, true) then
              return value
            end
            for position in value:gmatch('()%%') do
              if not match(value, '^%x%x', position + 1) then
                return value
              end
            end
            local decoded = gsub(value, '%%(%x%x)', function(hex) return char(tonumber(hex, 16)) end)
            return utf8len(decoded) and decoded or value
          end
          local function encodeComponent(value)
            return (gsub(value, "[^%w%-_%.!~%*'%(%)]", function(c) return format('%%%02X', byte(c)) end))
          end
          -- Escapes like the application does: {{name}} tokens stay readable.
          local function escape(value)
            local out, position = {}, 1
            while true do
              local s, e = find(value, '{{[^{}]*}}', position)
              if not s then
                break
              end
              out[#out + 1] = encodeComponent(sub(value, position, s - 1))
              out[#out + 1] = sub(value, s, e)
              position = e + 1
            end
            out[#out + 1] = encodeComponent(sub(value, position))
            return concat(out)
          end
          local function split(value, separator)
            local out, position = {}, 1
            while true do
              local s = find(value, separator, position, true)
              if not s then
                out[#out + 1] = sub(value, position)
                return out
              end
              out[#out + 1] = sub(value, position, s - 1)
              position = s + #separator
            end
          end
          local function readQuery(raw)
            local out = {}
            for _, segment in ipairs(split(raw, '&')) do
              if segment ~= '' then
                local eq = find(segment, '=', 1, true)
                if eq then
                  out[#out + 1] = { key = decode(sub(segment, 1, eq - 1)), value = decode(sub(segment, eq + 1)) }
                else
                  out[#out + 1] = { key = decode(segment), value = '' }
                end
              end
            end
            return out
          end
          local function writeQuery(list)
            local out = {}
            for _, item in ipairs(list) do
              if not item.disabled and item.key ~= '' then
                out[#out + 1] = escape(item.key) .. '=' .. escape(item.value)
              end
            end
            return concat(out, '&')
          end

          local function parse(value)
            local rest, hash, query = value, '', ''
            local at = find(rest, '#', 1, true)
            if at then
              hash, rest = sub(rest, at + 1), sub(rest, 1, at - 1)
            end
            at = find(rest, '?', 1, true)
            if at then
              query, rest = sub(rest, at + 1), sub(rest, 1, at - 1)
            end
            local protocol = ''
            local s, e = find(rest, '://', 1, true)
            if s then
              protocol, rest = sub(rest, 1, s - 1), sub(rest, e + 1)
            end
            local slash = find(rest, '/', 1, true)
            local path = slash and sub(rest, slash) or ''
            local authority = slash and sub(rest, 1, slash - 1) or rest
            local auth = ''
            local before, after = match(authority, '^(.*)@(.*)$')
            if before then
              auth, authority = before, after
            end
            local host, port = match(authority, '^(.*):(%d+)$')
            if not host then
              host, port = authority, ''
            end
            return { protocol = protocol, auth = auth, host = host, port = port, path = path, query = query, hash = hash }
          end

          local function flushUrl(value)
            mutate(function(dto) dto.url = value end)
          end
          local function currentUrl()
            return hostCall('req.get').url
          end

          local queryList = newList(
            'pm.request.url.query',
            function() return readQuery(parse(currentUrl()).query) end,
            function(list)
              local p = parse(currentUrl())
              local query = writeQuery(list)
              flushUrl((p.protocol == '' and '' or p.protocol .. '://')
                .. (p.auth == '' and '' or p.auth .. '@')
                .. p.host
                .. (p.port == '' and '' or ':' .. p.port)
                .. p.path
                .. (query == '' and '' or '?' .. query)
                .. (p.hash == '' and '' or '#' .. p.hash))
            end,
            false)

          local urlMethods = {}
          function urlMethods.to_string() return currentUrl() end
          function urlMethods.update(value)
            checkString(value, 'update')
            flushUrl(value)
          end
          function urlMethods.get_host() return parse(currentUrl()).host end
          function urlMethods.get_path()
            local path = parse(currentUrl()).path
            return path == '' and '/' or path
          end
          function urlMethods.get_query_string() return parse(currentUrl()).query end
          function urlMethods.get_path_with_query()
            local query = parse(currentUrl()).query
            return urlMethods.get_path() .. (query == '' and '' or '?' .. query)
          end
          urlMethods.toString = urlMethods.to_string
          urlMethods.getHost = urlMethods.get_host
          urlMethods.getPath = urlMethods.get_path
          urlMethods.getQueryString = urlMethods.get_query_string
          urlMethods.getPathWithQuery = urlMethods.get_path_with_query

          local url = setmetatable({}, {
            __index = function(_, member)
              if urlMethods[member] then
                return urlMethods[member]
              elseif member == 'protocol' then
                return parse(currentUrl()).protocol
              elseif member == 'host' then
                local host = parse(currentUrl()).host
                return host == '' and {} or split(host, '.')
              elseif member == 'port' then
                return parse(currentUrl()).port
              elseif member == 'path' then
                local segments = {}
                for segment in parse(currentUrl()).path:gmatch('[^/]+') do
                  segments[#segments + 1] = segment
                end
                return segments
              elseif member == 'hash' then
                return parse(currentUrl()).hash
              elseif member == 'query' then
                return queryList
              end
            end,
            __newindex = function(_, member) readOnly('pm.request.url', member, 'update') end,
            __tostring = currentUrl,
            __metatable = 'pm.request.url is protected',
          })

          -- ---- pm.request.body
          local settableModes = { none = true, raw = true, urlencoded = true }
          local urlencoded = newList(
            'pm.request.body.urlencoded',
            function() return items(hostCall('req.get').urlEncoded) end,
            function(list) mutate(function(dto) dto.urlEncoded = rows(list) end) end,
            false)
          local formdata = newList(
            'pm.request.body.formdata',
            function() return items(hostCall('req.get').formData) end,
            nil,
            false)

          local bodyMethods = {}
          function bodyMethods.get_mode() return hostCall('req.get').bodyMode end
          function bodyMethods.set_mode(mode)
            if mode == 'formdata' or mode == 'binary' then
              error("pm.request.body mode '" .. mode .. "' cannot be set from a script", 2)
            end
            if not settableModes[mode] then
              error("pm.request.body mode '" .. tostring(mode) .. "' is not valid", 2)
            end
            mutate(function(dto) dto.bodyMode = mode end)
          end
          function bodyMethods.get_raw() return hostCall('req.get').bodyText end
          function bodyMethods.set_raw(value)
            if type(value) ~= 'string' then
              error('pm.request.body.raw must be a string', 2)
            end
            mutate(function(dto) dto.bodyText = value end)
          end
          function bodyMethods.to_string() return hostCall('req.get').bodyText end
          bodyMethods.getMode, bodyMethods.setMode = bodyMethods.get_mode, bodyMethods.set_mode
          bodyMethods.getRaw, bodyMethods.setRaw = bodyMethods.get_raw, bodyMethods.set_raw
          bodyMethods.toString = bodyMethods.to_string

          local body = setmetatable({}, {
            __index = function(_, member)
              if bodyMethods[member] then
                return bodyMethods[member]
              elseif member == 'mode' then
                return bodyMethods.get_mode()
              elseif member == 'raw' then
                return bodyMethods.get_raw()
              elseif member == 'urlencoded' then
                return urlencoded
              elseif member == 'formdata' then
                return formdata
              end
            end,
            __newindex = function(_, member)
              readOnly('pm.request.body', member, (member == 'mode' or member == 'raw') and 'set_' .. member or nil)
            end,
            __tostring = bodyMethods.to_string,
            __metatable = 'pm.request.body is protected',
          })

          -- ---- pm.request
          local info = hostCall('info.get')
          local headers = newList(
            'pm.request.headers',
            function() return items(hostCall('req.get').headers) end,
            function(list) mutate(function(dto) dto.headers = rows(list) end) end,
            true)

          local requestMethods = {}
          function requestMethods.get_method() return hostCall('req.get').method end
          function requestMethods.set_method(method)
            if type(method) ~= 'string' then
              error('pm.request.method must be a string', 2)
            end
            mutate(function(dto) dto.method = upper(method) end)
          end
          function requestMethods.set_url(value)
            if type(value) ~= 'string' then
              error('pm.request.url must be a string', 2)
            end
            flushUrl(value)
          end
          requestMethods.getMethod, requestMethods.setMethod, requestMethods.setUrl =
            requestMethods.get_method, requestMethods.set_method, requestMethods.set_url

          local stubs = { auth = unsupported('pm.request.auth'), certificate = unsupported('pm.request.certificate') }
          local request = setmetatable({}, {
            __index = function(_, member)
              if requestMethods[member] then
                return requestMethods[member]
              elseif member == 'id' then
                return info.requestId
              elseif member == 'name' then
                return info.requestName
              elseif member == 'method' then
                return requestMethods.get_method()
              elseif member == 'url' then
                return url
              elseif member == 'headers' then
                return headers
              elseif member == 'body' then
                return body
              end
              return stubs[member]
            end,
            __newindex = function(_, member)
              readOnly('pm.request', member, (member == 'method' and 'set_method') or (member == 'url' and 'set_url') or nil)
            end,
            __metatable = 'pm.request is protected',
          })
          define('request', request)

          -- ---- pm.response (post-response only; the snapshot is immutable, so it is built once)
          local response
          local function buildResponse(dto)
            local result = {
              code = dto.code,
              status = dto.status,
              response_time = dto.time, responseTime = dto.time,
              response_size = dto.size, responseSize = dto.size,
              headers = newList(
                'pm.response.headers',
                function() return items(dto.headers) end,
                nil,
                true),
            }
            function result.text() return dto.text end
            function result.json()
              local ok, value = pcall(json.decode, dto.text)
              if not ok then
                error('Response body is not valid JSON: ' .. tostring(value), 2)
              end
              return value
            end
            function result.reason() return dto.status end
            function result.size() return { body = dto.size, header = 0, total = dto.size } end
            -- pm.response.to is a fresh assertion chain per access, defined by the test prelude.
            return setmetatable(result, { __index = function(_, member) if member == 'to' then return __responseTo(dto) end end })
          end
          local function current()
            if response == nil then
              local dto = hostCall('res.get')
              if rawequal(dto, NULL) then
                error('pm.response is only available in post-response scripts', 3)
              end
              response = buildResponse(dto)
            end
            return response
          end
          define('response', setmetatable({}, {
            __index = function(_, member) return current()[member] end,
            __newindex = function(_, member) readOnly('pm.response', member) end,
            __metatable = 'pm.response is protected',
          }))
        end
        """;
}

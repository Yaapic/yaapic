namespace Yaapic.Scripting.Lua;

/// <summary>
/// The Lua <c>pm</c> prelude, part 5: <c>pm.sendRequest</c> (synchronous, returns <c>res, err</c>) and <c>pm.cookies</c>
/// (and the cookie list behind <c>res.cookies</c>). The Lua twin of the JavaScript send prelude.
/// </summary>
internal static class LuaPmSendPrelude
{
    /// <summary>The prelude source, evaluated as its own chunk after <see cref="LuaPmTestPrelude.Source"/>.</summary>
    internal const string Source = """
        do
          local hostCall, define = __hostCall, __pmDefine
          local NULL, json = pm.null, pm.json
          local type, error, ipairs, pairs, tostring, rawequal, pcall = type, error, ipairs, pairs, tostring, rawequal, pcall
          local sort, upper = table.sort, string.upper

          local function present(value)
            if value == nil or rawequal(value, NULL) then
              return nil
            end
            return value
          end

          -- Read-only cookie access over a list of host cookies (the current response, or a sendRequest response).
          local function cookieList(cookies)
            cookies = present(cookies) or {}
            local list = {}
            local function find(name)
              for _, cookie in ipairs(cookies) do
                if cookie.name == name then
                  return cookie
                end
              end
            end
            function list.get(name)
              local cookie = find(name)
              return cookie and cookie.value or nil
            end
            function list.has(name)
              return find(name) ~= nil
            end
            function list.to_object()
              local out = {}
              for _, cookie in ipairs(cookies) do
                if out[cookie.name] == nil then
                  out[cookie.name] = cookie.value
                end
              end
              return out
            end
            function list.count()
              return #cookies
            end
            function list.all()
              local out = {}
              for i, c in ipairs(cookies) do
                local httpOnly = c.httpOnly == true
                out[i] = {
                  name = c.name,
                  value = c.value,
                  domain = present(c.domain),
                  path = present(c.path),
                  expires = present(c.expires),
                  http_only = httpOnly,
                  httpOnly = httpOnly,
                  secure = c.secure == true,
                }
              end
              return json.array(out)
            end
            function list.each(fn)
              for _, cookie in ipairs(list.all()) do
                fn(cookie)
              end
            end
            function list.jar()
              error('The cookie jar is not available in Yaapic yet', 2)
            end
            list.toObject = list.to_object
            return list
          end
          __cookieList = cookieList

          -- pm.cookies reads the host on every call (empty in pre-request).
          local live = {}
          for _, name in ipairs({ 'get', 'has', 'to_object', 'toObject', 'count', 'all', 'each' }) do
            live[name] = function(...) return cookieList(hostCall('cookies'))[name](...) end
          end
          function live.jar()
            error('The cookie jar is not available in Yaapic yet', 2)
          end
          define('cookies', live)

          local function rows(headers)
            local out = {}
            headers = present(headers)
            if headers == nil then
              return json.array(out)
            end
            if headers[1] ~= nil then
              for i, h in ipairs(headers) do
                out[i] = { key = tostring(h.key), value = tostring(present(h.value) or ''), enabled = h.disabled ~= true }
              end
            else
              local keys = {}
              for key in pairs(headers) do
                keys[#keys + 1] = key
              end
              sort(keys)
              for i, key in ipairs(keys) do
                out[i] = { key = key, value = tostring(headers[key]), enabled = true }
              end
            end
            return json.array(out)
          end

          local function toDto(input)
            local options = type(input) == 'string' and { url = input } or input
            if type(options) ~= 'table' or present(options.url) == nil then
              error('pm.sendRequest: expected a URL or an options table with a url', 3)
            end
            local dto = {
              method = upper(tostring(options.method or 'GET')),
              url = tostring(options.url),
              headers = rows(options.header or options.headers),
              bodyMode = 'none',
              bodyText = '',
              bodyContentType = '',
              urlEncoded = json.array({}),
            }
            local body = present(options.body)
            if body ~= nil then
              local mode = body.mode or 'raw'
              if mode == 'raw' then
                dto.bodyMode = 'raw'
                dto.bodyText = tostring(body.raw or '')
                dto.bodyContentType = tostring(body.options and body.options.raw and body.options.raw.language or '')
              elseif mode == 'urlencoded' then
                dto.bodyMode = 'urlencoded'
                dto.urlEncoded = rows(body.urlencoded)
              else
                error("pm.sendRequest body mode '" .. tostring(mode) .. "' is not supported", 3)
              end
            end
            return dto
          end

          -- Blocking: returns res, err (exactly one is nil). A callback runs first, as callback(err, res); its errors propagate.
          local function sendRequest(input, callback)
            if callback ~= nil and type(callback) ~= 'function' then
              error('pm.sendRequest: callback must be a function', 2)
            end
            local dto = toDto(input)
            local ok, reply = pcall(hostCall, 'send', dto)
            if not ok then
              reply = { error = tostring(reply) }
            end
            local res, err
            if present(reply.response) ~= nil then
              res = __buildResponse(reply.response)
            else
              err = present(reply.error) or 'Request failed'
            end
            if callback ~= nil then
              callback(err, res)
            end
            return res, err
          end
          define('send_request', sendRequest)
          define('sendRequest', sendRequest)
        end
        """;
}

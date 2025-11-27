-- -*- coding: utf-8 -*-

local export = {}

function export.startsWith(str, prefix)
    if type(str) ~= "string" or type(prefix) ~= "string" then
        return false
    end
    if #prefix > #str then
        return false
    end
    for i = 1, #prefix do
        if string.byte(str, i) ~= string.byte(prefix, i) then
            return false
        end
    end
    return true
end

function export.endsWith(str, suffix)
    if type(str) ~= "string" or type(suffix) ~= "string" then
        return false
    end
    local strLen = #str
    local suffixLen = #suffix
    if suffixLen == 0 then -- 空后缀始终匹配
        return true
    end
    if suffixLen > strLen then -- 后缀比主字符串长，必不匹配
        return false
    end

    local startIndex = strLen - suffixLen + 1
    for i = 1, suffixLen do
        local idx = startIndex + i - 1
        if string.byte(str, idx) ~= string.byte(suffix, i) then
            return false
        end
    end

    return true
end

return export

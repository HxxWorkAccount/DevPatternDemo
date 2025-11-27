local export = {}

--/*====------------ Base ------------====*/--

-- 万物基类
local object = {}
export.object = object

object.__mro__ = { object }

function object:__init__()
end

function object:__tostring()
    return string.format("<object instance at %s>", tostring(self):gsub("table: ", ""))
end

--/*====------------ MRO 继承 ------------====*/--

local function searchSymbol(cls, symbol, startIndex)
    local mro = cls.__mro__
    startIndex = startIndex or 1
    for i = startIndex, #mro do
        local value = rawget(mro[i], symbol)
        if value ~= nil then
            return value
        end
    end
    return nil
end

-- 创建 MRO 线性化列表
local function createMro(cls, parents)
    local mro = { cls }
    if not parents or #parents == 0 then
        return mro
    end

    -- 准备合并列表：[p1.__mro__, p2.__mro__, ..., parents]
    local lists = {}
    for _, parent in ipairs(parents) do
        local parentMro = {}
        if parent.__mro__ then
            for _, c in ipairs(parent.__mro__) do
                table.insert(parentMro, c)
            end
            table.insert(lists, parentMro)
        end
    end
    table.insert(lists, parents)

    -- C3 线性化算法
    while #lists > 0 do
        local head = nil

        -- 尝试找到一个合法的 head
        for i, list in ipairs(lists) do
            local candidate = list[1]
            if candidate then
                local valid = true
                -- 检查 candidate 是否在其他列表中（除了头部）
                for j, otherList in ipairs(lists) do
                    if i ~= j then
                        for k = 2, #otherList do
                            if otherList[k] == candidate then
                                valid = false
                                break
                            end
                        end
                    end
                    if not valid then break end
                end
                if valid then
                    head = candidate
                    break
                end
            end
        end
        
        if not head then
            error("Cannot create a consistent method resolution order (MRO)")
        end

        table.insert(mro, head)

        -- 从所有列表中移除 head
        local i = 1
        while i <= #lists do
            local list = lists[i]
            if list[1] == head then
                table.remove(list, 1)
            end
            if #list == 0 then -- 如果列表为空，移除它
                table.remove(lists, i)
            else
                i = i + 1
            end
        end
    end

    return mro
end

--/*====------------ Metaclass ------------====*/--

local mt = { }

function mt.__index(cls, symbol)
    return searchSymbol(cls, symbol) -- Lua 类核心操作：将 cls 元表的 __index 指向 cls 本身
end

function mt.__tostring(cls)
    return string.format("<class '%s'>", cls.__name__ or "unknown")
end

function mt.__call(cls, ...)
    local instance = {}
    setmetatable(instance, { __index = cls })
    instance.__class__ = cls
    if instance.__init__ then
        instance:__init__(...)
    end
    return instance
end

--/*====------------ Class 接口 ------------====*/--

function export.class(...)
    local parents = {...}
    if #parents == 0 then
        parents = { object }
    end
    local cls = { }
    cls.__mro__ = createMro(cls, parents)
    setmetatable(cls, mt)
    return cls
end

function export.getbase(cls, instance, symbol)
    local startIndex = 0
    local mro = instance.__class__.__mro__
    for i, c in ipairs(mro) do
        if c == cls then
            startIndex = i + 1
            break
        end
    end
    if startIndex == 0 then
        return nil
    else
        return searchSymbol(instance.__class__, symbol, startIndex)
    end
end

function export.initbase(cls, instance, ...)
    local init = export.getbase(cls, instance, "__init__") -- 总会找到，因为 object 有 __init__
    init(instance, ...)
end

function export.callbase(cls, instance, methodName, ...)
    local method = export.getbase(cls, instance, methodName)
    if method then
        return method(instance, ...)
    else
        error("Method not found in base classes: " .. methodName)
    end
end

function export.callable(instance)
    if type(instance) == "function" then return true end
    local mt = getmetatable(instance)
    if mt and mt.__call then return true end
    return false
end

function export.isinstance(instance, cls)
    if type(instance) ~= "table" then return false end
    local c = instance.__class__
    if not c or not c.__mro__ then return false end
    for _, p in ipairs(c.__mro__) do
        if p == cls then return true end
    end
    return false
end

function export.issubclass(sub, sup)
    if type(sub) ~= "table" or type(sup) ~= "table" then return false end
    if not sub.__mro__ then return false end
    for _, p in ipairs(sub.__mro__) do
        if p == sup then return true end
    end
    return false
end

return export

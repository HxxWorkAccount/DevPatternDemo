-- -*- coding: utf-8 -*-
local export = {}
local StringUtils = require("Lua.Universal.Utils.StringUtils")

--/*====------------ 缓存旧的模块 ------------====*/--
-- 记录因重载而被替换的模块，这样下次再发生重载也可以继续刷新它们

local OLD_MODULES_CACHE = {}
setmetatable(OLD_MODULES_CACHE, { __mode = "k" }) -- 弱表

--/*====------------ 一些常量和判断方法 ------------====*/--

-- 该表内的字段，不论是否是方法，都直接替换
local REPLACE_WHEN_MATCH = {
    ["__index"] = true,
    ["__newindex"] = true,
}

-- （可扩展）模块重载剔除
function export.canReload(moduleName)
    local module = package.loaded[moduleName]
    if type(module) ~= "table" then
        return false
    elseif module == export then -- 排除掉重载模块自身
        return false
    end
    return true
end

-- Lua 中表实例有两种用途：存储数据、表示结构（如：类），这里我们只想更新结构
-- 如果表实例用作数据，递归更新是有很大风险的，因为其中的数据可能指向外部模块的对象，等于变相修改了其他模块的状态
-- （可扩展）因此，这里通过一些“约定”来判断表的实际用途。如果是结构，则递归更新；如果是数据，则跳过
-- 同理，一般数据（如：浮点、字符串）也可以分为静态配置和全局数据，如果是静态配置我们就希望更新
local DATA_TABLE_NAME_PREFIXES = {
    "ms_", "s_", "m_",
}
local function isData(key, val)
    if type(val) == "function" then
        return false
    end
    for _, prefix in ipairs(DATA_TABLE_NAME_PREFIXES) do
        if StringUtils.startsWith(key, prefix) then
            return true
        end
    end
    return false
end

-- 全部重载时，仅重载项目的模块，不重载内置或第三方模块
local PROJECT_MODULE_PREFIXES = {
    "Lua.",
    "Dev.Lua.",
    "Tests.Lua.",
}
local function isProjectModule(moduleName)
    for _, prefix in ipairs(PROJECT_MODULE_PREFIXES) do
        if StringUtils.startsWith(moduleName, prefix) then
            return true
        end
    end
end

--/*====------------ 热重载接口 ------------====*/--

-- 递归更新工具函数，特点：
-- - 不删除旧表已有（但新表没有）的成员
-- - 会添加新成员（这里不仅限于函数）
-- - 不同类型会直接替换
-- - 会根据名字判断其用途，如果是数据则会跳过更新（保持状态）
local function updateTable(oldTable, newTable)
    if type(oldTable) ~= "table" or type(newTable) ~= "table" then return end
    local visited = {}
    local function update(old, new)
        if visited[old] then return end
        visited[old] = true

        -- 遍历新表
        for k, v in next, new do -- 不遍历元表
            if REPLACE_WHEN_MATCH[k] then -- 特殊字段强制覆盖（可能删除）
                old[k] = new[k]
            elseif isData(k, v) then -- 数据字段，跳过更新

            elseif type(old[k]) == "table" and type(v) == "table" and v ~= old[k] then -- 都是表，递归更新
                update(old[k], v)
            else -- 类型相同、类型不同、函数类型，全部都直接替换（也可能新增）
                old[k] = new[k]
            end
        end

        -- 元表不会参与更新（因为元表可能是来自其他模块的对象，热重载只应该发生在单个模块中）
        -- 但是如果元表不一样，这里暂时选择替换（可能有潜在风险）
        local oldMt = getmetatable(old)
        local newMt = getmetatable(new)
        if oldMt ~= newMt then
            setmetatable(old, newMt)
        end
    end
    update(oldTable, newTable)
end

-- 重载模块，要点：
-- - 会清除 packaged.loaded 中的模块缓存，然后重新 require 导入
-- - 只有导出部分（export）里的内容会被更新
-- - 旧模块会被替换并被弱表缓存（这样多次重载时，就能重载之前残留的模块）
function export.reloadModule(moduleName)
    local old = package.loaded[moduleName]
    if not old then
        -- print(string.format("HotReload skipped: '%s', module not loaded", moduleName))
        return
    end
    if not export.canReload(moduleName) then
        print(string.format("Can't reload '%s'", moduleName))
        return
    end

    package.loaded[moduleName] = nil
    local status, new = pcall(require, moduleName)

    -- 基本检查
    if not status then
        print(string.format("HotReload failed: %s\t%s", moduleName, new))
        package.loaded[moduleName] = old -- 恢复旧模块引用
        return
    elseif type(old) ~= "table" or type(new) ~= "table" then
        print(string.format("HotReload failed: %s, old or new module is not a table", moduleName))
        package.loaded[moduleName] = old -- 恢复旧模块引用
        return
    end

    -- 更新旧模块
    local oldModules = OLD_MODULES_CACHE[old]
    OLD_MODULES_CACHE[old] = nil
    if type(oldModules) ~= "table" then
        oldModules = {}
        setmetatable(oldModules, { __mode = "k" }) -- 弱表
    end
    oldModules[old] = true -- 缓存旧模块（弱引用）
    for module, _ in pairs(oldModules) do
        updateTable(module, new)
    end
    OLD_MODULES_CACHE[new] = oldModules -- 传递缓存

    print(string.format("HotReload success: '%s'", moduleName))
end

-- 完全重载
function export.reloadAllModules()
    local moduleList = {} -- 避免在遍历时修改 package.loaded
    for moduleName, module in pairs(package.loaded) do
        if export.canReload(moduleName) and isProjectModule(moduleName) then
            table.insert(moduleList, moduleName)
        end
    end
    for _, moduleName in ipairs(moduleList) do
        export.reloadModule(moduleName)
    end
end

return export

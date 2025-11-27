local export = {}
local Class = require("Lua.Universal.Class")

-- LuaBehaviour 和 LuaSingleton 上的基本绑定
local UNITY_MESSAGES = {
    "awake",
    "onEnable",
    "start",
    "update",
    "onDisable",
    "onDestroy",
}

-- 该代码由 C# 的 LuaBridge.CreateBinding 调用，其运行的上下文存在一些特殊变量：
-- - scope: 上下文本身，是 Lua 绑定脚本文件的 require 环境，也是 __init__ 和 _bind 的执行环境
-- - component: 当前绑定的 Unity 组件实例
local BindingComponentBase = Class.class()

    -- 构造函数参数固定
    function BindingComponentBase:__init__(component)
        self.component = component
        self._bindings = {} -- 存放绑定的消息，供 C# 侧调用
    end
    
    -- 将组件的消息绑定到执行环境上（这样 C# 侧就可以从环境中直接读取），必须是无参闭包
    -- 该函数不在构造函数中调用，而是由 LuaBridge.CreateBinding 显式调用（毕竟逻辑上是个虚方法，就不放到构造函数了）
    function BindingComponentBase:_bind()
        for _, msgName in ipairs(UNITY_MESSAGES) do
            if self[msgName] then
                self._bindings[msgName] = function() self[msgName](self) end
            end
        end
    end

export.BindingComponentBase = BindingComponentBase

return export

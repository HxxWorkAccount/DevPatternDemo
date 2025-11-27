local export = {}
local Class = require("Lua.Universal.Class")
local BindingComponentBase = require("Lua.Universal.BindingComponentBase").BindingComponentBase

-- 测试 BindingComponentBase 的绑定功能

local TestBinding = Class.class(BindingComponentBase)

    function TestBinding:__init__(component)
        Class.initbase(TestBinding, self, component)
        self.testValue = 42
        self.m_interval = 0
    end

    function TestBinding:awake()
        print("TestBinding awake, testValue =", self.testValue)
    end

    function TestBinding:onEnable()
        print("TestBinding onEnable")
    end

    function TestBinding:start()
        print("TestBinding start")
    end

    function TestBinding:update()
        if self.m_interval > 1.3 then
            print("TestBinding update")
            -- print("Test LuaTable " .. tostring(self.component.lobject.testValue))
            self.m_interval = 0
        else
            self.m_interval = self.m_interval + CS.UnityEngine.Time.deltaTime
        end
    end

    function TestBinding:onDisable()
        print("TestBinding onDisable")
    end

    function TestBinding:onDestroy()
        print("TestBinding onDestroy")
    end

export.TestBinding = TestBinding

return export

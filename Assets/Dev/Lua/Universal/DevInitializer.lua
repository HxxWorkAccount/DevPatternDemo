local export = {}
local Class = require("Lua.Universal.Class")
local BindingComponentBase = require("Lua.Universal.BindingComponentBase").BindingComponentBase

-- 测试 BindingComponentBase 的绑定功能

local DevInitializer = Class.class(BindingComponentBase)

    function DevInitializer:__init__(component)
        Class.initbase(DevInitializer, self, component)
    end

    function DevInitializer:awake()
        print("DevInitializer awake, testValue =", self.testValue)
    end

    function DevInitializer:start()
        print("DevInitializer start")
    end

    function DevInitializer:onDisable()
        print("DevInitializer onDisable")
    end

    function DevInitializer:onDestroy()
        print("DevInitializer onDestroy")
    end

export.DevInitializer = DevInitializer


return export

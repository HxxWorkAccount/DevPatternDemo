namespace DevPattern.Lua {
using XLua;
using System;
using System.Text;
using UnityEngine;
using DevPattern.Universal.Lua;

public class LuaBindingHelper : ILuaBindingHelper
{
    public ILuaTable Bind(Component component, string bindingModule, string bindingClass = "") {
        var luaEnv = LuaManager.instance.LuaEnv ?? throw new Exception("LuaBindingHelper Bind failed, LuaEnv is null");

        // 临时上下文
        using LuaTable scriptScope = luaEnv.NewTable();
        using (LuaTable meta = luaEnv.NewTable()) {
            meta.Set("__index", luaEnv.Global);
            scriptScope.SetMetaTable(meta);
        }

        // 将所需值注入到 Lua 脚本域中
        scriptScope.Set("component", component);

        // Lua 绑定类的名字
        if (string.IsNullOrEmpty(bindingClass)) {
            bindingClass = bindingModule;
            int lastDot  = bindingModule.LastIndexOf('.');
            if (lastDot != -1) {
                bindingClass = bindingModule[(lastDot + 1)..];
            } else {
                Debug.LogWarning($"LuaBridge.CreateBinding: failed to infer binding class name from module: '{bindingModule}'.");
                bindingClass = "BindingComponent";
            }
        }

        // 执行绑定脚本
        string bindingScript = $@"
            local BindingModule = require('{bindingModule}')
            local BindingClass = BindingModule['{bindingClass}']
            local bindingInstance = BindingClass(component)
            bindingInstance:_bind()
            return bindingInstance
        ";

        object[] results         = luaEnv.DoString(bindingScript, bindingModule, scriptScope);
        LuaTable bindingInstance = (LuaTable)results[0];
        return new LuaTableWrapper(bindingInstance);
    }
}

}

namespace DevPattern.Universal.Lua {
using System;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;

// Lua 中可这样获取单例对象：
// local singleton = CS.DevPattern.Universal.Lua.LuaSingleton.instance.luaInstance
// if notNull(singleton) then
//     local singletonInstance = singleton.luaInstance
//     ...
// end
public abstract class LuaSingleton : Singleton<LuaBehaviour> {
    protected Action    m_luaAwake;
    protected Action    m_luaOnEnable;
    protected Action    m_luaStart;
    protected Action    m_luaUpdate;
    protected Action    m_luaOnDisable;
    protected Action    m_luaOnDestroy;
    protected ILuaTable m_luaInstance;
    public ILuaTable luaInstance => m_luaInstance;
    public object lobject => m_luaInstance.lobject;

    public readonly string bindingModule;
    protected readonly string m_bindingClass;

    /*====-------------- Life --------------====*/

    public LuaSingleton(string bindingModule, string bindingClass = "") {
        this.bindingModule = bindingModule;
        m_bindingClass  = bindingClass;
    }

    protected override void OnSingletonAwake() {
        m_luaInstance = LuaBridge.instance.luaBindingHelper.Bind(this, bindingModule, m_bindingClass);
        m_luaAwake = m_luaInstance.GetInPath<Action>("_bindings.awake");
        m_luaOnEnable = m_luaInstance.GetInPath<Action>("_bindings.onEnable");
        m_luaStart = m_luaInstance.GetInPath<Action>("_bindings.start");
        m_luaUpdate = m_luaInstance.GetInPath<Action>("_bindings.update");
        m_luaOnDisable = m_luaInstance.GetInPath<Action>("_bindings.onDisable");
        m_luaOnDestroy = m_luaInstance.GetInPath<Action>("_bindings.onDestroy");

        LAwake();
        m_luaAwake?.Invoke();
    }
    protected virtual void OnEnable() {
        OnLEnable();
        m_luaOnEnable?.Invoke();
    }
    protected virtual void Start() {
        LStart();
        m_luaStart?.Invoke();
    }
    protected virtual void Update() {
        LUpdate();
        m_luaUpdate?.Invoke();
    }
    protected override void OnSingletonDisable() {
        m_luaOnDisable?.Invoke();
        OnLDisable();
    }
    protected override void OnSingletonDestroy() {
        m_luaOnDestroy?.Invoke();
        OnLDestroy();
        // 如果有自定义消息，也要记得释放！
        m_luaAwake     = null;
        m_luaOnEnable  = null;
        m_luaStart     = null;
        m_luaUpdate    = null;
        m_luaOnDisable = null;
        m_luaOnDestroy = null;
        m_luaInstance?.Dispose();
    }

    /*====-------------- Messages --------------====*/

    protected virtual void LAwake() { }
    protected virtual void OnLEnable() { }
    protected virtual void LStart() { }
    protected virtual void LUpdate() { }
    protected virtual void OnLDisable() { }
    protected virtual void OnLDestroy() { }
}

}

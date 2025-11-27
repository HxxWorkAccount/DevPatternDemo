namespace DevPattern.Lua {
using XLua;
using System;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;

class LuaFunctionWrapper : ILuaFunction
{
    private LuaFunction m_luaFunction;
    public LuaFunction  luaFunction => m_luaFunction;

    /*====-------------- Life --------------====*/

    public LuaFunctionWrapper(LuaFunction function) {
        m_luaFunction = function;
    }

    public static implicit operator LuaFunction(LuaFunctionWrapper wrapper) {
        return wrapper.m_luaFunction;
    }

    public void Dispose() {
        m_luaFunction?.Dispose();
    }

    public override bool Equals(object obj) {
        return m_luaFunction.Equals(obj);
    }
    public override int GetHashCode() {
        return m_luaFunction.GetHashCode();
    }
    public override string ToString() {
        return m_luaFunction.ToString();
    }

    /*====-------------- ILuaFunction --------------====*/

    public object lobject => m_luaFunction;

    public void Action<T>(T a) {
        m_luaFunction.Action(a);
    }
    public TResult Func<T, TResult>(T a) {
        return m_luaFunction.Func<T, TResult>(a);
    }
    public void Action<T1, T2>(T1 a1, T2 a2) {
        m_luaFunction.Action(a1, a2);
    }
    public TResult Func<T1, T2, TResult>(T1 a1, T2 a2) {
        return m_luaFunction.Func<T1, T2, TResult>(a1, a2);
    }
    public T Cast<T>() {
        return m_luaFunction.Cast<T>();
    }
    public void SetEnv(ILuaTable env) {
        m_luaFunction.SetEnv((LuaTableWrapper)env);
    }

    // deprecated
    public object[] Call(object[] args, Type[] returnTypes) {
        return m_luaFunction.Call(args, returnTypes);
    }
    // deprecated
    public object[] Call(params object[] args) {
        return m_luaFunction.Call(args);
    }
}

}

namespace DevPattern.Lua {
using XLua;
using System;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;

class LuaEnvWrapper : ILuaEnv
{
    private LuaEnv m_luaEnv;
    public LuaEnv  luaEnv => m_luaEnv;

    /*====-------------- Life --------------====*/

    public LuaEnvWrapper(LuaEnv luaEnv) {
        m_luaEnv = luaEnv;
    }

    public static implicit operator LuaEnv(LuaEnvWrapper wrapper) {
        return wrapper.m_luaEnv;
    }

    public void Dispose() {
        m_luaEnv?.Dispose();
    }

    public override bool Equals(object obj) {
        return m_luaEnv.Equals(obj);
    }
    public override int GetHashCode() {
        return m_luaEnv.GetHashCode();
    }
    public override string ToString() {
        return m_luaEnv.ToString();
    }

    /*====-------------- ILuaEnv --------------====*/

    public ILuaTable Global => new LuaTableWrapper(m_luaEnv.Global);

    public T LoadString<T>(byte[] chunk, string chunkName = "chunk", ILuaTable env = null) {
        return m_luaEnv.LoadString<T>(chunk, chunkName, (LuaTableWrapper)env);
    }

    public T LoadString<T>(string chunk, string chunkName = "chunk", ILuaTable env = null) {
        return m_luaEnv.LoadString<T>(chunk, chunkName, (LuaTableWrapper)env);
    }

    public ILuaFunction LoadString(string chunk, string chunkName = "chunk", ILuaTable env = null) {
        var func = m_luaEnv.LoadString<XLua.LuaFunction>(chunk, chunkName, (LuaTableWrapper)env);
        return new LuaFunctionWrapper(func);
    }

    public object[] DoString(byte[] chunk, string chunkName = "chunk", ILuaTable env = null) {
        return m_luaEnv.DoString(chunk, chunkName, (LuaTableWrapper)env);
    }

    public object[] DoString(string chunk, string chunkName = "chunk", ILuaTable env = null) {
        return m_luaEnv.DoString(chunk, chunkName, (LuaTableWrapper)env);
    }

    public void Alias(Type type, string alias) {
        m_luaEnv.Alias(type, alias);
    }

    public ILuaTable NewTable() {
        return new LuaTableWrapper(m_luaEnv.NewTable());
    }

    public void ThrowExceptionFromError(int oldTop) {
        m_luaEnv.ThrowExceptionFromError(oldTop);
    }

    public void GC() {
        m_luaEnv.GC();
    }
    public int  GcPause { get => m_luaEnv.GcPause; set => m_luaEnv.GcPause = value; }
    public int  GcStepmul { get => m_luaEnv.GcStepmul; set => m_luaEnv.GcStepmul = value; }
    public void FullGc() {
        m_luaEnv.FullGc();
    }
    public void StopGc() {
        m_luaEnv.StopGc();
    }
    public void RestartGc() {
        m_luaEnv.RestartGc();
    }
    public bool GcStep(int data) {
        return m_luaEnv.GcStep(data);
    }
    public int  Memroy => m_luaEnv.Memroy;

}

}

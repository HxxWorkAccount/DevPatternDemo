namespace DevPattern.Universal.Lua {
using System;
using UnityEngine;

public interface ILuaEnv
{
    public ILuaTable Global { get; }

    public T LoadString<T>(byte[] chunk, string chunkName = "chunk", ILuaTable env = null);

    public T LoadString<T>(string chunk, string chunkName = "chunk", ILuaTable env = null);

    public ILuaFunction LoadString(string chunk, string chunkName = "chunk", ILuaTable env = null);

    public object[] DoString(byte[] chunk, string chunkName = "chunk", ILuaTable env = null);

    public object[] DoString(string chunk, string chunkName = "chunk", ILuaTable env = null);

    public void Alias(Type type, string alias);

    public ILuaTable NewTable();

    public void ThrowExceptionFromError(int oldTop);

    public void GC();
    public int  GcPause { get; set; }
    public int  GcStepmul { get; set; }
    public void FullGc();
    public void StopGc();
    public void RestartGc();
    public bool GcStep(int data);
    public int  Memroy { get; }
}

}

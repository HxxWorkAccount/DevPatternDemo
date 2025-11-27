namespace DevPattern.Lua {
using XLua;
using System;
using System.Text;
using UnityEngine;
using DevPattern.Universal.Lua;

public class LuaCLIHelper : ILuaCLIHelper
{
    public LuaTable m_cliEnv;
    public StringBuilder m_outputBuffer = new();

    public void TryInitEnv() {
        if (m_cliEnv != null || LuaManager.rawInstance == null)
            return;
        m_cliEnv            = LuaManager.instance.LuaEnv.NewTable();
        using LuaTable meta = LuaManager.instance.LuaEnv.NewTable();
        meta.Set("__index", LuaManager.instance.LuaEnv.Global);
        m_cliEnv.SetMetaTable(meta);
        m_cliEnv.Set("global", LuaManager.instance.LuaEnv.Global);
    }

    public void ExecuteLuaCode(string luaCode) {
        if (LuaManager.rawInstance == null) {
            Debug.LogWarning("LuaManager not initialized.");
            return;
        } else {
            LuaFunction func = null;
            try {
                func = LuaManager.instance.LuaEnv.LoadString("return " + luaCode, "GameCLI");
            } catch (Exception) { }
            func ??= LuaManager.instance.LuaEnv.LoadString(luaCode, "GameCLI");

            TryInitEnv();
            if (m_cliEnv == null) {
                Debug.LogError("Failed to create CLI lua environment.");
                return;
            }

            func.SetEnv(m_cliEnv);
            object[] results;
            try {
                results = func.Call();
            } catch (Exception e) {
                Debug.LogError($"Lua: {e.Message}");
                return;
            }
            if (results != null && results.Length != 0) {
                m_outputBuffer.Clear();
                for (int i = 0; i < results.Length; i++) {
                    object result = results[i];
                    if (result == null) {
                        m_outputBuffer.Append("nil");
                    } else if (result is string s) {
                        m_outputBuffer.Append($"\"{s}\"");
                    } else if (result is double || result is float) {
                        m_outputBuffer.Append(string.Format("{0:G}", result));
                    } else {
                        m_outputBuffer.Append(result.ToString());
                    }
                    if (i < results.Length - 1)
                        m_outputBuffer.Append("    ");
                }
                Debug.Log($"LUA: {m_outputBuffer}");
            }
        }
    }
}

}

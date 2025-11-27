namespace DevPattern.Lua {
using XLua;
using System;
using System.Text;
using UnityEngine;
using DevPattern.Universal.Lua;
using System.IO;
using DevPattern.Universal.Utils;

public class LuaHotreloadHelper : ILuaHotreloadHelper
{
    private string m_currentReloadModuleName = string.Empty;
    private bool   m_reloadAllFlag           = false;

    public LuaHotreloadHelper() {
        LuaManager.instance.RegisterCustomLoader(HotreloadLoader, -100);
    }

    public void HotReloadModule(string moduleName) {
        var luaEnv                = LuaManager.instance.LuaEnv ?? throw new Exception("LuaHotreloadHelper: LuaEnv is null");
        m_currentReloadModuleName = moduleName;
        try {
            luaEnv.DoString($@"require('Lua.Universal.LuaHotreload').reloadModule('{moduleName}')");
        } catch (Exception e) {
            Debug.LogError($"[LuaHotreloadHelper] HotReloadModule Exception: {e}");
        }
        m_currentReloadModuleName = string.Empty;
    }

    public void HotReloadAllModules() {
        var luaEnv      = LuaManager.instance.LuaEnv ?? throw new Exception("LuaHotreloadHelper: LuaEnv is null");
        m_reloadAllFlag = true;
        try {
            luaEnv.DoString($@"require('Lua.Universal.LuaHotreload').reloadAllModules()");
        } catch (Exception e) {
            Debug.LogError($"[LuaHotreloadHelper] HotReloadAllModules Exception: {e}");
        }
        m_reloadAllFlag = false;
    }

    private byte[] HotreloadLoader(string moduleName, bool reload) {
        if (!reload)
            return null;
        if (moduleName != m_currentReloadModuleName && !m_reloadAllFlag)
            return null;

        string assetPath;
        bool   isResource;
        if (!EnvironmentUtils.env.isEditor) {  // 非 Editor 环境先看看散包下有没有，有就读取
            assetPath = LuaBridge.LuaToAssetPath(moduleName, LuaSource.Additional, out isResource);
            if (!isResource && File.Exists(assetPath)) {
                return File.ReadAllBytes(assetPath);
            }
        } else {
            // 直接读取 Assets 目录下的文件进行重载
            assetPath = LuaBridge.LuaToAssetPath(moduleName, LuaSource.Addressable, out isResource);
            if (isResource)  // Resource 目录下的资源暂不支持热更，因为我压根不知道具体目录在哪（Resources 目录可以放在任意位置）
                return null;
            string fullAssetPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
            return File.ReadAllBytes(fullAssetPath);
        }
        Debug.LogWarning($"[HotreloadLoader] Hotreload failed! moduleName: '{moduleName}', asset path: '{assetPath}'");
        return null;
    }
}

}

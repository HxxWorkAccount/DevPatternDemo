namespace DevPattern.Universal.Lua {
using System;
using UnityEngine;
using DevPattern.Universal.Utils;
using System.Collections.Concurrent;
using System.IO;

/* LuaManager 等代码必须和 xLua 源码一起放在 Assembly-CSharp.csproj，
       所以 Release、Dev、Test 都无法直接操控 Lua，这里搞个桥接来解决 */

public enum LuaSource {
    Addressable,
    Resource,
    Additional,  // 散包外链附加到 StreammingAssets 下的 Lua 脚本
    // Hotfix,
}

public class LuaBridge
{
    private static LuaBridge s_instance = new LuaBridge();
    public static LuaBridge  instance {
        get {
            s_instance ??= new LuaBridge();
            return s_instance;
        }
    }

    private ILuaEnv             m_luaEnv;
    public ILuaEnv              luaEnv => m_luaEnv;
    private ILuaCLIHelper       m_luaCLIHelper;
    public ILuaCLIHelper        luaCLIHelper => m_luaCLIHelper;
    private ILuaBindingHelper   m_luaBindingHelper;
    public ILuaBindingHelper    luaBindingHelper => m_luaBindingHelper;
    private ILuaHotreloadHelper m_luaHotreloadHelper;
    public ILuaHotreloadHelper  luaHotreloadHelper => m_luaHotreloadHelper;

    private static string k_additionalLuaFolder;
    public static string  additionalLuaFolder {
        get {
            if (string.IsNullOrEmpty(k_additionalLuaFolder)) {
                k_additionalLuaFolder = $"{Application.streamingAssetsPath}/AdditionalLua/";
            }
            return k_additionalLuaFolder;
        }
    }

    /*====-------------- For LuaManager --------------====*/

    public void _InitByLuaManager(  //
        ILuaEnv             luaEnv,
        ILuaCLIHelper       luaCLIHelper,
        ILuaBindingHelper   luaBindingHelper,
        ILuaHotreloadHelper luaHotreloadHelper
    ) {
        m_luaEnv             = luaEnv;
        m_luaCLIHelper       = luaCLIHelper;
        m_luaBindingHelper   = luaBindingHelper;
        m_luaHotreloadHelper = luaHotreloadHelper;
    }

    /*====-------------- Utils --------------====*/

    /* hintWhenNotResource 是指当资源不是 Resource 时，解析为什么路径。各情况：
     * - 当 moduleName 以 "Resources." 开头时，强制解析为 Resource 路径
     * - 非 Resource 开头 + LuaSource.Addressables 时，解析为 "Assets/..."（Addressables 可用的资源路径）
     * - 非 Editor 模式 + 非 Resource 开头 + LuaSource.Additional 时，解析为 "Application.streamingAssetsPath/..."（散包外链附加路径）
     * - */
    public static string LuaToAssetPath(string moduleName, LuaSource hintWhenNotResource, out bool isResource) {
        var env    = EnvironmentUtils.env;
        isResource = false;
        if (moduleName.StartsWith("Resources.")) {
            isResource = true;
            return moduleName["Resources.".Length..].Replace('.', '/');
        } else if (hintWhenNotResource == LuaSource.Addressable) {
            return $"Assets/{moduleName.Replace('.', '/')}.lua";
        } else if (!env.isEditor && hintWhenNotResource == LuaSource.Additional) {  // 散包只有非 Editor 模式才支持
            return Path.Combine(Application.streamingAssetsPath, "AdditionalLua", $"{moduleName.Replace('.', '/')}.lua");
        } else {
            throw new Exception($"Unsupported hintWhenNotResource: '{hintWhenNotResource}', ModuleName: '{moduleName}'");
        }
    }

    /* inferredSource 是根据路径模式自动识别出的类型。各情况：
     * - 当 assetPath 以 "Assets/" 开头时，解析为 Addressables 资源路径，并返回对应模块名
     * - 当非 Editor 模式时，且 assetPath 与 Application.streamingAssets 开头一致时，解析为散包资源，并返回对应模块名
     * - 其他情况，一律当作 Resource 资源（危），可以把 Resource 作为一种兜底结果（或是理解为潜在非法路径），并返回对应模块名
     * - */
    public static string AssetToLuaPath(string assetPath, out LuaSource inferredSource) {
        var env   = EnvironmentUtils.env;
        assetPath = assetPath.Replace('\\', '/');
        if (assetPath.StartsWith("Assets/")) {
            Debug.Assert(assetPath.EndsWith(".lua"), $"Lua Asset Path should ends with '.lua': {assetPath}");
            assetPath      = assetPath["Assets/".Length..^ ".lua".Length];
            inferredSource = LuaSource.Addressable;
            return assetPath.Replace('/', '.');
        }
        bool isRooted = Path.IsPathRooted(assetPath);
        if (!env.isEditor && isRooted && PathUtils.IsPathUnderDirectory(assetPath, additionalLuaFolder)) {
            Debug.Assert(assetPath.EndsWith(".lua"), $"Additional Lua Path should ends with '.lua': {assetPath}");
            string relativePath = assetPath[additionalLuaFolder.Length..^ ".lua".Length];
            inferredSource      = LuaSource.Additional;
            return relativePath.Replace('/', '.');
        }
        // 兜底当 Resource 处理
        Debug.Assert(!assetPath.EndsWith(".lua"), $"Resource Lua Path should not ends with '.lua': {assetPath}");
        Debug.Assert(!isRooted, $"Resource Lua Path should not be rooted path: {assetPath}");
        inferredSource = LuaSource.Resource;
        return $"Resources.{assetPath.Replace('/', '.')}";
    }
}

}

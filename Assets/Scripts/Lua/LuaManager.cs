namespace DevPattern.Lua {
using XLua;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Linq;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;
using DevPattern.Universal;
using Unity.VisualScripting.Dependencies.NCalc;

// FIXIT == 考虑用 util.print_func_ref_by_csharp() 找到未释放的代码
[DefaultExecutionOrder(5000)]  // 确保最晚回收 LuaEnv
public sealed class LuaManager : Singleton<LuaManager>
{
    public delegate byte[] LoadLuaDelegate(string moduleName, bool reload);

    private class LuaLoaderInfo
    {
        public LoadLuaDelegate loader;
        public int             priority;
    }

    private readonly LuaEnv m_luaEnv = new();
    public LuaEnv           LuaEnv   => m_luaEnv;

    private List<LuaLoaderInfo> m_customLoaders = new();

    private Dictionary<string, byte[]>                                                                         m_moduleCache = new();
    private Dictionary<string, UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<TextAsset>> m_handleCache = new();

    /*====-------------- Life --------------====*/

    // 这里在 Init 中初始化，因为 LuaManager 的执行顺序靠后（确保最晚回收，但又要提前初始化）
    protected override void OnSingletonInit() {
        /* 注册 Lua Loader */
        m_luaEnv.AddLoader(LuaModuleLoader);
        RegisterCustomLoader(DefaultLoader, -200);  // 注册默认加载器
        if (!EnvironmentUtils.env.isEditor) {
            RegisterCustomLoader(DevAdditionalLoader, -190);
        }

        /* 执行 main 函数 */
        m_luaEnv.DoString("require 'Lua.Universal.Main'");

        /* 初始化 LuaBridge */
        LuaBridge.instance._InitByLuaManager(  //
            new LuaEnvWrapper(m_luaEnv), new LuaCLIHelper(), new LuaBindingHelper(), new LuaHotreloadHelper()
        );
    }

    private void Update() {
        m_luaEnv.Tick();
    }

    protected override void OnSingletonDestroy() {
        m_luaEnv?.Dispose();
    }

    /*====-------------- Lua Loader --------------====*/

    public byte[] LuaModuleLoader(ref string moduleName) {
        byte[] result = null;
        bool reload   = m_moduleCache.ContainsKey(moduleName);

        foreach (var loaderInfo in m_customLoaders) {
            try {
                result = loaderInfo.loader(moduleName, reload);
            } catch (Exception e) {
                Debug.LogError($"[LuaManager] Exception in loader for module '{moduleName}': {e}");
            }
            if (result != null)
                break;
        }

        if (result != null)
            m_moduleCache[moduleName] = result;
        else if (reload)
            result = m_moduleCache[moduleName];
        return result;
    }

    /* C# List.Sort 排序不稳定，尽量不要写 priority 相同的情况 */
    public void RegisterCustomLoader(LoadLuaDelegate loader, int priority = 0) {
        foreach (var loaderInfo in m_customLoaders) {
            if (loaderInfo.loader == loader) {
                Debug.LogWarning("[LuaManager] Loader already registered.");
                return;
            }
        }
        m_customLoaders.Add(new LuaLoaderInfo() { loader = loader, priority = priority });
        m_customLoaders.Sort((a, b) => b.priority.CompareTo(a.priority));  // 由大到小排序
    }

    public void UnregisterCustomLoader(LoadLuaDelegate loader) {
        int index = -1;
        foreach (var loaderInfo in m_customLoaders) {
            if (loaderInfo.loader == loader) {
                index = m_customLoaders.IndexOf(loaderInfo);
                break;
            }
        }
        if (index == -1) {
            Debug.LogWarning("[LuaManager] Loader not found.");
            return;
        } else {
            m_customLoaders.RemoveAt(index);
        }
    }

    /*====-------------- Built-in Loader --------------====*/

    private byte[] DefaultLoader(string moduleName, bool reload) {
        string assetPath = LuaBridge.LuaToAssetPath(moduleName, LuaSource.Addressable, out bool isResource);
        if (isResource) {
            var luaScript = Resources.Load<TextAsset>(assetPath);
            return luaScript.bytes;
        } else if (reload) {
            return null;  // 返回 null 后 LuaModuleLoader 会自己从缓存中读取
        } else if (m_handleCache.TryGetValue(assetPath, out var cachedHandle)) {
            return cachedHandle.Result.bytes;
        } else {
            var resourceHandle = ResourceUtils.LoadAsset<TextAsset>(assetPath);
            try {
                var text = resourceHandle.WaitForCompletion();
                m_handleCache.Add(assetPath, resourceHandle);
                return text.bytes;
            } catch (Exception) {
                Debug.Log($"[LuaModuleLoader] module path not found: {moduleName}, asset path: {assetPath}");
                Addressables.Release(resourceHandle);  // 仅异常时需要释放
            }
        }
        return null;
    }

    private byte[] DevAdditionalLoader(string moduleName, bool reload) {
        string assetPath = LuaBridge.LuaToAssetPath(moduleName, LuaSource.Additional, out bool isResource);
        if (isResource)
            return null;
        else if (reload)
            return null;  // reload 工作让 Hotreload 来处理，否则就直接读缓存
        else if (File.Exists(assetPath))
            return File.ReadAllBytes(assetPath);
        return null;
    }
}
}

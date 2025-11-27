namespace DevPattern.Universal.Utils {
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class ResourceUtils
{
    public static bool ValidateAssetPath(string assetPath) {
        /* 规则：
         * - Lua 文件只能在 Assets/Lua, Assets/Dev/Lua, Assets/Tests/Lua 目录下
         * - 如果路径中含有目录为特定平台（如：Editor、Client、Server、PC、Android、PE...），则当且仅当平台符合时通过
         * - 如果路径含有 Dev、Tests 则当且仅当对应版本可以通过 */
#if !UNITY_DEV && !UNITY_TEST
        return true;  // Release 版本跳过检查
#else
        var env = EnvironmentUtils.env;

        assetPath = assetPath.Replace('\\', '/');
        if (!assetPath.StartsWith("Assets/"))
            return false;

        /* 检查 Lua 文件 */
        if (assetPath.EndsWith(".lua")) {
            bool isValidStart =
                assetPath.StartsWith("Assets/Lua/") || (env.isDev && assetPath.StartsWith("Assets/Dev/Lua/")) ||
                (env.isTest && assetPath.StartsWith("Assets/Tests/Lua/"));
            if (!isValidStart)
                return false;
        }

        /* 检查发布类型 */
        if (assetPath.Contains("/Dev/") && !env.isDev)
            return false;
        if (assetPath.Contains("/Tests/") && !env.isTest)
            return false;

        /* 检查平台类型 */
        if (assetPath.Contains("/Editor/") && !env.isEditor)
            return false;
        if (assetPath.Contains("/Client/") && !env.isClient)
            return false;
        if (assetPath.Contains("/Server/") && !env.isServer)
            return false;
        if (assetPath.Contains("/Mobile/") && !env.isMobile)
            return false;
        if (assetPath.Contains("/Console/") && !env.isConsole)
            return false;

        return true;
#endif
    }

    public static AsyncOperationHandle<TObject> LoadAsset<TObject>(string assetPath) {
        if (!ValidateAssetPath(assetPath)) {
            return Addressables.ResourceManager.CreateCompletedOperationWithException<TObject>(default, new InvalidKeyException(assetPath));
        }
        return Addressables.LoadAssetAsync<TObject>(assetPath);
    }
}

}

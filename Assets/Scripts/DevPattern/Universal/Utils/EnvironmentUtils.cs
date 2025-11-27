namespace DevPattern.Universal.Utils {
using System;
using UnityEngine;

public static class EnvironmentUtils
{
    /* 注意！！！
     * 这几个宏不要在代码其他地方使用！！！（包括：UNITY_CLIENT、UNITY_SERVER、UNITY_DEV、UNITY_TEST、UNITY_RELEASE）
     * 因为这几个宏是编译时设置的（也不能在 Player 中添加，会破坏编译逻辑）
     * 之所以这样是因为 Unity 似乎没有提供一个方便的途径控制宏列表
     * 随意切换 PlayerSettings 里的宏不但会导致 Domain Reload，而且还可能导致 Addressables 出现一些诡异的错误（例如 Include In Build 为 false 时抛出错误）
     * 因此，统一 Editor 模式下都以 host + dev + test 的模式开发运行即可（不要切换 PlayerSettings 宏）
     * 如果要在其他任何代码处做判断，直接通过 `Environment.env` 里的属性来判断即可！ */

    public const bool IsClient =
#if UNITY_CLIENT || UNITY_EDITOR
        true;
#else
        false;
#endif

    public const bool IsServer =
#if UNITY_SERVER || UNITY_EDITOR
        true;
#else
        false;
#endif

    public const bool IsHost =
#if UNITY_CLIENT && UNITY_SERVER || UNITY_EDITOR
        true;
#else
        false;
#endif

    public const bool IsDev =
#if UNITY_DEV || UNITY_EDITOR
        true;
#else
        false;
#endif

    public const bool IsTest =
#if UNITY_TEST || UNITY_EDITOR
        true;
#else
        false;
#endif

    public const bool IsRelease =
#if UNITY_RELEASE && !UNITY_EDITOR
        true;
#else
        false;
#endif

    public struct Environment
    {
        public readonly bool            isClient          => IsClient;
        public readonly bool            isServer          => IsServer;
        public readonly bool            IsDedicatedServer => IsServer && !IsClient;
        public readonly bool            IsDedicatedClient => IsClient && !IsServer;
        public readonly bool            isDev             => IsDev;
        public readonly bool            isTest            => IsTest;
        public readonly bool            isEditor          => Application.isEditor;
        public readonly bool            isRelease         => IsRelease;
        public readonly bool            isHost            => isClient && isServer;
        public readonly bool            isConsole         => Application.isConsolePlatform;
        public readonly bool            isMobile          => Application.isMobilePlatform;
        public readonly RuntimePlatform platform          => Application.platform;
    }

    private static bool        s_environmentInitialized = false;
    private static Environment s_environment;
    public static Environment  env {
        get {
            if (!s_environmentInitialized) {
                s_environment = new Environment();
            }
            return s_environment;
        }
    }
}

}

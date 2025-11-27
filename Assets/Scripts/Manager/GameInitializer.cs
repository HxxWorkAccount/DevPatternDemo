namespace DevPattern.Manager {
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Lua;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;
using DevPattern.Universal;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/* 因为大多数管理器（如：LuaManager）的执行次序都靠后，但往往又需要最先初始化，所以这里提供一个脚本来对他们进行初始化 */
[DefaultExecutionOrder(-1000)]
public sealed class GameInitializer : Singleton<GameInitializer>
{
    protected override void OnSingletonAwake() {
        InitBasicSystem();
    }

    private void InitBasicSystem() {
        var luaManager = LuaManager.instance;
    }
}

}

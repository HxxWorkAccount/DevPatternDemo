namespace DevPattern.Dev.Universal {
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Manager;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;
using DevPattern.Dev.Universal.Lua;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class DevInitializer : LuaBehaviour
{
    private readonly InitCLIManagerOperation        m_initCLIManager        = new();
    private readonly InitHotreloadListenerOperation m_initHotreloadListener = new();

    /*====-------------- Life --------------====*/

    public DevInitializer(): base("Dev.Lua.Universal.DevInitializer") { }

    protected override void LAwake() {
        InitOperationManager.instance.Register(m_initCLIManager);
        InitOperationManager.instance.Register(m_initHotreloadListener);
    }

    protected override void LStart() {
        InitOperationManager.instance.Restart(m_initCLIManager.id);
        InitOperationManager.instance.Restart(m_initHotreloadListener.id);
    }

    protected override void OnLDestroy() {
        m_initCLIManager.Dispose();
        m_initHotreloadListener.Dispose();
    }

    /*====-------------- Initializer --------------====*/

    public class InitCLIManagerOperation : InitPrefabOperation
    {
        public const string k_id         = "InitCLIManagerOperation";
        public const string k_prefabPath = "Assets/Dev/Content/Universal/Prefabs/CLIManager.prefab";

        public InitCLIManagerOperation(): base(k_id, k_prefabPath) { }
    }

    public class InitHotreloadListenerOperation : InitComponentOperation<LuaHotreloadListener>
    {
        public const string k_id = "InitHotreloadListenerOperation";

        public InitHotreloadListenerOperation(): base(k_id, "LuaHotreloadListener") { }
    }
}

}

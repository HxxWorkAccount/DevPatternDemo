namespace DevPattern.Universal.Manager {
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class InitPrefabOperation : InitOperationBase
{
    public readonly string                     prefabPath = "";
    protected AsyncOperationHandle<GameObject> m_resourceHandle;
    public AsyncOperationHandle<GameObject>    resourceHandle => m_resourceHandle;

    public InitPrefabOperation(string id, string prefabPath, HashSet<string> dependencies = null): base(id, dependencies) {
        this.prefabPath = prefabPath;
        Debug.Assert(!string.IsNullOrEmpty(this.prefabPath), "InitPrefabOperation prefabPath is null or empty!");
    }

    protected override IEnumerator DoRestart() {
        if (!m_resourceHandle.IsValid())
            m_resourceHandle = ResourceUtils.LoadAsset<GameObject>(prefabPath);
        yield return m_resourceHandle;
        if (AddressablesUtils.HasResult(m_resourceHandle)) {
            var cliManagerPrefab = m_resourceHandle.Result;
            InstantiatePrefab(cliManagerPrefab);
            m_succeeded = true;
        } else {
            Debug.LogError($"Load prefab failed: {m_resourceHandle}, Exception: {m_resourceHandle.OperationException}");
            Addressables.Release(m_resourceHandle);
            m_resourceHandle = default;
        }
    }

    public override void Dispose() {
        if (m_resourceHandle.IsValid()) {
            Addressables.Release(m_resourceHandle);
            m_resourceHandle = default;
        }
    }

    protected virtual GameObject InstantiatePrefab(GameObject prefab) {
        return GameObject.Instantiate(prefab);
    }
}


}

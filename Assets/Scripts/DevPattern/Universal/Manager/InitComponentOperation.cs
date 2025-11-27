namespace DevPattern.Universal.Manager {
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class InitComponentOperation<T> : InitOperationBase
    where T : MonoBehaviour
{
    public readonly string objectName;
    protected GameObject   m_gameObject;
    public GameObject      gameObject => m_gameObject;

    public InitComponentOperation(string id, string objectName, HashSet<string> dependencies = null): base(id, dependencies) {
        this.objectName = objectName;
    }

    protected override IEnumerator DoRestart() {
        if (m_gameObject == null)
            m_gameObject = new GameObject(objectName);
        try {
            m_gameObject.AddComponent<T>();
            m_succeeded = true;
        } catch (Exception e) {
            Debug.LogError($"InitComponentOperation<{typeof(T).Name}> failed: {e}");
            m_succeeded = false;
            yield break;
        }
        yield return null;
    }
}


}

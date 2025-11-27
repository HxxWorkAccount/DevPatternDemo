namespace DevPattern.Universal.Manager {
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public abstract class InitOperationBase
    : IInitOperation,
      IDisposable
{
    protected readonly string m_id           = "";
    protected bool            m_succeeded    = false;
    HashSet<string>           m_dependencies = null;

    public InitOperationBase(string id, HashSet<string> dependencies = null) {
        m_id           = id;
        m_dependencies = dependencies;
        Debug.Assert(!string.IsNullOrEmpty(m_id), "InitPrefabOperation id is null or empty!");
    }

    /*====-------------- Interface --------------====*/

    public virtual string          id           => m_id;
    public virtual bool            succeeded    => m_succeeded;
    public virtual HashSet<string> dependencies => m_dependencies;

    public virtual IEnumerator Restart() {
        if (m_succeeded)
            throw new InvalidOperationException($"Can't restart operation, '{m_id}' has already succeeded!");
        m_succeeded = false;
        return DoRestart();
    }

    protected abstract IEnumerator DoRestart();
    public virtual void OnSuccess() { }
    public virtual void OnFailed() { }
    public virtual void Dispose() { }
}


}

namespace DevPattern.Universal.Manager {
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IInitOperation
{
    public string id { get; }
    public bool   succeeded { get; }
    public HashSet<string> dependencies { get; }  // 可为空，注意 InitOperationManager 依赖计数器来处理依赖达成条件，因此这里应始终返回相同结果

    public IEnumerator Restart();  // 支持失败后重启
    public void        OnSuccess();
    public void        OnFailed();
    // public void        OnDependenciesFailed(IReadOnlyList<string> depids);
}

}

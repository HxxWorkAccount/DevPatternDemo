namespace DevPattern.Universal.Manager {
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;
using DevPattern.Universal;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum InitOperationStatus {
    NotStarted,  // 未被启动
    Pending,     // 等待依赖完成
    InProgress,  // 正在运行
    Completed,   // 完成（状态锁定）
    Failed,      // 失败（可重起）
    Cancelled,   // 已取消（曾经注册过，但又注销了）（未来可能又重新注册）
}

public sealed class InitOperationManager : Singleton<InitOperationManager>
{
    private class InitOperationInfo
    {
        public IInitOperation      operation;
        public InitOperationStatus status;
        public int                 waitingCount;
    }

    private readonly HashSet<string> m_cancelledOperations                  = new();
    private readonly HashSet<string> m_completedOperations                  = new();
    private readonly Dictionary<string, InitOperationInfo> m_initOperations = new();

    /* 安全的 Handle 结果（防止重入错误） */
    private readonly UniqueQueue<string> m_tempPendingOperations = new();  // 临时存储等待启动的任务 ID 列表
    private bool                         m_handling              = false;

    /*====-------------- Life --------------====*/

    protected override void OnSingletonAwake() {
        StartCoroutine(LoadInitPrefabs());
    }

    private IEnumerator LoadInitPrefabs() {
        var                                    funcs   = GetInitPrefabsFuncs();
        List<AsyncOperationHandle<GameObject>> handles = new();
        foreach (var func in funcs) {
            var path = func();
            if (string.IsNullOrEmpty(path))
                continue;
            handles.Add(ResourceUtils.LoadAsset<GameObject>(path));
        }

        /* 批量等待 */
        foreach (var handle in handles) {
            yield return handle;
            try {
                if (handle.Status == AsyncOperationStatus.Succeeded) {
                    var prefab = handle.Result;
                    Instantiate(prefab, transform);
                } else {
                    Debug.LogWarning($"[GameInitializer] Load Init Prefab Failed: {handle}, Exception: {handle.OperationException}");
                }
            } catch (Exception e) {
                Debug.LogError($"[GameInitializer] Load Init Prefab Exception: {e}");
            }
        }

        /* 批量释放 Handles */
        foreach (var handle in handles) {
            Addressables.Release(handle);
        }
    }

    /*====-------------- Init Tasks Management --------------====*/
    // FIXIT == 如果某个任务 Failed 了（或始终无法完成），而又有任务依赖它，那么被依赖任务永远无法启动。而且这里不会做任何提醒，因为做提醒的复杂度非常高：
    // - [ ] 要处理很多边界情况，启动时就要检查（还要处理正在 handling 的情况，而且启动的以来错误和 handling 中的依赖错误可能不是一个）
    // - [ ] 要处理多个依赖失败的情况
    // - [ ] 消息提醒可能重入 Restart 和 Handling，导致各种复杂问题问题
    // 个人建议是注册者自己做一个超时计时器，就不增加这里的复杂度了（真的很复杂）

    public bool TryGetStatus(string id, out InitOperationStatus status) {
        if (m_completedOperations.Contains(id)) {
            status = InitOperationStatus.Completed;
            return true;
        }
        if (m_cancelledOperations.Contains(id)) {
            status = InitOperationStatus.Cancelled;
            return true;
        }
        if (m_initOperations.TryGetValue(id, out var operationInfo)) {
            status = operationInfo.status;
            return true;
        }
        status = default;
        return false;
    }

    public bool Register(IInitOperation operation) {
        if (m_completedOperations.Contains(operation.id) || m_initOperations.ContainsKey(operation.id))
            return false;
        // 计算依赖数量
        int waitingCount = operation.dependencies != null ? operation.dependencies.Count : 0;
        if (operation.dependencies != null) {
            foreach (var depId in operation.dependencies) {
                if (m_completedOperations.Contains(depId))
                    waitingCount--;
            }
        }
        var operationInfo =
            new InitOperationInfo { operation = operation, status = InitOperationStatus.NotStarted, waitingCount = waitingCount };
        m_initOperations.Add(operation.id, operationInfo);
        m_cancelledOperations.Remove(operation.id);
        return true;
    }

    public bool Unregister(string id) {
        /* 只能移除 NotStarted、Pending（有依赖未完成）和 Failed 状态的任务 */
        if (!m_initOperations.TryGetValue(id, out var operationInfo))
            return false;
        var status = operationInfo.status;
        if (status == InitOperationStatus.Completed || status == InitOperationStatus.InProgress ||
            status == InitOperationStatus.Cancelled || (status == InitOperationStatus.Pending && operationInfo.waitingCount == 0)) {
            return false;
        }
        m_cancelledOperations.Add(id);
        return m_initOperations.Remove(id);
    }

    public void Restart(string id) {
        if (m_completedOperations.Contains(id))
            throw new Exception($"[GameInitializer] RestartInitOperation Failed: Init Operation Already Completed: {id}");
        if (!m_initOperations.TryGetValue(id, out var operationInfo))
            throw new Exception($"[GameInitializer] RestartInitOperation Failed: Init Operation Not Found: {id}");
        if (operationInfo.status != InitOperationStatus.NotStarted && operationInfo.status != InitOperationStatus.Failed)
            throw new Exception(
                $"[GameInitializer] RestartInitOperation Failed: Init Operation Status Invalid: {id}, Status: {operationInfo.status}"
            );

        operationInfo.status = InitOperationStatus.Pending;

        /* 检查任务是否可以启动 */
        if (operationInfo.waitingCount == 0) {
            if (m_handling) {
                m_tempPendingOperations.Enqueue(id);
            } else {
                DoStartInitOperation(id);
            }
        }
    }

    private void HandleOperationFinished(string id) {
        bool prevHandling = m_handling;
        m_handling        = true;
        try {
            var operationInfo = m_initOperations[id];
            Debug.Assert(operationInfo.status == InitOperationStatus.Completed || operationInfo.status == InitOperationStatus.Failed);

            // 1. 完成后，收集状态。所有非 Completed 的任务都视为 Failed
            if (operationInfo.status == InitOperationStatus.Completed) {
                m_initOperations.Remove(id);
                m_completedOperations.Add(id);
                foreach (var kvp in m_initOperations) {
                    var opinfo       = kvp.Value;
                    var dependencies = opinfo.operation.dependencies;
                    if (dependencies != null && dependencies.Contains(id)) {
                        opinfo.waitingCount--;
                        if (opinfo.waitingCount == 0 && opinfo.status == InitOperationStatus.Pending) {
                            m_tempPendingOperations.Enqueue(kvp.Key);  // 缓存待启动的任务
                        }
                    }
                }
            }

            // 2. 触发 IInitOperation 的回调，这里可能触发 Register 和 Restart，甚至可能重入 HandleOperationFinished
            try {
                if (operationInfo.status == InitOperationStatus.Completed) {
                    operationInfo.operation.OnSuccess();
                } else {
                    operationInfo.operation.OnFailed();  // 在 Failed 中重启自己一定要小心递归！
                }
            } catch (Exception e) {
                Debug.LogError($"[GameInitializer] Init Operation OnSuccess/OnFailed Exception: {id}, Exception: {e}");
            }

            // 3. 启动 waitingCount == 0 但 Pending 的任务，这里可能触发 Register 和 Restart，甚至可能重入 HandleOperationFinished
            while (m_tempPendingOperations.TryDequeue(out var pendingId)) {
                if (TryGetStatus(pendingId, out var status) && status == InitOperationStatus.Pending)
                    DoStartInitOperation(pendingId);
            }
        } catch (Exception e) {
            Debug.LogError($"[GameInitializer] HandleOperationFinished Exception: {id}, Exception: {e}");
        }
        m_handling = prevHandling;
    }

    private void DoStartInitOperation(string id) {
        var operationInfo = m_initOperations[id];
        Debug.Assert(operationInfo.status == InitOperationStatus.Pending);
        Debug.Assert(operationInfo.waitingCount == 0);
        operationInfo.status = InitOperationStatus.InProgress;

        IEnumerator Coro() {
            IEnumerator operationCoro;
            try {
                operationCoro = operationInfo.operation.Restart();
            } catch (Exception e) {
                Debug.LogError($"[GameInitializer] Init Operation Restart Exception: {id}, Exception: {e}");
                operationInfo.status = InitOperationStatus.Failed;
                HandleOperationFinished(id);
                yield break;
            }

            yield return operationCoro;

            if (operationInfo.operation.succeeded) {
                operationInfo.status = InitOperationStatus.Completed;
            } else {
                operationInfo.status = InitOperationStatus.Failed;
            }
            HandleOperationFinished(id);
        }

        StartCoroutine(Coro());
    }

    /*====-------------- Utils --------------====*/

    private static List<Func<string>> GetInitPrefabsFuncs() {
        var env = EnvironmentUtils.env;
        /* 内置的列表 */
        List<Func<string>> funcs = new() {
            () => {
                if (env.isDev)
                    return "Assets/Dev/Content/Universal/Prefabs/DevInitializer.prefab";
                return "";
            },
            // () => {
            //     if (env.isDev && env.isServer)
            //         return "Assets/Dev/Content/Client/Prefabs/DevServerInitializer.prefab";
            //     return "";
            // },
            // () => {
            //     if (env.isDev && env.isClient)
            //         return "Assets/Dev/Content/Server/Prefabs/DevClientInitializer.prefab";
            //     return "";
            // },
        };
        return funcs;
    }
}

}

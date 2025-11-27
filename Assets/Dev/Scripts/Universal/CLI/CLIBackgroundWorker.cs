namespace DevPattern.Dev.Universal.CLI {
using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using Newtonsoft.Json;

/* CLIBackgroundWorker 功能：
 * - [x] CLIBackgroundWorker 由 CLIManager 负责启动，作为一个独立线程，引用 CLIManager 上两个的并发容器（输入和输出）
 * - [x] CLIBackgroundWorker 周期性检查并发容器，将"输出并发容器"内的 Log 转发至所有 Host
 * - [x] CLIBackgroundWorker 将 Host 的输出定向至"输入并发容器"
 * - */

// FIXIT == 发送频率和带宽优化
// - [ ] 要做一下发送频率、消息长度的限制？面对突然大量的 Log 和 Input 会发生什么？怎么处理？（譬如每个 client 做个定时器，时间限制内只能发一个命令，指向性的返回一个警告 Log？也可以在 CLI 上做相同的限制）
public class CLIBackgroundWorker
{
    public enum State {
        Running,
        Stopping,
        Stopped,
    }

    public struct IPAndPort
    {
        public string ip;
        public int    port;
    }

    private readonly ConcurrentQueue<CLIProtocol.Packet> m_inputQueue;
    private readonly ConcurrentQueue<CLIProtocol.Packet> m_outputQueue;
    private Thread                                       m_workerThread;
    private readonly TextWriter                          m_originaOut;

    private volatile bool m_running;  // 同时被 Unity 主线程和工作线程访问，需要设为 volatile
    private readonly ConcurrentQueue<CLIHostBase> m_pendingAddHosts    = new();  // 待注册的 Hosts 队列
    private readonly ConcurrentQueue<CLIHostBase> m_pendingRemoveHosts = new();  // 待移除的 Hosts 队列

    /* 线程数据，类成员访问时要注意线程安全！ */
    private readonly List<CLIHostBase> m_hosts = new();
    private Exception                  m_threadException;

    /*====-------------- Properties --------------====*/

    public bool  running     => m_running;
    public bool  threadAlive => m_workerThread != null && m_workerThread.IsAlive;
    public State workerState {
        get {
            if (running && threadAlive)
                return State.Running;
            else if (!running && threadAlive)
                return State.Stopping;
            else
                return State.Stopped;
        }
    }
    public Exception threadException => m_threadException;

    /*====-------------- Worker Management --------------====*/

    public CLIBackgroundWorker(
        ConcurrentQueue<CLIProtocol.Packet> inputQueue,
        ConcurrentQueue<CLIProtocol.Packet> outputQueue,
        TextWriter                          originalOut
    ) {
        m_inputQueue  = inputQueue;
        m_outputQueue = outputQueue;
        m_originaOut  = originalOut;
    }

    public void EnqueueHost(CLIHostBase host) {
        m_pendingAddHosts.Enqueue(host);
    }

    public void DequeueRemoteHost(CLIHostBase host) {
        m_pendingRemoveHosts.Enqueue(host);
    }

    public bool Start() {
        if (workerState != State.Stopped)
            return false;
        m_running      = true;
        m_workerThread = new Thread(WorkLoop) { IsBackground = true };
        m_workerThread.Start();
        return true;
    }

    public bool Stop() {
        m_running = false;
        if (m_workerThread != null && m_workerThread.IsAlive) {
            m_workerThread.Join(500);  // Join 是一个同步点，执行后就可以安全访问 Woker 的数据和函数了
            if (m_workerThread.IsAlive)
                return false;
        }

        // 清理 Host
        foreach (var host in m_hosts) {
            try {
                host.OnStop();
            } catch (Exception e) {
                PushRawLog($"Error when closing host '{host}': {e.Message}", LogType.Error);
            }
        }
        m_hosts.Clear();

        // 清理待处理 Host
        while (m_pendingAddHosts.TryDequeue(out var host)) {
            try {
                host.OnStop();
            } catch (Exception e) {
                PushRawLog($"Error when closing pending host '{host}': {e.Message}", LogType.Error);
            }
        }

        m_workerThread    = null;
        m_threadException = null;
        return true;
    }

    //====================== 线程逻辑 ======================//

    private void WorkLoop() {
        try {
            while (m_running) {
                // 处理待添加的 Host
                while (m_pendingAddHosts.TryDequeue(out var host)) {
                    TryAddHost(host);
                }

                // 处理输出 (Unity -> Hosts)
                while (m_outputQueue.TryDequeue(out var packet)) {
                    for (int i = m_hosts.Count - 1; i >= 0; i--) {
                        if (!m_hosts[i].IsAlive) {
                            PushRawLog($"Host '{m_hosts[i]}' is not alive, removing.", LogType.Warning);
                            RemoveHostAt(i);
                            continue;
                        }
                        try {
                            m_hosts[i].Send(packet);
                        } catch (Exception e) {
                            PushRawLog($"Error sending to host: {e.Message}", LogType.Error);
                            RemoveHostAt(i);
                        }
                    }
                }

                // 更新并处理输入 (Hosts -> Unity)
                for (int i = m_hosts.Count - 1; i >= 0; i--) {
                    var host = m_hosts[i];
                    if (!host.IsAlive) {
                        PushRawLog($"Host '{m_hosts[i]}' is not alive, removing.", LogType.Warning);
                        RemoveHostAt(i);
                        continue;
                    }
                    try {
                        host.OnUpdate();  // 更新 host
                        while (host.TryReceive(out var packet)) {
                            if (packet.type == MessageType.Disconnect) {
                                RemoveHostAt(i);
                                break;  // 断开连接后不处理后续消息
                            } else {
                                m_inputQueue.Enqueue(packet);
                            }
                        }
                    } catch (Exception e) {
                        PushRawLog($"Error updating host: {e.Message}", LogType.Error);
                        RemoveHostAt(i);
                    }
                }

                // 处理待移除的 Host
                while (m_pendingRemoveHosts.TryDequeue(out var hostToRemove)) {
                    int index = m_hosts.IndexOf(hostToRemove);
                    if (index >= 0) {
                        RemoveHostAt(index);
                    }
                }

                Thread.Sleep(10);
            }
        } catch (Exception e) {
            // 这里是最后兜底，线程遇到无法捕获的异常。处理策略：向 CLIManager 发送要给消息，然后终止线程（CLIManager 本身可能也要终止）
            m_threadException = e;
            PushRawLog($"Thread Crashed: {e.Message}\n{e.StackTrace}", LogType.Error);
        } finally {
            // 无论是否 crash，结束前都会清理
            foreach (var host in m_hosts) {
                try {
                    host.OnStop();
                } catch (Exception e) {
                    PushRawLog($"Error when closing host '{host}': {e.Message}", LogType.Error);
                }
            }
            m_hosts.Clear();
        }
    }

    // FIXIT == 考虑如何将 Add 和 Remove 消息传给 CLIManager，让 Unity 主线程能知道当前所有客户端的连接状态

    private void TryAddHost(CLIHostBase host) {
        try {
            host.OnStart(PushRawLog, m_originaOut);
        } catch (Exception e) {
            PushRawLog($"Failed to start host: {e.Message}", LogType.Error);
            TryStopHost(host);
            return;
        }
        m_hosts.Add(host);
    }

    private void RemoveHostAt(int index) {
        if (index >= 0 && index < m_hosts.Count) {
            TryStopHost(m_hosts[index]);
            m_hosts.RemoveAt(index);
        }
    }

    private void TryStopHost(CLIHostBase host) {
        try {
            host.OnStop();
        } catch (Exception e) {
            PushRawLog($"Failed to stop host: {e.Message}", LogType.Error);
        }
    }

    /*====-------------- Utils --------------====*/

    private void PushRawLog(string msg, LogType logType = LogType.Info) {
        /* 这部分消息不会再转发给 CLI，而是强制输出到 OriginalOut 和 Debug Log */
        CLIProtocol.Packet packet;
        switch (logType) {
        case LogType.Warning:
            packet = new CLIProtocol.Packet { type = MessageType.WorkerWarning, data = msg };
            break;
        case LogType.Error:
            packet = new CLIProtocol.Packet { type = MessageType.WorkerError, data = msg };
            break;
        default:
            packet = new CLIProtocol.Packet { type = MessageType.WorkerInfo, data = msg };
            break;
        }
        m_inputQueue.Enqueue(packet);
    }
}

}

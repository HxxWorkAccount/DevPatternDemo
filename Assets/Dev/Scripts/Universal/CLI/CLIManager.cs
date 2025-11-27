namespace DevPattern.Dev.Universal.CLI {
using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.InputSystem;
using DevPattern.Universal.Utils;
using DevPattern.Universal;
using DevPattern.Universal.Lua;
using System.Collections.Generic;

/* CLIManager 功能：
 * - [x] CLIManager 负责注册监听 Unity 的 Debug.Log，并将其内容发送至的输出并发容器
 * - [x] CLIManager 保管标准输出，CLIManager 将 Console.Out 重定向并接收至容纳 Log 的输出并发容器
 * - [x] 留意 CLIManager 的 OnEnable、OnDisable、OnDestroy 实现
 * - [x] CLIManager 在 Update 中周期性检查"输入并发容器"
 * - [x] 监听一个组合按键，可以换出 CLI 界面
 * - [ ] // FIXIT == 有空把标准错误 Console.Error 也重定向了（注意 ServerCLI 里直接取了 Console.Error，如果重定向则要缓存一下，最好创建一个 CLIWorkerContext 之类的东西整合一下）
 * - */

class CLIManager : Singleton<CLIManager>
{
    public const string k_ignoreMark         = "@@@ ";
    public const string k_serverCommandStart = "$";
    public const string k_clientCommandStart = "$$";

    // 用于重定向 Console.Out，写入 outputQueue，仅当遇到 \n 时才发送消息（或者 Flush 强制发送）
    private class CLIConsoleWriter : TextWriter
    {
        private readonly StringBuilder m_lineBuffer = new();
        private readonly ConcurrentQueue<CLIProtocol.Packet> m_outputQueue;
        private readonly TextWriter                          m_originaOut;
        private readonly object                              m_lock = new();

        // 所有权还是再 CLIManager，这里只是引用
        public CLIConsoleWriter(ConcurrentQueue<CLIProtocol.Packet> queue, TextWriter original) {
            m_outputQueue = queue;
            m_originaOut  = original;
        }

        public override Encoding Encoding => m_originaOut != null ? m_originaOut.Encoding : Encoding.UTF8;

        public override void Write(char value) {
            lock (m_lock) {
                if (value == '\n') {
                    FlushBuffer();
                } else {
                    m_lineBuffer.Append(value);
                }
            }
        }

        public override void Write(string value) {
            if (string.IsNullOrEmpty(value))
                return;
            lock (m_lock) {
                // 检查是否包含换行符，如果包含则需要分割处理
                int lastIndex = 0;
                int newlineIndex;
                while ((newlineIndex = value.IndexOf('\n', lastIndex)) != -1) {
                    // 将换行符之前的内容追加到 buffer
                    m_lineBuffer.Append(value, lastIndex, newlineIndex - lastIndex);
                    // 遇到换行符，立即发送当前 buffer
                    FlushBuffer();
                    lastIndex = newlineIndex + 1;
                }

                // 将剩余部分追加到 buffer
                if (lastIndex < value.Length) {
                    m_lineBuffer.Append(value, lastIndex, value.Length - lastIndex);
                }
            }
        }

        public override void WriteLine(string value) {
            if (value == null)
                return;
            lock (m_lock) {
                if (m_lineBuffer.Length > 0) {
                    m_lineBuffer.Append(value);
                    FlushBuffer();
                } else {
                    EnqueuePacket(value);  // 如果 buffer 为空，直接发送，避免一次 StringBuilder 拷贝
                }
            }
        }

        public override void Flush() {
            lock (m_lock) {
                if (m_lineBuffer.Length > 0) {
                    FlushBuffer();
                }
            }
        }

        /*====-------------- Utils --------------====*/

        private void FlushBuffer() {
            if (m_lineBuffer.Length > 0 && m_lineBuffer[m_lineBuffer.Length - 1] == '\r')
                m_lineBuffer.Length--;  // 去掉末尾的 \r
            if (m_lineBuffer.Length == 0)
                return;
            string content = m_lineBuffer.ToString();
            m_lineBuffer.Clear();
            EnqueuePacket(content);
        }

        private void EnqueuePacket(string message) {
            if (string.IsNullOrEmpty(message))
                return;
            if (message.StartsWith("[Error]", StringComparison.OrdinalIgnoreCase)) {
                var packet = CLIProtocol.LogMessage.Pack(LogType.Error, message);
                m_outputQueue.Enqueue(packet);
            } else if (message.StartsWith("[Warning]", StringComparison.OrdinalIgnoreCase)) {
                var packet = CLIProtocol.LogMessage.Pack(LogType.Warning, message);
                m_outputQueue.Enqueue(packet);
            } else {
                var packet = CLIProtocol.LogMessage.Pack(LogType.Info, message);
                m_outputQueue.Enqueue(packet);
            }
        }
    }

    /*====-------------- Member --------------====*/

    // [SerializeField]
    [SerializeField]
    private InputActionAsset m_inputActionAsset;
    private InputAction      m_openCLIAction;

    public bool testLog = false;
    [Range(0.1f, 10f)]
    public float  testLogInterval   = 3f;
    private float m_tempLogInterval = 0f;

    private readonly ConcurrentQueue<CLIProtocol.Packet> m_inputQueue = new();  // 存储来自 CLI 发的协议（json 格式）
    private readonly ConcurrentQueue<CLIProtocol.Packet> m_outputQueue = new();  // 存放来自 Unity 向 CLI 发送的协议（json 格式）
    private CLIBackgroundWorker m_backgroundWorker;
    private TextWriter          m_originalOut;
    private bool                m_started = false;

    private List<Func<string, bool>> m_clientCommandHandlers = new();
    private List<Func<string, bool>> m_serverCommandHandlers = new();

    /*====-------------- Life --------------====*/

    protected override void OnSingletonInit() {
        m_originalOut      = Console.Out;
        m_backgroundWorker = new CLIBackgroundWorker(m_inputQueue, m_outputQueue, m_originalOut);
        m_openCLIAction    = m_inputActionAsset.FindAction("Open CLI", throwIfNotFound: false);

        if (EnvironmentUtils.env.IsDedicatedServer) {
            Application.SetStackTraceLogType(UnityEngine.LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(UnityEngine.LogType.Warning, StackTraceLogType.None);
        }

        // 注册一下统一命令处理
        RegisterClientCommandHandler(UniversalCommandHandler.HandleCommand);
        RegisterServerCommandHandler(UniversalCommandHandler.HandleCommand);
    }

    protected virtual void OnEnable() {
        if (m_backgroundWorker.Start()) {
            // 重定向 Console.Out
            Console.SetOut(new CLIConsoleWriter(m_outputQueue, m_originalOut));
            // 监听 Unity Log，使用 Threaded 版本，以避免主线程阻塞
            Application.logMessageReceivedThreaded += HandleUnityLog;
            m_started = true;

            // 注册默认的 CLI（由 Worker 负责触发 OnStop 来释放，这里不持有，所以不需要取消注册）
            RegisterDefaultCLI();
            if (m_openCLIAction != null)
                m_openCLIAction.performed += OnOpenCLI;
        } else {
            RawLog("[CLI Manager] Failed to start background worker thread.", LogType.Error);
        }
    }

    protected override void OnSingletonDisable() {
        if (m_started) {
            if (m_originalOut != null)  // 恢复 Console.Out
                Console.SetOut(m_originalOut);
            Application.logMessageReceivedThreaded -= HandleUnityLog;  // 取消注册 Unity Log Callback
            m_started = false;
        }
        m_backgroundWorker.Stop();
        // 停止后还要再处理一趟 inputQueue，检查最后的异常信息
        while (m_inputQueue.TryDequeue(out CLIProtocol.Packet packet))
            ProcessInput(packet, true);
        m_inputQueue.Clear();
        m_outputQueue.Clear();
        if (m_openCLIAction != null)
            m_openCLIAction.performed += OnOpenCLI;
    }

    protected override void OnSingletonDestroy() {
        m_backgroundWorker = null;
    }

    private void Update() {
        /* 尝试读取输入 */
        if (m_backgroundWorker.workerState == CLIBackgroundWorker.State.Running) {
            while (m_inputQueue.TryDequeue(out CLIProtocol.Packet packet)) {
                ProcessInput(packet);
            }
        } else {
            RawLog($"[CLI Manager] worker thread is not running: {m_backgroundWorker.workerState}", LogType.Error);
            enabled = false;
        }

        /* 测试 Log */
        if (testLog) {
            if (m_tempLogInterval > testLogInterval) {
                Debug.Log("This is a test log message~ ( •̀ .̫ •́ )✧");
                Console.WriteLine("This is a test console output~ ψ(｀∇´)ψ");
                Console.Write("test console 'write' output~ (¬‿¬)");
                Console.Write("\n[WARNING] test warning");
                Console.Write("\r\n");
                m_tempLogInterval = 0f;
            } else {
                m_tempLogInterval += Time.unscaledDeltaTime;
            }
        }
    }

    /*====-------------- 输入输出处理 --------------====*/

    private void ProcessInput(CLIProtocol.Packet packet, bool onlyError = false) {
        try {
            switch (packet.type) {
            case MessageType.Input:
                if (onlyError)
                    break;
                string inputMsg = packet.data;
                if (inputMsg.StartsWith(k_clientCommandStart)) {  // 必须先检查 client
                    ProcessClientCommand(inputMsg.Substring(k_clientCommandStart.Length));
                } else if (inputMsg.StartsWith(k_serverCommandStart)) {
                    ProcessServerCommand(inputMsg.Substring(k_serverCommandStart.Length));
                } else {  // Lua 代码
                    ProcessLuaInput(inputMsg);
                }
                break;
            case MessageType.WorkerInfo:
                RawLog($"[CLI Worker] {packet.data}", LogType.Info);
                break;
            case MessageType.WorkerWarning:
                RawLog($"[CLI Worker Warning] {packet.data}", LogType.Warning);
                break;
            case MessageType.WorkerError:
                RawLog($"[CLI Worker Error] {packet.data}", LogType.Error);
                break;
            }
        } catch (Exception e) {
            RawLog($"[CLI Error] Parse input packet failed: {e.Message}");
        }
    }

    // 注：这里如果遇到命令平台与当前平台不一致的情况，要通过协议来发送命令
    // 注：这里只允许注册不允许注销，避免复杂的回调重入问题
    public bool RegisterClientCommandHandler(Func<string, bool> handler) {
        if (m_clientCommandHandlers.Contains(handler))
            return false;
        m_clientCommandHandlers.Add(handler);
        return true;
    }
    public void ProcessClientCommand(string command) {
        if (EnvironmentUtils.env.IsDedicatedServer) {
            RawLog($"Can't run client command on dedicated server: {command}");
        } else {
            ProcessCommandHelper(m_clientCommandHandlers, command);
        }
    }
    public bool RegisterServerCommandHandler(Func<string, bool> handler) {
        if (m_serverCommandHandlers.Contains(handler))
            return false;
        m_serverCommandHandlers.Add(handler);
        return true;
    }
    public void ProcessServerCommand(string command) {
        if (EnvironmentUtils.env.IsDedicatedClient) {
            RawLog($"Can't run server command on pure client: {command}");
        } else {
            ProcessCommandHelper(m_serverCommandHandlers, command);
        }
    }
    private void ProcessCommandHelper(List<Func<string, bool>> handlers, string command) {
        bool handled = false;
        foreach (var handler in handlers) {
            try {
                if (handler(command)) {
                    handled = true;
                    break;  // 已处理，跳出
                }
            } catch (Exception e) {
                RawLog($"[CLI Command] Handler command '{command}' failed: {e.Message}", LogType.Error);
                handled = true;
                break;  // 遇到异常就当是匹配到命令了
            }
        }
        if (!handled) {
            RawLog($"[CLI Command] Unknown command: {command}", LogType.Warning);
        }
    }

    public void ProcessLuaInput(string luaCode) {
        if (LuaBridge.instance.luaCLIHelper != null) {
            LuaBridge.instance.luaCLIHelper.ExecuteLuaCode(luaCode);
        } else {
            RawLog("LuaCLIHelper is not set!", LogType.Error);
        }
    }

    /* 注意！该函数不在 Unity 主线程执行 */
    private void HandleUnityLog(string logString, string stackTrace, UnityEngine.LogType type) {
        if (logString.StartsWith(k_ignoreMark))  // 部分 Log 可跳过
            return;
        LogType cliLogType = LogType.Info;
        bool    needStack  = false;
        switch (type) {
        case UnityEngine.LogType.Error:
        case UnityEngine.LogType.Assert:
        case UnityEngine.LogType.Exception:
            needStack  = true;
            cliLogType = LogType.Error;
            break;
        case UnityEngine.LogType.Warning:
            cliLogType = LogType.Warning;
            break;
        }
        var packet = CLIProtocol.LogMessage.Pack(cliLogType, logString, needStack ? stackTrace : "");
        m_outputQueue.Enqueue(packet);
    }

    public void RawLog(string msg, LogType logType = LogType.Info) {
        /* 跳过 CLI 广播，直接在 Unity Console 或 Console.Out 上输出
           注意，仅当工作线程出现影响传输的异常时，才应使用该函数 */
#if UNITY_EDITOR
        switch (logType) {
        case LogType.Info:
            Debug.Log($"{k_ignoreMark} {msg}");
            break;
        case LogType.Warning:
            Debug.LogWarning($"{k_ignoreMark} {msg}");
            break;
        case LogType.Error:
            Debug.LogError($"{k_ignoreMark} {msg}");
            break;
        }
#else
        switch (logType) {
        case LogType.Info:
            m_originalOut?.WriteLine(msg);
            break;
        case LogType.Warning:
            m_originalOut?.WriteLine($"[WARNING] {msg}");
            break;
        case LogType.Error:
            m_originalOut?.WriteLine($"[ERROR] {msg}");
            break;
        }
#endif
    }

    /*====-------------- 注册 CLI Host --------------====*/

    public void RegisterConnection(CLIRemoteHost remoteHost) {
        m_backgroundWorker.EnqueueHost(remoteHost);
    }

    public void UnregisterConnection(CLIRemoteHost remoteHost) {
        m_backgroundWorker.DequeueRemoteHost(remoteHost);
    }

    private void RegisterDefaultCLI() {
        Debug.Log($"Try Register Default CLI {EnvironmentUtils.env.IsDedicatedServer}");
        if (EnvironmentUtils.env.IsDedicatedServer) {  // 启用 Local CLI
            // DS 也用 ClientCLI 得了（创建本地进程交互），Server 专用的模式写起来有点麻烦
            RegisterConnection(new ClientCLI());
        } else {  // Client, Host，启用 OS 的图形 CLI
            RegisterConnection(new ClientCLI());
        }
    }

    private void OnOpenCLI(InputAction.CallbackContext context) {
        Debug.Log("Try Open CLI Window");
        if (EnvironmentUtils.env.IsDedicatedServer) {
            RegisterConnection(new ClientCLI());
        } else {
            RegisterConnection(new ClientCLI());
        }
    }
}

}

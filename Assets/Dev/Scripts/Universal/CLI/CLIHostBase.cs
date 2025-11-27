namespace DevPattern.Dev.Universal.CLI {
using System;
using System.IO;
using Newtonsoft.Json;

public abstract class CLIHostBase
{
    /// <summary>当 Host 被 Worker 启动时调用</summary>
    public abstract void OnStart(Action<string, LogType> logger, TextWriter originalOut);
    /// <summary>当 Host 被 Worker 停止时调用</summary>
    public abstract void OnStop();
    /// <summary>Worker 循环中调用，发生在 Send 后，TryReceive 前</summary>
    public abstract void OnUpdate();

    public abstract bool IsAlive { get; }  // 若为 false，则 Worker 会移除该 Host

    /// <summary>尝试获取发给 Unity 的输入</summary>
    public abstract bool TryReceive(out CLIProtocol.Packet packet);
    /// <summary>收到 Unity 的输出</summary>
    public abstract void Send(CLIProtocol.Packet packet);

    public static bool TryUnpack(string packet, out CLIProtocol.Packet result) {
        result = default;
        try {
            result = JsonConvert.DeserializeObject<CLIProtocol.Packet>(packet);
            return true;
        } catch {
            return false;
        }
    }
}

}

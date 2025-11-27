namespace DevPattern.Dev.Universal.CLI {
using System;
using Newtonsoft.Json;

// FIXIT == 有空要做一下优化，现在代码里里太多临时对象了，虽然是开发版本采用的功能，但还是得考虑一下 GC 性能吧
// - [ ] 是否有更高层的通信、连接封装可以用，现在直接裸 TCP 通信
// - [ ] 用池化、缓存来解决里面的一些临时对象问题
// - [ ] Newtonsoft.Json 性能也挺一般的，考虑用其他序列化方式（最好是能基于静态反射的）

[Serializable]
public enum MessageType {
    Log,         // data: LogMessage Json
    Handshake,   // data: HandshakeMessage Json
    Disconnect,  // data: reason string
    Input,       // data: input string

    WorkerInfo,     // data: msg string
    WorkerWarning,  // data: msg string
    WorkerError,    // data: msg string
}

[Serializable]
public enum LogType {
    Info,
    Warning,
    Error,
}

public static class CLIProtocol
{
    [Serializable]
    public struct Packet
    {
        public MessageType type;
        public string      data;
    }

    [Serializable]
    public struct HandshakeMessage
    {  // 由 CLIBackgroundWorker 发送给刚建立连接的 client，可以传递 Unity 运行时的一些信息
        public string deviceId;
        public string platform;

        public static Packet Pack(string deviceId, string platform) {
            return new Packet {
                type = MessageType.Handshake,
                data = JsonConvert.SerializeObject(new HandshakeMessage { deviceId = deviceId, platform = platform })
            };
        }
    }

    [Serializable]
    public struct LogMessage
    {  // Unity 发出的 Log
        public LogType logType;
        public long    timestamp;
        public string  content;
        public string  stackTrace;

        public static LogMessage CreateCurrentLog(LogType type, string content, string stackTrace = "") {
            return new LogMessage {
                logType = type, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), content = content, stackTrace = stackTrace
            };
        }

        public static Packet Pack(LogType type, string content, string stackTrace = "") {
            var logMsg = CreateCurrentLog(type, content, stackTrace);
            return new Packet { type = MessageType.Log, data = JsonConvert.SerializeObject(logMsg) };
        }
    }
}

}

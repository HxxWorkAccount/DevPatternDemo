namespace DevPattern.Dev.Universal.CLI {
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CLIRemoteHost : CLIHostBase
{
    public const int k_bufferSize = 8192;

    protected string                  m_ip;
    protected int                     m_port;
    protected TcpClient               m_client;
    protected NetworkStream           m_stream;
    protected Action<string, LogType> m_logger;

    private readonly byte[] m_readBuffer     = new byte[k_bufferSize];
    private readonly char[] m_charBuffer     = new char[k_bufferSize];
    private readonly StringBuilder m_buffer  = new();
    private readonly Decoder       m_decoder = Encoding.UTF8.GetDecoder();

    private readonly Queue<CLIProtocol.Packet> m_receivedPackets = new();

    public string        ip      => m_ip;
    public int           port    => m_port;
    public override bool IsAlive => m_client != null && m_client.Connected;

    public override string ToString() {
        return $"CLIRemoteHost({m_ip}:{m_port})";
    }

    /*====-------------- Life --------------====*/

    public CLIRemoteHost(string ip, int port) {
        m_ip   = ip;
        m_port = port;
    }

    public override void OnStart(Action<string, LogType> logger, TextWriter originalOut) {
        m_logger             = logger;
        m_client             = new TcpClient();
        m_client.SendTimeout = 2000;  // 2 秒发送超时
        var result           = m_client.BeginConnect(m_ip, m_port, null, null);
        // FIXIT == 这是个阻塞函数，但我不想用 ConnectAsync(...).ContinueWith，因为回调不在当前线程上下文运行。处于简单考虑，这里暂时直接同步等待
        //          对于某些情况来说 2 秒可能不足以建立连接（网络差或距离远），总之之后再处理吧。。。
        bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
        if (success) {
            m_client.EndConnect(result);
            m_stream = m_client.GetStream();
            Send(CLIProtocol.HandshakeMessage.Pack(System.Environment.MachineName, UnityEngine.Application.platform.ToString()));
            logger($"CLIRemoteHost connected to {m_ip}:{m_port}.", LogType.Info);
        } else {
            m_client.Close();
            m_client = null;
            throw new Exception($"CLIRemoteHost failed to connect to {m_ip}:{m_port}.");
        }
    }

    public override void OnStop() {
        m_client?.Close();
        m_client = null;
    }

    public override void OnUpdate() {
        if (!IsAlive)
            return;

        if (m_stream.DataAvailable) {
            int bytesRead = m_stream.Read(m_readBuffer, 0, m_readBuffer.Length);
            if (bytesRead > 0) {
                int charCount = m_decoder.GetChars(m_readBuffer, 0, bytesRead, m_charBuffer, 0);
                m_buffer.Append(m_charBuffer, 0, charCount);
                ProcessBuffer();
            }
        }
    }

    private void ProcessBuffer() {
        string[] jsons = m_buffer.ToString().Split('\n');
        m_buffer.Clear();

        // 粘包处理
        int count = jsons.Length;
        if (count > 0 && jsons[count - 1] != String.Empty) {
            m_buffer.Append(jsons[count - 1]);  // 最后一个不完整，重新压回缓冲区
            count--;
        }

        for (int j = 0; j < count; j++) {
            string json = jsons[j];
            if (TryUnpack(json, out CLIProtocol.Packet packet)) {
                m_receivedPackets.Enqueue(packet);
            }
        }
    }

    public override void Send(CLIProtocol.Packet packet) {
        if (m_client != null && m_client.Connected) {
            try {
                string json  = JsonConvert.SerializeObject(packet) + "\n";
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                m_stream.Write(bytes, 0, bytes.Length);  // 可能会超时报错，但好过阻塞
            } catch (Exception e) {
                m_logger($"CLIRemoteHost send packet failed: {e.Message}", LogType.Error);
            }
        }
    }

    public override bool TryReceive(out CLIProtocol.Packet packet) {
        if (m_receivedPackets.Count > 0) {
            packet = m_receivedPackets.Dequeue();
            return true;
        }
        packet = default;
        return false;
    }
}

}

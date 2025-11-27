namespace DevPattern.Dev.Universal.CLI {
using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

public class ServerCLI : PromptToolkitCLI
{
    public const int k_port = 57680;  // 随便捏个数

    private TextWriter m_originalOut;

    public override string cliName => "Server CLI";

    public ServerCLI(): base(k_port) { }

    public override void OnStart(Action<string, LogType> logger, TextWriter originalOut) {
        m_originalOut = originalOut;
        base.OnStart(logger, originalOut);
    }

    protected override void AfterProcessStarted() {
        if (m_process.StartInfo.RedirectStandardOutput) {
            m_process.OutputDataReceived += (sender, e) => {
                if (e.Data != null) {
                    m_originalOut.WriteLine(e.Data);
                }
            };
            m_process.BeginOutputReadLine();
        }
    }

    public override ProcessStartInfo GetProcessStartInfo() {
        return new ProcessStartInfo {
            FileName               = m_executablePath,
            Arguments              = $"--ip {m_ip} --port {m_port}",
            UseShellExecute        = false,
            CreateNoWindow         = true, // 直接在 Unity 进程上输出
            RedirectStandardInput  = false,
            RedirectStandardOutput = true, // 重定向，因为 Console.Out 已经被重定向了
            RedirectStandardError  = false,
        };
    }
}

}

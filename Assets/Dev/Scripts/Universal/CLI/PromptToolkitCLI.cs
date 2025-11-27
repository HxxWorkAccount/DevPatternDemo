namespace DevPattern.Dev.Universal.CLI {
using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

public abstract class PromptToolkitCLI : CLIRemoteHost
{
    protected Process m_process;

    protected string     m_executablePath;
    public bool          IsProcessAlive => m_process != null && !m_process.HasExited;
    public override bool IsAlive        => base.IsAlive && IsProcessAlive;

    public abstract string cliName { get; }

    public PromptToolkitCLI(int port): base(IPAddress.Loopback.ToString(), port) {
        // 在主线程构造时获取路径，避免在后台线程访问 Application.dataPath 导致异常
        m_executablePath = GetPythonExecutablePath();
        UnityEngine.Debug.Log($"[{cliName}] Executable path: {m_executablePath}");
    }

    /*====-------------- Life --------------====*/

    public override void OnStart(Action<string, LogType> logger, TextWriter originalOut) {
        m_logger = logger;
        if (!File.Exists(m_executablePath)) {
            logger($"Invalid CLI executable path: {m_executablePath}", LogType.Warning);
            return;
        }

        var startInfo = GetProcessStartInfo();
        try {
            m_process = Process.Start(startInfo);
            logger($"process {m_process.Id} started.", LogType.Info);
        } catch (Exception e) {
            throw new Exception($"Failed to start process at '{m_executablePath}': {e.Message}");  // 继续抛出，中断启动流程
        }

        Thread.Sleep(2000);  // 等待一会让进程启动起来（不会卡主线程）

        if (m_process != null) {
            // m_process.Refresh();
            logger(
                $"[{cliName}] Process Check - ID: {m_process.Id}, HasExited: {m_process.HasExited}, Responding: {m_process.Responding}",
                LogType.Info
            );
        }

        // 检查进程是否启动成功，若失败则杀死进程然后抛出错误
        if (!IsProcessAlive) {
            string exitCode = "unknown";
            try {
                exitCode = m_process.ExitCode.ToString();
            } catch { }
            // 无法读取 ErrorOutput 了，因为没有重定向。请直接查看弹出的 CLI 窗口中的报错信息。
            throw new Exception(
                $"Failed to start CLIProcessor. Process exited early. ExitCode: {exitCode}. Please check the console window for errors."
            );
        }

        AfterProcessStarted();

        base.OnStart(logger, originalOut);
        logger($"process {m_process.Id} started.", LogType.Info);
    }

    protected virtual void AfterProcessStarted() { }

    public override void OnStop() {
        base.OnStop();
        if (IsProcessAlive) {
            try {
                m_process.Kill();
                m_logger($"process {m_process.Id} killed.", LogType.Info);
            } catch (Exception e) {
                m_logger($"Failed to kill CLIProcessor: {e.Message}", LogType.Error);
            }
        }
    }

    /*====-------------- Utils --------------====*/

    public virtual ProcessStartInfo GetProcessStartInfo() {  // 默认实现适用于 Windows 和 MacOS 的一般版本
        return new ProcessStartInfo {
            FileName               = m_executablePath,  //
            Arguments              = $"--ip {m_ip} --port {m_port}",
            UseShellExecute        = true,
            CreateNoWindow         = false,  // 弹出黑框！
            RedirectStandardOutput = false,
            RedirectStandardError  = false,
            // WorkingDirectory       = Path.GetDirectoryName(m_executablePath)
        };
    }
    
    public static string GetPythonExecutablePath() {
#if UNITY_EDITOR_WIN
        return $"{Application.dataPath}/Dev/Tools/Windows/CLIProcessor/CLIProcessor.exe";
#elif UNITY_EDITOR_OSX
        return $"{Application.dataPath}/Dev/Tools/MacOS/CLIProcessor/CLIProcessor";
#elif UNITY_STANDALONE_WIN
        return $"{Application.streamingAssetsPath}/CLIProcessor.exe";
#elif UNITY_STANDALONE_OSX
        return $"{Application.streamingAssetsPath}/CLIProcessor";
#else
        throw new NotImplementedException("Unsupported platform for getting Python executable path.");
#endif
    }
}

}

namespace DevPattern.Editor.Build {
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildUtils
{
    public static BuildTarget ToUnityTarget(ClientBuildTarget target) {
        switch (target) {
        case ClientBuildTarget.MacOS:
            return BuildTarget.StandaloneOSX;
        case ClientBuildTarget.Windows:
            return BuildTarget.StandaloneWindows64;
        case ClientBuildTarget.Linux:
            return BuildTarget.StandaloneLinux64;
        case ClientBuildTarget.iOS:
            return BuildTarget.iOS;
        case ClientBuildTarget.Android:
            return BuildTarget.Android;
        case ClientBuildTarget.WebGL:
            return BuildTarget.WebGL;
        case ClientBuildTarget.WSA:
            return BuildTarget.WSAPlayer;
        case ClientBuildTarget.PS4:
            return BuildTarget.PS4;
        case ClientBuildTarget.PS5:
            return BuildTarget.PS5;
        case ClientBuildTarget.XboxOne:
            return BuildTarget.XboxOne;
        case ClientBuildTarget.tvOS:
            return BuildTarget.tvOS;
        case ClientBuildTarget.Switch:
            return BuildTarget.Switch;
        case ClientBuildTarget.Stadia:
            return BuildTarget.Stadia;
        case ClientBuildTarget.VisionOS:
            return BuildTarget.VisionOS;
        }
        return BuildTarget.NoTarget;
    }

    public static BuildTarget ToUnityTarget(ServerBuildTarget target) {
        switch (target) {
        case ServerBuildTarget.MacOS:
            return BuildTarget.StandaloneOSX;
        case ServerBuildTarget.Windows:
            return BuildTarget.StandaloneWindows64;
        case ServerBuildTarget.Linux:
            return BuildTarget.StandaloneLinux64;
        case ServerBuildTarget.LinuxHeadlessSimulation:
            return BuildTarget.LinuxHeadlessSimulation;
        case ServerBuildTarget.EmbeddedLinux:
            return BuildTarget.EmbeddedLinux;
        }
        return BuildTarget.NoTarget;
    }

    public static BuildTargetGroup ToUnityTargetGroup(ClientBuildTarget target) {
        if (target == ClientBuildTarget.Windows || target == ClientBuildTarget.MacOS || target == ClientBuildTarget.Linux)
            return BuildTargetGroup.Standalone;
        return (BuildTargetGroup)target;
    }
    public static BuildTargetGroup ToUnityTargetGroup(ServerBuildTarget target) {
        if (target == ServerBuildTarget.Windows || target == ServerBuildTarget.MacOS || target == ServerBuildTarget.Linux)
            return BuildTargetGroup.Standalone;
        return (BuildTargetGroup)target;
    }

    public static void CopyDirectory(string sourceDir, string destinationDir) {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        // 创建目标目录
        Directory.CreateDirectory(destinationDir);

        // 复制所有文件
        foreach (FileInfo file in dir.GetFiles()) {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // 递归复制子目录
        foreach (DirectoryInfo subDir in dir.GetDirectories()) {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public static bool IsLinux(ServerBuildTarget target) {
        return target == ServerBuildTarget.Linux || target == ServerBuildTarget.LinuxHeadlessSimulation ||
               target == ServerBuildTarget.EmbeddedLinux;
    }
}

}

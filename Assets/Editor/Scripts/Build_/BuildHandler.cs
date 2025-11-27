namespace DevPattern.Editor.Build {
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public sealed class BuildControlReportHook
    : IPostprocessBuildWithReport,
      IPreprocessBuildWithReport
{
    private struct BuildResult
    {
        public BuildReport report;
        public string      buildDir;

        public bool buildSuccess =>
            report.summary.result != UnityEditor.Build.Reporting.BuildResult.Failed &&
            report.summary.result != UnityEditor.Build.Reporting.BuildResult.Cancelled;
        public bool   buidExists         => File.Exists(report.summary.outputPath);
        public string buildName          => Path.GetFileNameWithoutExtension(report.summary.outputPath);
        public string buildNameEx        => Path.GetFileName(report.summary.outputPath);
        public string buildDirName       => Path.GetFileNameWithoutExtension(report.summary.outputPath);
        public string dataDir            => Path.Combine(buildDir, $"{buildName}_Data");
        public string streamingAssetsDir => Path.Combine(buildDir, $"{buildName}_Data", "StreamingAssets");

        public BuildResult(BuildReport report) {
            this.report = report;
            // report.summary.outputPath 对于独立平台构建是可执行文件的路径，对于其他平台（如 WebGL）是输出目录的路径。
            if (File.Exists(report.summary.outputPath)) {
                buildDir = Path.GetDirectoryName(report.summary.outputPath);
            } else if (Directory.Exists(report.summary.outputPath)) {
                buildDir = report.summary.outputPath;
            } else {
                buildDir = "";
            }
        }
    }

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) {
        Debug.Log($"BuildControl: Preprocess report for '{report.summary.platform}'");
    }

    public void OnPostprocessBuild(BuildReport report) {
        Debug.Log($"BuildControl: Postprocess report result '{report.summary.result}'");
        BuildResult buildResult = new(report);
        TryMoveCLIProcesser(buildResult);
        TryBackup(buildResult);
    }

    private void TryMoveCLIProcesser(BuildResult buildResult) {
        var buildControlWindow = BuildControlWindow.GetWindowInstance();
        var settings           = buildControlWindow.settings;

        if (buildResult.buildSuccess && settings.buildType != BuildType.Release) {
            string processorPath;
            string outputPath;
            if (BuildUtils.IsLinux(settings.serverTarget)) {
                processorPath = $"{Application.dataPath}/Dev/Tools/Linux/CLIProcessor/CLIProcessor";
                outputPath    = $"{buildResult.streamingAssetsDir}/CLIProcessor";
            } else if (settings.clientTarget == ClientBuildTarget.Windows) {
                processorPath = $"{Application.dataPath}/Dev/Tools/Windows/CLIProcessor/CLIProcessor.exe";
                outputPath    = $"{buildResult.streamingAssetsDir}/CLIProcessor.exe";
            } else {
                processorPath = $"{Application.dataPath}/Dev/Tools/MacOS/CLIProcessor/CLIProcessor";
                outputPath    = $"{buildResult.streamingAssetsDir}/CLIProcessor";
            }

            if (string.IsNullOrEmpty(processorPath)) {
                Debug.Log($"BuildControl: CLIProcessor not found for the target platform, skipping move: ${processorPath}");
                return;
            }

            Debug.Log($"Moving CLIProcessor to streamingassets folder.");
            string streamingAssetsDir = buildResult.streamingAssetsDir;
            if (!Directory.Exists(streamingAssetsDir)) {
                Directory.CreateDirectory(streamingAssetsDir);
            }
            File.Copy(processorPath, outputPath, true);
        }
    }

    private void TryBackup(BuildResult buildResult) {
        var buildControlWindow = BuildControlWindow.GetWindowInstance();

        // 备份处理
        if (buildControlWindow.backupBuildArtifact) {
            if (buildResult.buildSuccess) {
                Debug.LogWarning("BuildControl: Build failed or cancelled, skipping backup.");
                return;
            }
            string sourceDirectory = buildResult.buildDir;
            if (String.IsNullOrEmpty(sourceDirectory)) {
                Debug.LogError($"BuildControl: Build output path not found at '{sourceDirectory}'. Backup failed.");
                return;
            }

            try {
                string timestamp            = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string buildsDirectory      = Directory.GetParent(sourceDirectory).FullName;
                string destinationDirectory = Path.Combine(buildsDirectory, $"{Path.GetFileName(sourceDirectory)}_{timestamp}");

                Debug.Log($"BuildControl: Backing up build from '{sourceDirectory}' to '{destinationDirectory}'...");
                BuildUtils.CopyDirectory(sourceDirectory, destinationDirectory);
            } catch (Exception e) {
                Debug.LogError($"BuildControl: Backup failed. Error: {e.Message}");
            }
        }
    }
}

}

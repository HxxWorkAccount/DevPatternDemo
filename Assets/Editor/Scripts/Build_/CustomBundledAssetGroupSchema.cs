namespace DevPattern.Editor.Build {
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.PackageManager;
using Unity.VisualScripting;
using Codice.Client.Commands;
using System.ComponentModel;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build.DataBuilders;

[CreateAssetMenu(fileName = "CustomBuildScriptPacked.asset", menuName = "DevPattern/CustomBuildScriptPacked")]
public class CustomBuildScriptPackedMode : BuildScriptPackedMode
{
    protected override string ProcessBundledAssetSchema(  //
        BundledAssetGroupSchema       schema,
        AddressableAssetGroup         assetGroup,
        AddressableAssetsBuildContext aaContext
    ) {
        /* 剔除逻辑为在 BuildControlWindow 上也做了 */
        if (ValidateAssetGroup(assetGroup))
            return base.ProcessBundledAssetSchema(schema, assetGroup, aaContext);
        else
            return string.Empty;
    }

    public static bool ValidateAssetGroup(AddressableAssetGroup assetGroup) {
        string groupName = assetGroup.Name;
        string pattern   = @"^\(([^)]*?)\)";
        Match  match     = Regex.Match(groupName, pattern);

        if (match.Success) {
            string[] tags = match.Groups[1].Value.Split(',').Select(t => t.Trim().ToLower()).ToArray();
            if (tags.Length == 0)
                return true;

            var buildControlWindow = BuildControlWindow.GetWindowInstance();
            var settings           = buildControlWindow.settings;
            if (settings == null)
                throw new InvalidOperationException("BuildControlWindow settings is null.");

            // FIXIT == 批处理模式下，没法获取 window 实例，可以提供个选项读取预留的 SO 配置文件
            var tagSettings = Instantiate(settings);
            foreach (string tag in tags) {
                if (Enum.TryParse(tag, true, out tagSettings.settings.buildType)) {
                    continue;
                } else if (Enum.TryParse(tag, true, out tagSettings.settings.logicalPlatform)) {
                    continue;
                } else if (settings.logicalPlatform == LogicalPlatform.Server) {
                    if (Enum.TryParse(tag, true, out tagSettings.settings.serverTarget))
                        continue;
                } else if (Enum.TryParse(tag, true, out tagSettings.settings.clientTarget)) {
                    continue;
                }
                Debug.LogWarning($"Invalid tag '{tag}' in asset group '{groupName}'");
            }
            return settings.Encompass(tagSettings);
        }

        // 平台资源剔除、DLC 剔除逻辑暂时就不写了，之后可以在这里拓展
        return true;
    }
}
}

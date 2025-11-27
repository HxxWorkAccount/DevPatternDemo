namespace DevPattern.Editor.Build {
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.PackageManager;
using Unity.VisualScripting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.AddressableAssets.Build;
using CSObjectWrapEditor;

// （Todo）场景管理：EditorBuildSettings.scenes / BuildPlayerOptions.scenes，支持特定环境模板
// （Todo）研究一下怎么做代码剔除（IUnityLinkerProcessor）、Shader Variant 剔除（IPreprocessShaders、IPreprocessComputeShaders）

//====================== 构建配置相关 ======================//

// 按包容性排序，例如：Test 构建总是包含 Test、Release 和 Dev 的代码，Dev 总是包含 Dev、Release 的代码
[Serializable]
public enum BuildType {
    Release,
    Dev,
    Test,
}

[Serializable]
public enum LogicalPlatform {
    Client,
    Server,
    Host,
}

[Serializable]
public enum ClientBuildTarget {
    iOS      = BuildTargetGroup.iOS,
    Android  = BuildTargetGroup.Android,
    WebGL    = BuildTargetGroup.WebGL,
    WSA      = BuildTargetGroup.WSA,
    PS4      = BuildTargetGroup.PS4,
    PS5      = BuildTargetGroup.PS5,
    XboxOne  = BuildTargetGroup.XboxOne,
    tvOS     = BuildTargetGroup.tvOS,
    Switch   = BuildTargetGroup.Switch,
    Stadia   = BuildTargetGroup.Stadia,
    VisionOS = BuildTargetGroup.VisionOS,
    Windows,
    MacOS,
    Linux,
}

[Serializable]
public enum ServerBuildTarget {
    LinuxHeadlessSimulation = BuildTargetGroup.LinuxHeadlessSimulation,
    EmbeddedLinux           = BuildTargetGroup.EmbeddedLinux,
    Windows,
    MacOS,
    Linux,
}

[CreateAssetMenu(fileName = "DefaultBuildControlSettings", menuName = "DevPattern/Build Control Settings", order = 1)]
public sealed class BuildControlSettings : ScriptableObject
{
    [Serializable]
    public struct BuildSettingsData
    {
        public BuildType         buildType;
        public LogicalPlatform   logicalPlatform;
        public ClientBuildTarget clientTarget;
        public ServerBuildTarget serverTarget;

        public BuildSettingsData(
            BuildType         buildType         = BuildType.Dev,
            LogicalPlatform   logicalPlatform   = LogicalPlatform.Client,
            ClientBuildTarget clientBuildTarget = ClientBuildTarget.Windows,
            ServerBuildTarget serverBuildTarget = ServerBuildTarget.Windows
        ) {
            this.buildType       = buildType;
            this.logicalPlatform = logicalPlatform;
            this.clientTarget    = clientBuildTarget;
            this.serverTarget    = serverBuildTarget;
        }
    }

    public BuildSettingsData settings        = new BuildSettingsData();
    public BuildType         buildType       => settings.buildType;
    public LogicalPlatform   logicalPlatform => settings.logicalPlatform;
    public ClientBuildTarget clientTarget    => settings.clientTarget;
    public ServerBuildTarget serverTarget    => settings.serverTarget;

    /* Environment */
    public bool isClient          => logicalPlatform != LogicalPlatform.Server;
    public bool isServer          => logicalPlatform != LogicalPlatform.Client;
    public bool IsDedicatedServer => isServer && !isClient;
    public bool IsDedicatedClient => isClient && !isServer;
    public bool isDev             => buildType != BuildType.Release;
    public bool isTest            => buildType == BuildType.Test;
    public bool isRelease         => buildType == BuildType.Release;
    public bool isHost            => logicalPlatform == LogicalPlatform.Host;

    public bool Encompass(BuildControlSettings obj) {
        if (obj == null)
            return false;
        if (obj == this)
            return true;
        if ((int)buildType < (int)obj.buildType)
            return false;
        if (logicalPlatform != obj.logicalPlatform && logicalPlatform != LogicalPlatform.Host)
            return false;  // 当平台不一致时，仅允许 Host 包含 Client 和 Server
        if (logicalPlatform == LogicalPlatform.Server) {
            if (serverTarget != obj.serverTarget &&  // server target 不同，仅允许 Linux 包含另外两个特殊 Linux
                !(serverTarget == ServerBuildTarget.Linux && BuildUtils.IsLinux(obj.serverTarget)))
                return false;
        } else if (clientTarget != obj.clientTarget) {
            return false;
        }
        return true;
    }

    public string outputPath {
        get {
            string name;
            string ext = "";
            if (logicalPlatform == LogicalPlatform.Server) {
                name = $"{serverTarget}_{logicalPlatform}_{buildType}";
                if (serverTarget == ServerBuildTarget.Windows)
                    ext = ".exe";
            } else {
                name = $"{clientTarget}_{logicalPlatform}_{buildType}";
                if (clientTarget == ClientBuildTarget.Windows)
                    ext = ".exe";
            }
            return $"Builds/{name}/{name}{ext}";
        }
    }
    public BuildTarget buildTarget {
        get {
            if (logicalPlatform == LogicalPlatform.Server) {
                return BuildUtils.ToUnityTarget(serverTarget);
            } else {
                return BuildUtils.ToUnityTarget(clientTarget);
            }
        }
    }
    public BuildTargetGroup buildTargetGroup {
        get {
            if (logicalPlatform == LogicalPlatform.Server) {
                return BuildUtils.ToUnityTargetGroup(serverTarget);
            } else {
                return BuildUtils.ToUnityTargetGroup(clientTarget);
            }
        }
    }
    public BuildOptions buildOptions {
        get {
            BuildOptions options = BuildOptions.None;
            options |= BuildOptions.ShowBuiltPlayer;
            if (buildType == BuildType.Release) {
                options |= BuildOptions.CompressWithLz4HC;
                options |= BuildOptions.StrictMode;
            } else {
                options |= BuildOptions.Development;
                options |= BuildOptions.AllowDebugging;
                options |= BuildOptions.CompressWithLz4;
                if (buildType == BuildType.Test) {
                    options |= BuildOptions.IncludeTestAssemblies;
                }
            }
            switch (buildTargetGroup) {
            case BuildTargetGroup.Android:
                options |= BuildOptions.AcceptExternalModificationsToPlayer;
                break;
            case BuildTargetGroup.iOS:
                options |= BuildOptions.AcceptExternalModificationsToPlayer;
                break;
            }
            // EditorUserBuildSettings
            return options;
        }
    }

    public NamedBuildTarget namedBuildTarget {
        get {
            if (logicalPlatform == LogicalPlatform.Server)
                return NamedBuildTarget.Server;
            return NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
        }
    }

    public List<string> extraScriptingDefines {
        get {
            var results = new List<string>();
            if (buildType == BuildType.Release) {
                results.Add("UNITY_RELEASE");
            } else {
                results.Add("UNITY_DEV");  // DEV 内容也会打入 TEST
                if (buildType == BuildType.Test)
                    results.Add("UNITY_TEST");
            }
            switch (logicalPlatform) {
            case LogicalPlatform.Client:
                results.Add("UNITY_CLIENT");
                break;
            case LogicalPlatform.Server:
                results.Add("UNITY_SERVER");
                break;
            case LogicalPlatform.Host:
                results.Add("UNITY_CLIENT");
                results.Add("UNITY_SERVER");
                results.Add("UNITY_HOST");
                break;
            }
            return results;
        }
    }
}

//====================== 自定义构建面板 ======================//

public sealed class BuildControlWindow : EditorWindow
{
    // public const string               k_tempSettingsPath = "Assets/Editor/Temp/BuildControl/_TempSettingsBackup.asset";
    public const string               k_tempSettingsPath = "Library/DevPattern/Temp/BuildControl/TempSettingsBackup.json";
    private static BuildControlWindow s_instance;

    private static readonly string[] k_forbiddenCompilationSymbols = new string[] {
        "UNITY_EDITOR", "UNITY_HOST", "UNITY_CLIENT", "UNITY_SERVER", "UNITY_DEV", "UNITY_TEST", "UNITY_RELEASE",
    };

    [SerializeField]
    private BuildControlSettings m_referenceSetting;

    private BuildControlSettings m_settings;
    public BuildControlSettings  settings {
        get {
            if (m_settings != null)
                return m_settings;
            if (LoadBackupSettings())
                return m_settings;
            // var tempSettings = AssetDatabase.LoadAssetAtPath<BuildControlSettings>(k_tempSettingsPath);
            // if (tempSettings != null) {
            //     Debug.Log($"Recover from temp");
            //     m_settings = Instantiate(tempSettings);
            //     return m_settings;
            // }
            if (m_referenceSetting != null) {
                m_settings = Instantiate(m_referenceSetting);
                return m_settings;
            }
            Clear();
            return m_settings;
        }
    }

    // Build Settings on Control Panel
    [SerializeField]
    private bool m_fullBuild = false;
    public bool  fullBuild   => m_fullBuild;
    [SerializeField]
    private bool m_connectProfiler = false;
    public bool  connectProfiler   => m_connectProfiler;
    [SerializeField]
    private bool m_connectHost = false;
    public bool  connectHost   => m_connectHost;
    [SerializeField]
    private bool m_waitPlayerConnection = false;
    public bool  waitPlayerConnection   => m_waitPlayerConnection;
    [SerializeField]
    private bool m_coverage = false;
    public bool  coverage   => m_coverage;
    [SerializeField]
    private bool m_deepProfiling = false;
    public bool  deepProfiling   => m_deepProfiling;
    [SerializeField]
    private bool m_buildReport = false;
    public bool  buildReport   => m_buildReport;
    [SerializeField]
    private bool m_backupBuildArtifact = false;
    public bool  backupBuildArtifact   => m_backupBuildArtifact;

    /* UI */
    private Vector2 m_uiScrollCache;

    /* Backup and Recovery */
    private Func<AddressableAssetGroup, bool> m_prevGroupFilter;

    /*====-------------- Life --------------====*/

    [MenuItem("Window/DevPattern/Build Control")]
    public static void ShowWindow() {
        if (s_instance != null) {
            s_instance.Focus();
            return;
        } else {
            s_instance              = GetWindow<BuildControlWindow>();
            s_instance.titleContent = new GUIContent("Build Control");
            s_instance.Show();
        }
    }

    public static BuildControlWindow GetWindowInstance() {
        if (s_instance == null)
            ShowWindow();
        return s_instance;
    }

    private void OnEnable() {
        if (m_settings == null) {
            string[] settingGuids = AssetDatabase.FindAssets($"t:{nameof(BuildControlSettings)}");
            if (settingGuids.Length == 0) {
                Clear();
            } else {
                Load(settingGuids[0]);
            }
        }
    }

    private void OnDisable() { }

    /*====-------------- 配置加载与保存 --------------====*/

    private void Save() {
        if (m_referenceSetting != null && m_settings != null) {
            string originName = m_referenceSetting.name;
            EditorUtility.CopySerialized(m_settings, m_referenceSetting);
            m_referenceSetting.name = originName;
            EditorUtility.SetDirty(m_referenceSetting);
            AssetDatabase.SaveAssets();
        }
    }

    private void Load(string settingGuid) {
        BuildControlSettings referenceSetting = AssetDatabase.LoadAssetAtPath<BuildControlSettings>(  //
            AssetDatabase.GUIDToAssetPath(settingGuid)
        );
        Load(referenceSetting);
    }

    private void Load(BuildControlSettings settings) {
        m_referenceSetting = settings;
        m_settings         = Instantiate(m_referenceSetting);
    }

    private void Clear() {
        m_referenceSetting = null;
        if (m_settings == null)
            m_settings = CreateInstance<BuildControlSettings>();
    }

    private void ConfirmSave() {
        if (m_referenceSetting != null && m_settings != null) {
            bool shouldSave = EditorUtility.DisplayDialog("BuildControl", "Save changes to the BuildControlSettings asset?", "Yes", "No");
            if (shouldSave)
                Save();
        }
    }

    private bool LoadBackupSettings() {
        if (File.Exists(k_tempSettingsPath)) {
            string json         = File.ReadAllText(k_tempSettingsPath);
            m_settings          = CreateInstance<BuildControlSettings>();
            m_settings.settings = JsonUtility.FromJson<BuildControlSettings.BuildSettingsData>(json);
            return true;
            // var tempSettings = AssetDatabase.LoadAssetAtPath<BuildControlSettings>(k_tempSettingsPath);
            // if (tempSettings != null) {
            //     Debug.Log($"Recover from temp");
            //     m_settings = Instantiate(tempSettings);
            //     return true;
            // }
        }
        return false;
    }

    private void BackupTempSettings() {
        if (m_settings != null) {
            string directory = Path.GetDirectoryName(k_tempSettingsPath);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            string json = JsonUtility.ToJson(m_settings.settings);
            File.WriteAllText(k_tempSettingsPath, json);

            // if (!Directory.Exists(directory)) {
            //     Directory.CreateDirectory(directory);
            //     AssetDatabase.Refresh();
            // }
            // AssetDatabase.DeleteAsset(k_tempSettingsPath);
            // var backup       = Instantiate(m_settings);
            // backup.hideFlags = HideFlags.HideInHierarchy;
            // AssetDatabase.CreateAsset(backup, k_tempSettingsPath);
            // AssetDatabase.SaveAssets();
        } else {
            Debug.LogError($"Saved failed, m_settings is null");
        }
    }

    /*====-------------- UI --------------====*/

    private void OnGUI() {
        var curr = settings;  // 重新获取一下 settings，避免为空（这里写的有点丑，但没时间改了）
        SettingsManagementGUI();
        SettingsPropertiesGUI();
        BuildGUI();
    }

    private void SettingsManagementGUI() {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        var currSetting = (BuildControlSettings)EditorGUILayout.ObjectField(  //
            "Settings", m_referenceSetting, typeof(BuildControlSettings), false
        );
        if (m_referenceSetting != null && GUILayout.Button("Save Settings")) {
            Save();
        }
        if (GUILayout.Button("Save Settings As...")) {
            string path = EditorUtility.SaveFilePanelInProject(  //
                "Save BuildControlSettings As", "BuildControlSettings", "asset", "Please enter a file name to save the settings to."
            );
            if (!string.IsNullOrEmpty(path)) {
                var newSetting = CreateInstance<BuildControlSettings>();
                EditorUtility.CopySerialized(m_settings, newSetting);
                AssetDatabase.CreateAsset(newSetting, path);
                AssetDatabase.SaveAssets();
                Load(newSetting);
                currSetting = m_referenceSetting;
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        if (EditorGUI.EndChangeCheck()) {
            if (currSetting != m_referenceSetting) {
                if (currSetting != null)
                    Load(currSetting);
                else
                    Clear();
            }
        }
    }

    private void SettingsPropertiesGUI() {
        EditorGUILayout.BeginVertical();

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(m_uiScrollCache)) {
            m_uiScrollCache = scrollScope.scrollPosition;

            /* 编辑部分 */
            m_settings.settings.buildType       = (BuildType)EditorGUILayout.EnumPopup("Build Type", m_settings.buildType);
            m_settings.settings.logicalPlatform = (LogicalPlatform
            )EditorGUILayout.EnumPopup("Logical Platform", m_settings.logicalPlatform);
            if (m_settings.logicalPlatform == LogicalPlatform.Server) {
                m_settings.settings.serverTarget = (ServerBuildTarget)EditorGUILayout.EnumPopup("Build Target", m_settings.serverTarget);
            } else {
                m_settings.settings.clientTarget = (ClientBuildTarget)EditorGUILayout.EnumPopup("Build Target", m_settings.clientTarget);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void BuildGUI() {
        // FIXIT == 添加一个提示，表示上次生成代码是否与当前编译目标不一致
        EditorGUILayout.BeginVertical();
        var addressableSetting       = AddressableAssetSettingsDefaultObject.Settings;
        var playerDataBuilderIndex   = addressableSetting.ActivePlayerDataBuilderIndex;
        var playModeDataBuilderIndex = addressableSetting.ActivePlayModeDataBuilderIndex;
        EditorGUILayout.LabelField($"player pack mode: {playerDataBuilderIndex}, editor play pack mode: {playModeDataBuilderIndex}");
        if (!CheckPlayerSettings(out string reason, true)) {
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Regenerate XLua Code")) {
            CSObjectWrapEditor.Generator.ClearAll();
            CSObjectWrapEditor.Generator.GenAll();
        }
        if (GUILayout.Button("Set Player Settings")) {
            SetPlayerSettings();
        }
        if (GUILayout.Button("Clear Player Settings")) {
            ClearPlayerSettings();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        /* 部分构建配置 */
        m_fullBuild       = GUILayout.Toggle(fullBuild, "Full Build", GUILayout.Width(200));
        m_connectProfiler = GUILayout.Toggle(connectProfiler, "Connect Profiler", GUILayout.Width(200));
        m_connectHost     = GUILayout.Toggle(connectHost, "Connect Profiler", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        m_waitPlayerConnection = GUILayout.Toggle(waitPlayerConnection, "Wait For Player Connection", GUILayout.Width(200));
        m_deepProfiling        = GUILayout.Toggle(deepProfiling, "Deep Profiling", GUILayout.Width(200));
        m_buildReport          = GUILayout.Toggle(buildReport, "Detailed Build Report", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        // m_coverage = GUILayout.Toggle(m_coverage, "Coverage", GUILayout.Width(200));
        m_backupBuildArtifact = GUILayout.Toggle(backupBuildArtifact, "Backup", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        /* 构建按钮 */
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(BuildPipeline.isBuildingPlayer);
        if (GUILayout.Button("Build") && !BuildPipeline.isBuildingPlayer) {
            BuildPlayer(false);
        }
        if (GUILayout.Button("Build and Run") && !BuildPipeline.isBuildingPlayer) {
            BuildPlayer(true);
        }
        if (GUILayout.Button("Build Assets") && !BuildPipeline.isBuildingPlayer) {
            BuildAssets();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    /*====-------------- Utils --------------====*/

    private void BuildPlayer(bool autoRun) {
        if (!CheckPlayerSettings(out string reason)) {
            Debug.LogError($"Invalid Player Settings! {reason}");
            return;
        }
        var buildPlayerOptions = GetBuildPlayerOptions();
        if (autoRun)
            buildPlayerOptions.options |= BuildOptions.AutoRunPlayer;
        else
            buildPlayerOptions.options &= ~BuildOptions.AutoRunPlayer;
        try {
            SetCustomDataBuilder();
            BackupTempSettings();
            BuildPipeline.BuildPlayer(buildPlayerOptions);
        } catch (Exception e) {
            Debug.LogError($"Build failed: {e.Message}");
        }
        Recovery();
    }

    private void BuildAssets() {
        try {
            SetCustomDataBuilder();
            BackupTempSettings();
            SetEditorUserBuildSettings();
            AddressableAssetSettings.BuildPlayerContent();
        } catch (Exception e) {
            Debug.LogError($"Build failed: {e.Message}");
        }
        Recovery();
    }

    private BuildPlayerOptions GetBuildPlayerOptions() {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes             = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        buildPlayerOptions.locationPathName   = m_settings.outputPath;
        buildPlayerOptions.targetGroup        = m_settings.buildTargetGroup;
        buildPlayerOptions.target             = m_settings.buildTarget;
        if (buildPlayerOptions.targetGroup == BuildTargetGroup.Standalone) {
            buildPlayerOptions.subtarget =
                m_settings.logicalPlatform == LogicalPlatform.Server ? (int)StandaloneBuildSubtarget.Server : (int)StandaloneBuildSubtarget.Player;
        } else {
            // buildPlayerOptions.subtarget =
            throw new NotImplementedException();
        }
        buildPlayerOptions.options = m_settings.buildOptions;
        if (fullBuild)
            buildPlayerOptions.options |= BuildOptions.CleanBuildCache;
        if (connectProfiler)
            buildPlayerOptions.options |= BuildOptions.ConnectWithProfiler;
        if (connectHost)
            buildPlayerOptions.options |= BuildOptions.ConnectToHost;
        if (waitPlayerConnection)
            buildPlayerOptions.options |= BuildOptions.WaitForPlayerConnection;
        if (coverage)
            buildPlayerOptions.options |= BuildOptions.EnableCodeCoverage;
        if (m_settings.buildType != BuildType.Release && deepProfiling)
            buildPlayerOptions.options |= BuildOptions.EnableDeepProfilingSupport;
        if (buildReport)
            buildPlayerOptions.options |= BuildOptions.DetailedBuildReport;
        buildPlayerOptions.extraScriptingDefines = m_settings.extraScriptingDefines.ToArray();
        return buildPlayerOptions;
    }

    private void SetCustomDataBuilder() {
        var addressableSetting              = AddressableAssetSettingsDefaultObject.Settings;
        m_prevGroupFilter                   = ContentUpdateScript.GroupFilterFunc;
        ContentUpdateScript.GroupFilterFunc = CustomGroupFilter;
        int builderIndex = addressableSetting.DataBuilders.FindIndex(b => b.GetType() == typeof(CustomBuildScriptPackedMode));
        if (builderIndex == -1)
            throw new InvalidOperationException(
                "Could not find 'CustomBuildScriptPackedMode' Data Builder! Make sure it is added to the AddressableAssetSettings.DataBuilders."
            );
        addressableSetting.ActivePlayerDataBuilderIndex = builderIndex;
    }

    private void Recovery() {
        ContentUpdateScript.GroupFilterFunc = m_prevGroupFilter;
        m_prevGroupFilter                   = null;
    }

    private void SetPlayerSettings() {
        string symbolStr        = PlayerSettings.GetScriptingDefineSymbols(settings.namedBuildTarget);
        string[] symbols        = symbolStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
        List<string> newSymbols = m_settings.extraScriptingDefines;
        foreach (string symbol in symbols) {
            if (!k_forbiddenCompilationSymbols.Contains(symbol))
                newSymbols.Add(symbol);
        }
        PlayerSettings.SetScriptingDefineSymbols(settings.namedBuildTarget, string.Join(";", newSymbols));
    }

    private void ClearPlayerSettings() {
        string symbolStr        = PlayerSettings.GetScriptingDefineSymbols(settings.namedBuildTarget);
        string[] symbols        = symbolStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
        List<string> newSymbols = new();
        foreach (string symbol in symbols) {
            if (!k_forbiddenCompilationSymbols.Contains(symbol))
                newSymbols.Add(symbol);
        }
        PlayerSettings.SetScriptingDefineSymbols(settings.namedBuildTarget, string.Join(";", newSymbols));
    }

    private void SetEditorUserBuildSettings() {
        /* BuildPipeline 会自动切换，Addressable 不会，所以这里手动切换一下 */
        var buildTarget        = settings.buildTarget;
        var buildTargetGroup   = settings.buildTargetGroup;
        var buildPlayerOptions = GetBuildPlayerOptions();
        if (EditorUserBuildSettings.activeBuildTarget != buildTarget || EditorUserBuildSettings.selectedBuildTargetGroup != buildTargetGroup) {
            Debug.Log(
                $"build assets failed. platform mismatch. expected: {buildTarget}-{buildTargetGroup}, curr: {EditorUserBuildSettings.activeBuildTarget}-{EditorUserBuildSettings.selectedBuildTargetGroup}"
            );
            EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
        }
        if (buildTargetGroup == BuildTargetGroup.Standalone &&
            buildPlayerOptions.subtarget != (int)EditorUserBuildSettings.standaloneBuildSubtarget) {
            Debug.Log(
                $"Switch Standalone Build Subtarget: {EditorUserBuildSettings.standaloneBuildSubtarget} -> {(StandaloneBuildSubtarget)buildPlayerOptions.subtarget}"
            );
            EditorUserBuildSettings.standaloneBuildSubtarget = (StandaloneBuildSubtarget)buildPlayerOptions.subtarget;
        } else {
            throw new NotImplementedException();
        }
    }

    private bool CheckPlayerSettings(out string reason, bool strict = false) {
        reason = string.Empty;

        /* 检查 Player Settings 上的编译符号 */
        if (settings.namedBuildTarget == NamedBuildTarget.Unknown) {
            reason = "Named Build Target is Unknown.";
            return false;
        }
        List<string> targetSymbols = m_settings.extraScriptingDefines;
        string       symbolStr     = PlayerSettings.GetScriptingDefineSymbols(settings.namedBuildTarget);
        // if (!string.IsNullOrEmpty(symbolStr))
        //     Debug.Log($"Current Define Symbols in '{settings.namedBuildTarget}' from Player Settings: '{symbolStr}'");
        string[] symbols            = symbolStr.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> invalidSymbols = new();
        foreach (var forbiddenSymbol in k_forbiddenCompilationSymbols) {
            if (symbols.Contains(forbiddenSymbol) && !targetSymbols.Contains(forbiddenSymbol)) {
                invalidSymbols.Add(forbiddenSymbol);
            }
        }
        if (invalidSymbols.Count > 0) {
            reason = $"Invalid compilation symbols in Player Settings: '{string.Join(", ", invalidSymbols)}'";
            return false;
        }
        if (strict) {
            List<string> lessSymbols = new();
            foreach (var targetSymbol in targetSymbols) {
                if (!symbols.Contains(targetSymbol)) {
                    lessSymbols.Add(targetSymbol);
                }
            }
            if (lessSymbols.Count > 0) {
                reason = $"Missing compilation symbols in Player Settings: '{string.Join(", ", lessSymbols)}'";
                return false;
            }
        }

        return true;
    }

    private bool CustomGroupFilter(AddressableAssetGroup group) {
        if (!CustomBuildScriptPackedMode.ValidateAssetGroup(group)) {
            Debug.Log($"Exclude Asset Group '{group.Name}' from build.");
            return false;
        } else if (m_prevGroupFilter != null) {
            return m_prevGroupFilter(group);
        } else {
            return true;
        }
    }
}

}

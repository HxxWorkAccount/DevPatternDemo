namespace DevPattern.Dev.Universal.Lua {
using System;
using System.IO;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Manager;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class LuaHotreloadListener : Singleton<LuaHotreloadListener>
{
    public static readonly List<string> k_editorWatchFolders = new() {
        "Lua",
        "Dev/Lua",
        "Tests/Lua",
    };

    private readonly List<FileSystemWatcher> m_watchers     = new();
    private readonly HashSet<string> m_tempChangedFiles     = new();
    private readonly ConcurrentQueue<string> m_changedFiles = new();

    /*====-------------- Life --------------====*/

    void OnEnable() {
        if (EnvironmentUtils.env.isEditor) {
            foreach (var folder in k_editorWatchFolders)
                CreateWatcher(folder);
            Debug.Log("Watching editor lua file changes...");
        } else {
            if (!Directory.Exists(LuaBridge.additionalLuaFolder))
                Directory.CreateDirectory(LuaBridge.additionalLuaFolder); // 将 Assets 下的 Lua 脚本外链过去
            CreateWatcher(LuaBridge.additionalLuaFolder);
            Debug.Log($"Watching additional lua file changes at '{LuaBridge.additionalLuaFolder}' ...");
        }
    }

    void Update() {
        if (m_changedFiles.Count > 0) {
            m_tempChangedFiles.Clear();
            while (m_changedFiles.TryDequeue(out string luaFilepath)) {
                m_tempChangedFiles.Add(luaFilepath);
            }
            foreach (var luaFilepath in m_tempChangedFiles) {
                try {
                    string moduleName = FilepathToLuaModuleName(luaFilepath);
                    LuaBridge.instance.luaHotreloadHelper.HotReloadModule(moduleName);
                } catch (Exception e) {
                    Debug.LogError($"Hotreload lua module failed: {luaFilepath}, exception: {e}");
                }
            }
            m_tempChangedFiles.Clear();
        }
    }

    protected override void OnSingletonDisable() {
        ClearWatchers();
        Debug.Log("Stopped watching lua files.");
    }

    /*====-------------- Watch Files --------------====*/

    private void CreateWatcher(string folderPath) {
        string path = Path.Combine(Application.dataPath, folderPath);
        if (!Directory.Exists(path)) {
            // Debug.LogWarning($"Create watcher failed, directory doesn't exist: {path}");
            return;
        }
        FileSystemWatcher watcher = new(path);

        watcher.Filter = "*.lua";  // 只监视 Lua 文件

        // 监视的更改类型
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

        // 添加事件处理器
        watcher.Changed += OnChanged;
        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;

        // 监视子目录（这里在 Windows 系统上是能监听到符号链接的，而且事件返回的是逻辑路径）
        watcher.IncludeSubdirectories = true;

        // 开始监视
        m_watchers.Add(watcher);
        watcher.EnableRaisingEvents = true;
        Debug.Log($"Create watcher: {path}");
    }

    private void ClearWatchers() {
        foreach (var watcher in m_watchers) {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnChanged;
            watcher.Created -= OnCreated;
            watcher.Deleted -= OnDeleted;
            watcher.Dispose();
        }
        m_watchers.Clear();
    }

    /*==--- 文件事件，这些事件不在 Unity 主线程上触发 ---==*/

    private void OnChanged(object source, FileSystemEventArgs e) {
        if (!IsLuaFile(e.FullPath))
            return;
        Debug.Log($"Module changed: {e.FullPath}, change type: {e.ChangeType}");
        m_changedFiles.Enqueue(e.FullPath);
    }

    private void OnCreated(object source, FileSystemEventArgs e) {
        if (!IsLuaFile(e.FullPath))
            return;
        // Debug.Log($"Module created: {LuaManager.PathToModuleName(e.FullPath)}, change type: {e.ChangeType}");
    }

    private void OnDeleted(object source, FileSystemEventArgs e) {
        if (!IsLuaFile(e.FullPath))
            return;
        // Debug.Log($"Module deleted: {LuaManager.PathToModuleName(e.FullPath)}, change type: {e.ChangeType}");
    }

    /*====-------------- Utils --------------====*/

    public static bool IsLuaFile(string filepath) {
        return filepath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase);
    }

    public static string FilepathToLuaModuleName(string fileFullpath) {
        if (EnvironmentUtils.env.isEditor) {
            string path       = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, fileFullpath));
            string moduleName = LuaBridge.AssetToLuaPath(path, out LuaSource inferredSource);
            Debug.Assert(inferredSource == LuaSource.Addressable, $"[FilepathToLuaModuleName] Wrong inferred source: '{fileFullpath}', inferred '{inferredSource}' expected 'Addressable'");
            return moduleName;
        } else {
            string moduleName = LuaBridge.AssetToLuaPath(fileFullpath, out LuaSource inferredSource);
            Debug.Assert(inferredSource == LuaSource.Additional, $"[FilepathToLuaModuleName] Wrong inferred source: '{fileFullpath}', inferred '{inferredSource}' expected 'Additional'");
            return moduleName;
        }
        throw new NotImplementedException($"Couldn't get lua module name from filepath in current environment. file: '{fileFullpath}'");
    }
}

}

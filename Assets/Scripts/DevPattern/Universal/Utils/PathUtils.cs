namespace DevPattern.Universal.Utils {
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class PathUtils
{
    public static bool IsPathUnderDirectory(string filePath, string directoryPath) {
        var fullFilePath      = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDirectoryPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullFilePath.Length > fullDirectoryPath.Length && // 相同目录并不会返回 true
               fullFilePath.StartsWith(fullDirectoryPath, StringComparison.OrdinalIgnoreCase);
    }
}

}

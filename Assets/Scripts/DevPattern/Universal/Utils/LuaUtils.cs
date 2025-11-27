namespace DevPattern.Universal.Utils {
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class LuaUtils
{
    public static bool IsNull(GameObject obj) {
        return obj == null;
    }
}

}

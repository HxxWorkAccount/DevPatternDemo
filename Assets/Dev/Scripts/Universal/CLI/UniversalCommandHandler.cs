namespace DevPattern.Dev.Universal.CLI {
using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.InputSystem;
using DevPattern.Universal.Utils;
using DevPattern.Universal;
using DevPattern.Universal.Lua;
using System.Collections.Generic;

public static class UniversalCommandHandler
{
    public static bool HandleCommand(string command) {
        string[] parameters = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parameters[0] == "reload") {
            if (parameters.Length <= 1) {
                LuaBridge.instance.luaHotreloadHelper.HotReloadAllModules();
            } else {
                LuaBridge.instance.luaHotreloadHelper.HotReloadModule(parameters[1]);
            }
            return true;
        }
        return false;
    }
}

}

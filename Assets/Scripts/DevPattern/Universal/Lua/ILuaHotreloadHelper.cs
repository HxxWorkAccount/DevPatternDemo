namespace DevPattern.Universal.Lua {
using System;
using UnityEngine;

public interface ILuaHotreloadHelper
{
    public void HotReloadModule(string moduleName);
    public void HotReloadAllModules();
}

}

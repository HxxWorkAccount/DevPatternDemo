namespace DevPattern.Dev.Client {
using System;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;

public class DevClientInitializer : LuaBehaviour
{
    public DevClientInitializer(): base("Dev.Lua.Client.DevClientInitializer") { }
}

}

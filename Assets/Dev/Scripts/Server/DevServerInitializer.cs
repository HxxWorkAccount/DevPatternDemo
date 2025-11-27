namespace DevPattern.Dev.Server {
using System;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;

public class DevServerInitializer : LuaBehaviour
{
    public DevServerInitializer(): base("Dev.Lua.Server.DevServerInitializer") { }
}

}

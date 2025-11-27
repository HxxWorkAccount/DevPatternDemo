namespace DevPattern.Server {
using System;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;

public class ServerInitializer : LuaBehaviour
{
    public ServerInitializer(): base("Lua.Server.ServerInitializer") { }
}

}

namespace DevPattern.Client {
using System;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;

public class ClientInitializer : LuaBehaviour
{
    public ClientInitializer(): base("Lua.Client.ClientInitializer") { }
}

}

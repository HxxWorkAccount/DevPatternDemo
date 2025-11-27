namespace DevPattern.Universal.Lua {
using System;
using UnityEngine;

public interface ILuaBase : IDisposable
{
    object lobject { get; }
}

}

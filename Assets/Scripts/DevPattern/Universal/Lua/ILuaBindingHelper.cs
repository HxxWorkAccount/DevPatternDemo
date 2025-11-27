namespace DevPattern.Universal.Lua {
using System;
using UnityEngine;

public interface ILuaBindingHelper
{
    public ILuaTable Bind(Component component, string bindingModule, string bindingClass="");
}

}

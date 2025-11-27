namespace DevPattern.Universal.Lua {
using System;
using UnityEngine;

public interface ILuaFunction : ILuaBase
{
    public void    Action<T>(T a);
    public TResult Func<T, TResult>(T a);
    public void    Action<T1, T2>(T1 a1, T2 a2);
    public TResult Func<T1, T2, TResult>(T1 a1, T2 a2);
    public T       Cast<T>();
    public void    SetEnv(ILuaTable env);

    // deprecated
    public object[] Call(object[] args, Type[] returnTypes);
    // deprecated
    public object[] Call(params object[] args);
}

}

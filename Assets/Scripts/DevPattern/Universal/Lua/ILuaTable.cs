namespace DevPattern.Universal.Lua {
using System;
using System.Collections;
using UnityEngine;

public interface ILuaTable : ILuaBase
{
    public bool        ContainsKey<TKey>(TKey key);
    public void        Get<TKey, TValue>(TKey key, out TValue value);
    public TValue      Get<TKey, TValue>(TKey key);
    public TValue      Get<TValue>(string key);
    public void        Set<TKey, TValue>(TKey key, TValue value);
    public void        SetMetaTable(ILuaTable metaTable);
    public T           Cast<T>();
    public T           GetInPath<T>(string path);
    public void        SetInPath<T>(string path, T val);
    public void        ForEach<TKey, TValue>(Action<TKey, TValue> action);
    public IEnumerable GetKeys();

    public int         Length { get; }
}

}

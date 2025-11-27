namespace DevPattern.Lua {
using XLua;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevPattern.Universal;
using DevPattern.Universal.Utils;
using DevPattern.Universal.Lua;

class LuaTableWrapper : ILuaTable
{
    private LuaTable m_luaTable;
    public LuaTable  luaTable => m_luaTable;

    /*====-------------- Life --------------====*/

    public LuaTableWrapper(LuaTable table) {
        m_luaTable = table;
    }

    public static implicit operator LuaTable(LuaTableWrapper wrapper) {
        return wrapper.m_luaTable;
    }

    public void Dispose() {
        m_luaTable?.Dispose();
    }

    public override bool Equals(object obj) {
        return m_luaTable.Equals(obj);
    }
    public override int GetHashCode() {
        return m_luaTable.GetHashCode();
    }
    public override string ToString() {
        return m_luaTable.ToString();
    }

    /*====-------------- ILuaTable --------------====*/

    public object lobject => m_luaTable;

    public bool ContainsKey<TKey>(TKey key) {
        return m_luaTable.ContainsKey(key);
    }
    public void Get<TKey, TValue>(TKey key, out TValue value) {
        m_luaTable.Get(key, out value);
    }
    public TValue Get<TKey, TValue>(TKey key) {
        return m_luaTable.Get<TKey, TValue>(key);
    }
    public TValue Get<TValue>(string key) {
        return m_luaTable.Get<TValue>(key);
    }
    public void Set<TKey, TValue>(TKey key, TValue value) {
        m_luaTable.Set(key, value);
    }
    public void SetMetaTable(ILuaTable metaTable) {
        m_luaTable.SetMetaTable((LuaTableWrapper)metaTable);
    }
    public T Cast<T>() {
        return m_luaTable.Cast<T>();
    }
    public T GetInPath<T>(string path) {
        return m_luaTable.GetInPath<T>(path);
    }
    public void SetInPath<T>(string path, T val) {
        m_luaTable.SetInPath(path, val);
    }
    public void ForEach<TKey, TValue>(Action<TKey, TValue> action) {
        m_luaTable.ForEach(action);
    }
    public IEnumerable GetKeys() {
        return m_luaTable.GetKeys();
    }

    public int Length => m_luaTable.Length;
}

}

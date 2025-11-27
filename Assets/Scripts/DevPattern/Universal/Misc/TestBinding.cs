namespace DevPattern.Universal.Misc {
using System;
using System.Collections.Generic;
using DevPattern.Universal.Lua;
using UnityEngine;

public class TestBinding : LuaBehaviour
{
    private float m_interval = 0.0f;

    public TestBinding(): base("Lua.Universal.TestBinding") { }

    protected override void LAwake() {
        Debug.Log("TestBinding LAwake");
    }
    protected override void OnLEnable() {
        Debug.Log("TestBinding OnLEnable");
    }
    protected override void LStart() {
        Debug.Log("TestBinding LStart");
    }
    protected override void LUpdate() {
        if (m_interval > 1.0f) {
            Debug.Log("TestBinding LUpdate Interval: " + m_interval);
            m_interval = 0.0f;
        } else {
            m_interval += Time.deltaTime;
        }
    }
    protected override void OnLDisable() {
        Debug.Log("TestBinding OnLDisable");
    }
    protected override void OnLDestroy() {
        Debug.Log("TestBinding OnLDestroy");
    }
}

}

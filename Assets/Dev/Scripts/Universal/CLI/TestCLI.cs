namespace DevPattern.Dev.Universal.CLI {
using System;
using UnityEngine;

public class TestCLI : MonoBehaviour
{
    [Range(0.1f, 10f)]
    public float  testLogInterval   = 2f;
    private float m_tempLogInterval = 0f;

    private void Update() {
        if (m_tempLogInterval > testLogInterval) {
            Debug.Log("TestCLI Debug.Log");
            Console.WriteLine("TestCLI Console.WriteLine");
            m_tempLogInterval = 0f;
        } else {
            m_tempLogInterval += Time.unscaledDeltaTime;
        }
    }
}

}

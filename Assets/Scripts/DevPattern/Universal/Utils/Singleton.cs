namespace DevPattern.Universal.Utils {
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour
    where T : MonoBehaviour
{
    private static T s_instance;
    public static T  instance {
        get {
            if (s_instance == null) {  // 如果实例为空，尝试在场景中查找
                s_instance = FindObjectOfType<T>();
                if (s_instance == null) {  // 找不到，则创建一个
                    GameObject obj = new GameObject();
                    obj.name       = $"Singleton: {typeof(T).Name}";
                    s_instance     = obj.AddComponent<T>();
                }
                var instance = s_instance as Singleton<T>;
                if (instance && !instance.m_inited) {
                    // 确保单例初始化只执行一次
                    instance.m_inited = true;
                    instance.OnSingletonInit();
                }
            }
            return s_instance;
        }
    }

    public static T rawInstance => s_instance;

    private bool m_inited = false;
    public bool  inited   => m_inited;

    protected virtual void Awake() {
        if (s_instance != null && s_instance != this) {  // 确保只有一个实例
            Destroy(this);
        } else {
            s_instance = this as T;
            if (!m_inited) {
                m_inited = true;
                OnSingletonInit();
            }
            OnSingletonAwake();
        }
    }

    protected virtual void OnDisable() {
        if (s_instance == this)
            OnSingletonDisable();
    }

    protected virtual void OnDestroy() {
        if (s_instance == this) {
            OnSingletonDestroy();
            s_instance = null;
        }
    }

    protected virtual void OnSingletonInit() { } // Init 可以确保其在获取时初始化，哪怕自己的 Awake 还未调用
    protected virtual void OnSingletonAwake() { }
    protected virtual void OnSingletonDisable() { }
    protected virtual void OnSingletonDestroy() { }
}

}

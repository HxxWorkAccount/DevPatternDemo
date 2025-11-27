namespace DevPattern.Universal.Utils {
using System.Collections;
using System.Collections.Generic;

/* 去重队列 */
public class UniqueQueue<T> : IEnumerable<T>
{
    private readonly Queue<T> m_queue = new();
    private readonly HashSet<T> m_set = new();

    public int Count => m_queue.Count;

    public bool Enqueue(T item) {
        if (m_set.Add(item)) {
            m_queue.Enqueue(item);
            return true;
        }
        return false;
    }

    public T Dequeue() {
        var item = m_queue.Dequeue();
        m_set.Remove(item);
        return item;
    }

    public bool TryDequeue(out T result) {
        if (m_queue.Count > 0) {
            result = m_queue.Dequeue();
            m_set.Remove(result);
            return true;
        }

        result = default;
        return false;
    }

    public T Peek() {
        return m_queue.Peek();
    }

    public bool Contains(T item) {
        return m_set.Contains(item);
    }

    public void Clear() {
        m_queue.Clear();
        m_set.Clear();
    }

    public IEnumerator<T> GetEnumerator() {
        return m_queue.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}

}

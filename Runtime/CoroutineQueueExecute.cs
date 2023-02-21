using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoroutineQueueExecute : MonoBehaviour
{
    private static CoroutineQueueExecute instance;
    public static CoroutineQueueExecute Get()
    {
        if (!instance)
        {
            instance = new GameObject(nameof(CoroutineQueueExecute)).AddComponent<CoroutineQueueExecute>();
            DontDestroyOnLoad(instance.gameObject);
        }
        return instance;
    }

    private List<IEnumerator> Queue = new List<IEnumerator>();

    public static void SuppressAllQueue()
    {
        Get()?.Queue.Clear();
    }

    IEnumerator Start()
    {
        while (true)
        {
            if (Queue.Count > 0)
            {
                var q = Queue[0];
                yield return Get().StartCoroutine(q);
                Queue.Remove(q);
            }
            yield return null;
        }
    }

    public static void PushToQueue(IEnumerator e)
    {
        Get().Queue.Add(e);
    }
}

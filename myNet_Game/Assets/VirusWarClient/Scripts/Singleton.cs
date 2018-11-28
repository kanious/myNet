using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    static T instance;

    public static T Instance
    {
        get
        {
            if(null == instance)
            {
                instance = FindObjectOfType(typeof(T)) as T;
                if(null == instance)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    instance = obj.AddComponent<T>();
                }
            }

            return instance;
        }
    }
}

public abstract class Singleton<T> where T : class, new()
{
    static T instance;

    public static T Instance
    {
        get
        {
            if(null == instance)
            {
                instance = new T();
            }

            return instance;
        }
    }
}

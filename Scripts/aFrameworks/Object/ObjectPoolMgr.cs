using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Godot;

public partial class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    public Dictionary<string, ObjectPool> pooledObjectPoolsDict = new Dictionary<string, ObjectPool>(128);
    public Dictionary<string, string> lazyObjectsDict = new Dictionary<string, string>(128);
    public Dictionary<string, ObjectPool> tempPooledObjectPoolsDict = new Dictionary<string, ObjectPool>(16);
    public override void _EnterTree()
    {
        base._EnterTree();
        LoadPooled("res://Assets/Objs/aPooledObjs/");
        LoadLazy("res://Assets/Objs/bLazyObjs/");
    }

    void LoadPooled(string openPath)
    {
        if (openPath[openPath.Length - 2] == '_') return;
        string[] psArr = ResourceLoader.ListDirectory(openPath);
        foreach (string ps in psArr)
        {
            if (ps[ps.Length - 1] != '/')
            {
                if (Path.GetExtension(ps) == ".tscn")
                {
                    if (ps[ps.Length - 6] == '_') continue;
                    var pool = new ObjectPool(openPath + ps);
                    pooledObjectPoolsDict[pool.objectName] = pool;
                    GD.Print("注册了对象池：" + pool.objectName);
                }
            }
            else
            {
                LoadPooled(openPath + ps);
            }
        }
    }
    void LoadLazy(string openPath)
    {
        if (openPath[openPath.Length - 2] == '_') return;
        string[] psArr = ResourceLoader.ListDirectory(openPath);
        foreach (string ps in psArr)
        {
            if (ps[ps.Length - 1] != '/')
            {
                if (Path.GetExtension(ps) == ".tscn")
                {
                    if (ps[ps.Length - 6] == '_') continue;
                    var name = ObjectPool.GetObjName(ps);
                    lazyObjectsDict[name] = openPath + ps;
                    GD.Print("注册了懒加载对象：" + name);
                }
            }
            else
            {
                LoadLazy(openPath + ps);
            }
        }
    }
    public static bool ExistPossibleObject(string objectName)
    {
        if(!Instance.pooledObjectPoolsDict.ContainsKey(objectName) && !Instance.lazyObjectsDict.ContainsKey(objectName))
            return false;
        return true;
    }
    public static string GetLazyPath(string objectName)
    {
        if (Instance.lazyObjectsDict.TryGetValue(objectName, out var path))
        {
            return path;
        }
        return string.Empty;
    }
    public static string GetLazyDir(string objectName)
    {
        if (Instance.lazyObjectsDict.TryGetValue(objectName, out var path))
        {
            return ObjectPool.GetObjDir(path);
        }
        return string.Empty;
    }
    public static T GetLazyInstance<T>(string objectName) where T : Node
    {
        if (!Instance.lazyObjectsDict.ContainsKey(objectName))
        {
            GD.PrintErr($"无法找到对应的Lazy对象:{objectName}");
            return null;
        }
        return Global.GetObj<T>(GetLazyPath(objectName));
    }
    public static ObjectPool GetTempObjectPool(string objectName)
    {
        if(Instance.tempPooledObjectPoolsDict.TryGetValue(objectName, out ObjectPool resPool))
        {
            return resPool;
        }
        if (!Instance.lazyObjectsDict.TryGetValue(objectName,out string path))
        {
            GD.PrintErr("尝试获取TempObjectPool不存在对应的LazyObject");
        }
        var pool = new ObjectPool(path);
        if (!pool.valid) return null;
        Instance.tempPooledObjectPoolsDict[objectName] = pool;
        return pool; 
    }
    public static void ClearTempObjectPool(string objectName)
    {
        if (Instance.tempPooledObjectPoolsDict.TryGetValue(objectName, out ObjectPool resPool))
        {
            Instance.tempPooledObjectPoolsDict.Remove(objectName);
            resPool.Free();
        }
    }
    public static void ClearTempObjectPool(ObjectPool pool)
    {
        if (!pool.valid) return;
        if (Instance.tempPooledObjectPoolsDict.TryGetValue(pool.objectName, out ObjectPool resPool))
        {
            Instance.tempPooledObjectPoolsDict.Remove(pool.objectName);
            resPool.Free();
        }
    }
    public static ObjectPool GetObjectPool(string objectName)
    {
        if (Instance.pooledObjectPoolsDict.TryGetValue(objectName, out var pool))
        {
            return pool;
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return null;
    }
    public static ObjectPool GetPossiblePool(string objectName)
    {
        if (Instance.pooledObjectPoolsDict.TryGetValue(objectName, out var pool))
        {
            return pool;
        }
        var pool2 =  GetTempObjectPool(objectName);
        if (pool2 == null)
            GD.PrintErr($"无法找到对应的Possible对象池:{objectName}");
        return pool2;
    }
    public static T GetPossibleObject<T>(string objectName) where T : IGameObject
    {
        if (Instance.pooledObjectPoolsDict.TryGetValue(objectName, out var pool))
        {
            IGameObject obj = pool.GetObject<T>();
            return (T)obj;
        }
        if (Instance.lazyObjectsDict.ContainsKey(objectName))
        {
            return GetTempObjectPool(objectName).GetObject<T>();
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return default(T);
    }
    public static T GetObject<T>(string objectName) where T : IGameObject
    {
        if (Instance.pooledObjectPoolsDict.TryGetValue(objectName, out var pool))
        {
            IGameObject obj = pool.GetObject<T>();
            return (T)obj;
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return default(T);
    }
    public static T GetObjectReadOnly<T>(string objectName) where T : IGameObject
    {
        if (Instance.pooledObjectPoolsDict.TryGetValue(objectName, out var pool))
        {
            IGameObject obj = pool.GetObject<T>();
            return (T)obj;
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return default(T);
    }
    public static T[] GetObjects<T>(string objectName, int count) where T : IGameObject
    {
        if (count < 0) return null;
        if (Instance.pooledObjectPoolsDict.TryGetValue(objectName, out var pool))
        {
            T[] ts = new T[count];
            for (int i = 0; i < count; i++)
            {
                IGameObject obj = pool.GetObject<T>();
                ts[i] = (T)obj;
            }
            return ts;
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return null;
    }
    public static IGameObject GetObject(string objectName)
    {
        var name = objectName;
        if (Instance.pooledObjectPoolsDict.TryGetValue(name, out var pool))
        {
            IGameObject obj = pool.GetObject();
            return obj;
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return null;
    }
    public static void ReturnObjectToPool(IGameObject obj)
    {
        var name = new StringName(obj.Info.ObjectName);
        if (Instance.pooledObjectPoolsDict.TryGetValue(name, out var pool))
        {
            pool.ReturnObjectToPool(obj);
            return;
        }
        if (Instance.tempPooledObjectPoolsDict.TryGetValue(name,out var pool1))
        {
            pool1.ReturnObjectToPool(obj);
            return;
        }
        GD.PrintErr($"返回时无法找到对应的对象池:{obj.Info.ObjectName}");
    }
    public static List<T> GetAllReadOnly<T>() where T : IGameObject
    {
        List<T> list = new List<T>();
        foreach (var pool in Instance.pooledObjectPoolsDict.Values)
        {
            if (pool.GetObjectReadOnly() is T)
            {
                list.Add(pool.GetObjectReadOnly<T>());
            }
        }
        return list;
    }
    public static List<ObjectPool> GetAllPools<T>() where T : IGameObject
    {
        List<ObjectPool> list = new List<ObjectPool>();
        foreach (var pool in Instance.pooledObjectPoolsDict.Values)
        {
            if (pool.GetObjectReadOnly() is T)
            {
                list.Add(pool);
            }
        }
        return list;
    }
    public static bool HasObjectPool(string name)
    {
        return Instance.pooledObjectPoolsDict.ContainsKey(name);
    }
}
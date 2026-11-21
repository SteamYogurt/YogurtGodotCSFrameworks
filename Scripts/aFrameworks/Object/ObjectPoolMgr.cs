using System.Collections.Generic;
using System.IO;
using Godot;

public partial class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    public const string PooledRoot = "res://Assets/Objs/aPooledObjs/";
    public const string LazyRoot = "res://Assets/Objs/bLazyObjs/";

    public Dictionary<string, ObjectPool> pooledObjectPoolsDict = new Dictionary<string, ObjectPool>(128);
    public Dictionary<string, string> lazyObjectsDict = new Dictionary<string, string>(128);
    public Dictionary<string, ObjectPool> tempPooledObjectPoolsDict = new Dictionary<string, ObjectPool>(16);
    public override void _EnterTree()
    {
        base._EnterTree();
        LoadPooled(PooledRoot);
        LoadLazy(LazyRoot);
    }

    public override void _ExitTree()
    {
        foreach (var pool in pooledObjectPoolsDict.Values)
            pool.Free();
        foreach (var pool in tempPooledObjectPoolsDict.Values)
            pool.Free();
        pooledObjectPoolsDict.Clear();
        tempPooledObjectPoolsDict.Clear();
        lazyObjectsDict.Clear();
        base._ExitTree();
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
    public static bool Exists(string objectName) => ExistPossibleObject(objectName);

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
    public static T Spawn<T>(string objectName) where T : IGameObject
    {
        if (Instance.pooledObjectPoolsDict.TryGetValue(objectName, out var pool))
        {
            return pool.GetObject<T>();
        }
        if (Instance.lazyObjectsDict.ContainsKey(objectName))
        {
            ObjectPool lazyPool = GetTempObjectPool(objectName);
            if (lazyPool == null)
            {
                return default;
            }
            return lazyPool.GetObject<T>();
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return default;
    }

    public static void Release(IGameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        ObjectPool owned = obj.Info?.Pool;
        if (owned != null)
        {
            owned.ReturnObjectToPool(obj);
            return;
        }

        string name = obj.Info?.ObjectName;
        if (string.IsNullOrEmpty(name))
        {
            GD.PrintErr("返回时对象没有名字，无法找到对象池");
            return;
        }

        if (Instance.pooledObjectPoolsDict.TryGetValue(name, out ObjectPool pool))
        {
            pool.ReturnObjectToPool(obj);
            return;
        }
        if (Instance.tempPooledObjectPoolsDict.TryGetValue(name, out ObjectPool tempPool))
        {
            tempPool.ReturnObjectToPool(obj);
            return;
        }
        GD.PrintErr($"返回时无法找到对应的对象池:{name}");
    }

    public static T GetPossibleObject<T>(string objectName) where T : IGameObject
        => Spawn<T>(objectName);
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
            return pool.GetObjectReadOnly<T>();
        }
        GD.PrintErr($"无法找到对应的对象池:{objectName}");
        return default;
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
    public static void ReturnObjectToPool(IGameObject obj) => Release(obj);
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

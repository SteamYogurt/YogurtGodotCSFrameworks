using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;

public partial class ObjectPool
{
    public bool valid = false;
    public ObjectPool(string path)
    {
        this.objectPath = path;
        this.packedScene = GD.Load<PackedScene>(path);
        IGameObject obj = packedScene.Instantiate<IGameObject>();
        if(obj == null)
        {
            GD.PrintErr("pool obj实例化失败");
            return;
        }
        if(obj.Info == null)
        {
            GD.PrintErr("未手动生成Info，共享资源加载失败！");
            // 这个必须手动生成，赋值默认新生成godot编辑器没有实现，一个根场景下返回的都是同一个对象，容易bug
            return;
        }
        valid = true;
        obj.Info.ObjectName = GetObjName(objectPath);
        //GD.Print("加载了："+obj.Info.ObjectName);
        objectName = obj.Info.ObjectName;
        obj.Info.Pool = this;
        obj.Info.DirPath = GetObjDir(objectPath);
        obj.OnLoad();
        entities = new Queue<IGameObject>();
        for (int i = 0; i < 1; i++)
        {
            if (i == 0)
            {
                entities.Enqueue(obj);
                obj.OnInstantiate();
                continue;
            }
            var instance = packedScene.Instantiate<IGameObject>();
            entities.Enqueue(instance);
            instance.OnInstantiate();
        }
    }
    public static string GetObjName(string objectPath)
    {
        return Path.GetFileNameWithoutExtension(objectPath);
    }
    public static string GetObjDir(string objectPath)
    {
        return objectPath.Substring(0, objectPath.Length - Path.GetFileName(objectPath).Length - 1);
    }
    string objectPath;
    PackedScene packedScene;
    public StringName objectName;
    Queue<IGameObject> entities;
    public T GetObject<T>() where T : IGameObject
    {
        CheckAndFill();
        T obj = (T)entities.Dequeue();
        return obj;
    }
    public T GetObjectReadOnly<T>() where T : IGameObject
    {
        CheckAndFill();
        return (T)entities.Peek();
    }
    public IGameObject GetObject()
    {
        CheckAndFill();
        IGameObject obj = entities.Dequeue();
        return obj;
    }
    public IGameObject GetObjectReadOnly()
    {
        CheckAndFill();
        return entities.Peek();
    }

    void CheckAndFill()
    {
        if (entities.Count == 0)
        {
            for (int i = 0; i < 1; i++)
            {
                var instance = packedScene.Instantiate<IGameObject>();
                entities.Enqueue(instance);
                instance.OnInstantiate();
            }
        }
    }
    public void ReturnObjectToPool(IGameObject obj)
    {
        entities.Enqueue(obj);
    }
    public void Free()
    {
        foreach (var entity in entities)
        {
            if(entity is Node node)
            {
                node.QueueFree();
            }
        }
        entities.Clear();
    }
}

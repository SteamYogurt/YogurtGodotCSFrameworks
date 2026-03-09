using Godot;
using System;
using System.Collections.Generic;

public static class Global
{
    public static T GetObj<T>(string path) where T : class
    {
        return GD.Load<PackedScene>(path).Instantiate<T>();
    }


    public static Random random;
    /// <summary>
    /// 返回0到count-1
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>s
    public static int GetRandIndex(int count)
    {
        if (count < 1) return 0;
        if (random == null) random = new Random((int)GD.Randi());
        return random.Next(count);
    }


    public static float GetVolValue(float db)
    {
        if (db >= 0.0f)
        {
            return 50.0f + (db / 6.0f) * 50.0f;
        }
        else
        {
            float v = Mathf.Pow(10.0f, db / 20.0f);
            return v * 50.0f;
        }
    }
    public static float GetDb(float val)
    {
        if (val >= 50.0f)
        {
            return (val - 50.0f) / 50.0f * 6.0f;
        }
        else if (val > 0.0f)
        {
            float v = val / 50.0f; // 0~1
            return 20.0f * Mathf.Log(v);
        }
        else
        {
            return -80.0f;
        }
    }

    public static string GetPercentage(this float val)
    {
        return (val * 100).ToString("F0") + "%";
    }

    public static Vector3 XYZ(this Vector2 v)
    {
        return new Vector3(v.X, 0, v.Y);
    }
    public static Vector2 XZ(this Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }
    public static Quaternion FaceToQuat(this Vector3 face)
    {
        if (face == Vector3.Zero) return Quaternion.Identity;
        Vector3 forward = face.Normalized();
        Vector3 right = Vector3.Up.Cross(forward).Normalized();
        Vector3 up = forward.Cross(right);
        return new Basis(right, up, forward).GetRotationQuaternion();
    }
    public static List<MeshInstance3D> GetAllMeshInstances(Node root)
    {
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
        AccumulateMeshInstances(root, meshInstances);
        return meshInstances;
    }

    private static void AccumulateMeshInstances(Node node, List<MeshInstance3D> list)
    {
        // 1. 核心改动：如果节点名字以 '_' 结尾，直接跳过该节点及其所有子节点
        if (node.Name.ToString().EndsWith("_"))
        {
            return;
        }

        // 2. 如果当前节点是 MeshInstance3D，加入列表
        if (node is MeshInstance3D mesh)
        {
            list.Add(mesh);
        }

        // 3. 递归遍历所有子节点
        foreach (Node child in node.GetChildren())
        {
            AccumulateMeshInstances(child, list);
        }
    }
    public static void TakeScreenshot()
    {
        // 1. 获取当前视口的图像
        Image image = Main.Instance.GetViewport().GetTexture().GetImage();

        // 2. 构造文件名（建议使用时间戳防止重复）
        string datetime = Time.GetDatetimeStringFromSystem().Replace(":", "-");
        string fileName = $"user://screen/shot_{datetime}.png";

        // 3. 保存图片
        Error error = image.SavePng(fileName);

        if (error == Error.Ok)
        {
            GD.Print($"截图成功！保存路径: {ProjectSettings.GlobalizePath(fileName)}");
        }
        else
        {
            GD.PrintErr($"截图失败: {error}");
        }
    }
}

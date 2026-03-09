using Godot;
using System;

public partial class TempPlayer3D : AudioStreamPlayer3D, IGameObject
{
    [Export]
    public ObjectInfo Info { get; set; }
    public static ObjectPool audioPool;
    public static void PlayAudio(Vector3 position, AudioStream audio,float db = 0)
    {
        if (audioPool == null) audioPool = ObjectPoolManager.GetObjectPool("TempPlayer3D");
        var player = audioPool.GetObject<TempPlayer3D>();
        player.Stream = audio;
        player.VolumeDb = db;
        player.Position = position;
        Main.Instance.AddChild(player);
        player.Play();
    }
    public static void PlayAudio(Vector3 position, string audioPath, float db = 0)
    {
        if (ResourceLoader.Exists(audioPath))
        {
            TempPlayer3D.PlayAudio(position, GD.Load<AudioStream>(audioPath), db);
        }
        else
        {
            GD.PrintErr("播放音乐资源路径不存在：" + audioPath);
        }
    }
    public override void _Ready()
    {
        Bus = "eff";
        ProcessMode = ProcessModeEnum.Always;
        Finished += () =>
        {
            GetParent().RemoveChild(this);
            audioPool.ReturnObjectToPool(this);
        };
    }
}

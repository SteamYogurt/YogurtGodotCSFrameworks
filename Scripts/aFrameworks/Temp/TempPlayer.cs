using Godot;
using System;

public partial class TempPlayer : AudioStreamPlayer, IGameObject
{
    [Export]
    public ObjectInfo Info { get; set; }
    public static ObjectPool audioPool;
    public static void PlayAudio(AudioStream audio, float db = 0)
    {
        if (audioPool == null) audioPool = ObjectPoolManager.GetObjectPool("TempPlayer");
        var player = audioPool.GetObject<TempPlayer>();
        player.Stream = audio;
        player.VolumeDb = db;
        Main.Instance.AddChild(player);
        player.Play();
    }
    public static void PlayAudio(string audioPath,float db = 0)
    {
        if (ResourceLoader.Exists(audioPath))
        {
            TempPlayer.PlayAudio(GD.Load<AudioStream>(audioPath), db);
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

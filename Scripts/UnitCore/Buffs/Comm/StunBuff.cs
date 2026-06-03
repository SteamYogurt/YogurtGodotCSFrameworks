using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class StunBuff : Buff
{
    const MonsterControlFlag StunFlags =
        MonsterControlFlag.DisableMove
        | MonsterControlFlag.DisableAttack
        | MonsterControlFlag.DisableThink;

    public override void OnEnter(BuffInstance instance)
    {
        if (instance.Owner is not IMonsterControlHost host)
        {
            instance.IsFinished = true;
            return;
        }

        host.MonsterControlController.AddFlags(StunFlags);
        instance.AddCleanup(() => host.MonsterControlController.RemoveFlags(StunFlags));
    }

    public override string GetBuffDes()
    {
        if (buffInfo != null && buffInfo.overrideBuffDes && !string.IsNullOrEmpty(buffInfo.buffDes))
        {
            return Tr(buffInfo.buffDes);
        }

        var lines = new List<string>
        {
            Tr("无法行动")
        };

        string stackStr = GetStackStr();
        if (!string.IsNullOrEmpty(stackStr))
        {
            lines.Add(stackStr);
        }

        return string.Join("\n", lines);
    }
}
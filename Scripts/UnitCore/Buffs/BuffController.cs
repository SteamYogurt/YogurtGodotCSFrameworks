using System.Collections.Generic;
using System.Linq;
using Godot;

public class BuffController
{
    private IUnit _owner;
    private List<BuffInstance> _activeBuffs = new();
    public List<BuffInstance> ActiveBuffs => _activeBuffs;

    public BuffController(IUnit owner)
    {
        _owner = owner;
        UpdateVisuals();
    }

    public void Update(float delta)
    {
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = _activeBuffs[i];
            buff.Data.OnTick(buff, delta);

            if (buff.GetResolvedDuration() > 0f)
            {
                buff.DurationTimer -= delta;
                if (buff.DurationTimer <= 0)
                {
                    buff.IsFinished = true;
                }
            }
        }

        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            if (_activeBuffs[i].IsFinished)
            {
                RemoveBuff(_activeBuffs[i]);
            }
        }
    }

    public void Reset()
    {
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            RemoveBuff(_activeBuffs[i]);
        }
    }

    public void AddBuff(Buff buffData, object caster, int stacks = 1)
    {
        if (buffData.BuffID == null)
        {
            GD.PrintErr("施加buff时 buff缺少id");
            return;
        }
        if (!_owner.CanReceiveBuff(buffData))
        {
            return;
        }

        var existingBuff = _activeBuffs.FirstOrDefault(b => b.Data.BuffID == buffData.BuffID);

        if (existingBuff != null)
        {
            existingBuff.UpdateCaster(caster);
            existingBuff.RefreshResolvedBuffValues();
            existingBuff.Data.OnRefresh(existingBuff, stacks);
        }
        else
        {
            var newBuff = new BuffInstance(buffData, _owner, caster, stacks);
            _activeBuffs.Add(newBuff);
            newBuff.Data.OnEnter(newBuff);
        }

        UpdateVisuals();
    }

    public void RemoveBuff(BuffInstance buff)
    {
        buff.Data.OnExit(buff);
        buff.CleanupAll();
        _activeBuffs.Remove(buff);

        UpdateVisuals();
    }

    public void RemoveBuffByID(StringName buffId)
    {
        var target = _activeBuffs.FirstOrDefault(b => b.Data.BuffID == buffId);
        if (target != null)
        {
            RemoveBuff(target);
        }
    }

    public bool HasBuff(StringName buffId)
    {
        if (buffId == null)
        {
            return false;
        }

        return _activeBuffs.Any(b => b.Data.BuffID == buffId);
    }

    public bool HasBuff(string buffId)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return false;
        }

        return _activeBuffs.Any(b => b.Data.BuffID.ToString() == buffId);
    }

    public bool HasBuffTag(BuffTag tag)
    {
        if (tag == BuffTag.None)
        {
            return false;
        }

        return _activeBuffs.Any(b => b.Data?.buffInfo != null && b.Data.buffInfo.tag == tag);
    }

    public int CountBuffTag(BuffTag tag)
    {
        if (tag == BuffTag.None)
        {
            return 0;
        }

        return _activeBuffs.Count(b => b.Data?.buffInfo != null && b.Data.buffInfo.tag == tag);
    }

    public void RefreshAllBuffModifierValues()
    {
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            BuffInstance buff = _activeBuffs[i];
            if (buff == null || buff.Data == null)
            {
                continue;
            }

            buff.RefreshResolvedBuffValues();
            buff.Data.OnStackChanged(buff);
        }

        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        var topBuff = _activeBuffs
            .Where(b => b.Data.buffInfo.changeColor)
            .OrderByDescending(b => b.Data.buffInfo.visualPriority)
            .FirstOrDefault();

        if (topBuff != null)
        {
            ApplyColor(topBuff.Data.buffInfo.color);
        }
        else
        {
            ApplyColor(new Color(1, 1, 1, 1));
        }
    }

    private void ApplyColor(Color col)
    {
        var list = _owner.GetVisualMeshes();
        if (list == null)
        {
            return;
        }

        foreach (var item in list)
        {
            if (GodotObject.IsInstanceValid(item))
            {
                item.SetInstanceShaderParameter("alb_mul", col);
            }
        }
    }
}
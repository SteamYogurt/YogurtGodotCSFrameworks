using System.Collections.Generic;
using Godot;

public class BuffController
{
    private IUnit _owner;
    private readonly List<BuffInstance> _activeBuffs = new();
    private readonly Dictionary<StringName, BuffInstance> _buffsById = new();

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
            BuffInstance buff = _activeBuffs[i];
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

        BuffInstance existingBuff = FindBuffById(buffData.BuffID);
        if (existingBuff != null)
        {
            existingBuff.UpdateCaster(caster);
            existingBuff.RefreshResolvedBuffValues();
            existingBuff.Data.OnRefresh(existingBuff, stacks);
        }
        else
        {
            BuffInstance newBuff = new BuffInstance(buffData, _owner, caster, stacks);
            _activeBuffs.Add(newBuff);
            RegisterBuffInstance(newBuff);
            newBuff.Data.OnEnter(newBuff);
        }

        UpdateVisuals();
    }

    public void RemoveBuff(BuffInstance buff)
    {
        if (buff == null)
        {
            return;
        }

        buff.Data.OnExit(buff);
        buff.CleanupAll();
        UnregisterBuffInstance(buff);
        _activeBuffs.Remove(buff);

        UpdateVisuals();
    }

    public void RemoveBuffByID(StringName buffId)
    {
        BuffInstance target = FindBuffById(buffId);
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

        return _buffsById.ContainsKey(buffId);
    }

    public bool HasBuff(string buffId)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return false;
        }

        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            BuffInstance buff = _activeBuffs[i];
            if (buff?.Data != null && buff.Data.BuffID.ToString() == buffId)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasBuffTag(BuffTag tag)
    {
        if (tag == BuffTag.None)
        {
            return false;
        }

        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            BuffInstance buff = _activeBuffs[i];
            if (buff?.Data?.buffInfo != null && buff.Data.buffInfo.tag == tag)
            {
                return true;
            }
        }

        return false;
    }

    public int CountBuffTag(BuffTag tag)
    {
        if (tag == BuffTag.None)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            BuffInstance buff = _activeBuffs[i];
            if (buff?.Data?.buffInfo != null && buff.Data.buffInfo.tag == tag)
            {
                count++;
            }
        }

        return count;
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
        BuffInstance topBuff = null;
        int topPriority = int.MinValue;

        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            BuffInstance buff = _activeBuffs[i];
            if (buff?.Data?.buffInfo == null || !buff.Data.buffInfo.changeColor)
            {
                continue;
            }

            int priority = buff.Data.buffInfo.visualPriority;
            if (topBuff == null || priority > topPriority)
            {
                topBuff = buff;
                topPriority = priority;
            }
        }

        if (topBuff != null)
        {
            ApplyColor(topBuff.Data.buffInfo.color);
        }
        else
        {
            ApplyColor(new Color(1, 1, 1, 1));
        }
    }

    BuffInstance FindBuffById(StringName buffId)
    {
        if (buffId == null)
        {
            return null;
        }

        return _buffsById.TryGetValue(buffId, out BuffInstance buff) ? buff : null;
    }

    void RegisterBuffInstance(BuffInstance buff)
    {
        if (buff?.Data?.BuffID == null)
        {
            return;
        }

        _buffsById[buff.Data.BuffID] = buff;
    }

    void UnregisterBuffInstance(BuffInstance buff)
    {
        if (buff?.Data?.BuffID == null)
        {
            return;
        }

        if (_buffsById.TryGetValue(buff.Data.BuffID, out BuffInstance registered)
            && ReferenceEquals(registered, buff))
        {
            _buffsById.Remove(buff.Data.BuffID);
        }
    }

    private void ApplyColor(Color col)
    {
        List<MeshInstance3D> list = _owner.GetVisualMeshes();
        if (list == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            MeshInstance3D item = list[i];
            if (GodotObject.IsInstanceValid(item))
            {
                item.SetInstanceShaderParameter("alb_mul", col);
            }
        }
    }
}

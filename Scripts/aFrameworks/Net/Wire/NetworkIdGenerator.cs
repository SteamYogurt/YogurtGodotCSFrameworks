using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class NetworkIdGenerator
{
    private uint _currentId = 1;
    public uint PeekNextId() => _currentId;
    private HashSet<uint> _usedIds = new();
    public uint GetNextId()
    {
        while (_usedIds.Contains(_currentId))
        {
            _currentId++;
            if (_currentId == 0) _currentId = 1;
        }
        _usedIds.Add(_currentId);
        return _currentId++;
    }
    public void ReleaseId(uint id) => _usedIds.Remove(id);
    public void SetUsed(uint id) => _usedIds.Add(id);
}

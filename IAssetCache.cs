using System;
using System.Collections.Generic;

namespace IAssetCacheJB
{
    public interface IAssetCache
    {
        public object Build(string path, Action interruptChecker);

        public void Merge(string path, object result);
        
        public int GetLocalAnchorUsages(ulong anchor);
        
        public int GetGuidUsages(string guid);
        
        public IEnumerable<ulong> GetComponentsFor(ulong gameObjectAnchor);
    }
}
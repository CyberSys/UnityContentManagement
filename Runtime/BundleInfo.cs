using System.Collections.Generic;
using System;
using UnityEngine;

[Serializable]
public class BundleInfo
{
    public string Name;
    public uint CRC;
    public AssetInfo AssetInfo;

    [HideInInspector]
    [SerializeField]
    public List<BundleInfo> Dependencies = new List<BundleInfo>();

    public override bool Equals(object obj)
    {
        if (obj is BundleInfo bi) return Name == bi.Name && AssetInfo.guid == bi.AssetInfo.guid && AssetInfo.path == bi.AssetInfo.path;

        return false;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
}

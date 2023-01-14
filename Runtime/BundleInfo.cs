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
}

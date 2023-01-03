using System.Collections.Generic;
using System;

[Serializable]
public class DependencyInfo
{
    public string BundleName;
    public AssetInfo AssetInfo;
    public List<DependencyInfo> Dependencies = new List<DependencyInfo>();
}

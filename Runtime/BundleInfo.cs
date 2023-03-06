using System;
using System.Collections.Generic;
using UnityEngine;

public interface IBundleInfo
{
    public string GetName();
    public string GetGroupName();
    public uint GetCRC();
    public string GetHash128();
    public List<DependencyBundleInfo> GetDependencies();
    public bool IsDependency();

}

[Serializable]
public class BundleInfo : IBundleInfo
{
    public string name;
    public string groupName;
    public uint CRC;
    public string Hash128;
    public List<DependencyBundleInfo> Dependencies = new List<DependencyBundleInfo>();

    public override bool Equals(object obj)
    {
        if (obj is BundleInfo bi) return name == bi.name;

        return false;
    }

    public override int GetHashCode()
    {
        return name.GetHashCode();
    }

    string IBundleInfo.GetHash128()
    {
        return Hash128;
    }

    uint IBundleInfo.GetCRC()
    {
        return CRC;
    }

    List<DependencyBundleInfo> IBundleInfo.GetDependencies()
    {
        return Dependencies;
    }

    string IBundleInfo.GetGroupName()
    {
        return groupName;
    }

    string IBundleInfo.GetName()
    {
        return name;
    }

    bool IBundleInfo.IsDependency()
    {
        return false;
    }
}

[Serializable]
public class DependencyBundleInfo : IBundleInfo
{
    public string name;
    public string groupName;
    public uint CRC;
    public string Hash128;

    public override bool Equals(object obj)
    {
        if (obj is DependencyBundleInfo dbi) return name == dbi.name;
        if (obj is BundleInfo bi) return name == bi.name;

        return false;
    }

    public override int GetHashCode()
    {
        return name.GetHashCode();
    }

    string IBundleInfo.GetHash128()
    {
        return Hash128;
    }

    uint IBundleInfo.GetCRC()
    {
        return CRC;
    }

    string IBundleInfo.GetName()
    {
        return name;
    }

    string IBundleInfo.GetGroupName()
    {
        return groupName;
    }

    List<DependencyBundleInfo> IBundleInfo.GetDependencies()
    {
        return null;
    }

    bool IBundleInfo.IsDependency()
    {
        return true;
    }
}

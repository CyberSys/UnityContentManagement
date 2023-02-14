using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Build.Pipeline;

public partial class ContentDatabase : ScriptableObject
{
    public bool TryGetChain(string guid, out BundleInfo chain)
    {
        chain = default;

        foreach(var c in m_ContentInfo.Chains)
        {
            if(c.AssetInfo.guid == guid)
            {
                chain = c;
                return true;
            }
        }

        return false;
    }

    public void GenerateChains(CompatibilityAssetBundleManifest manifest)
    {
#if UNITY_EDITOR
        m_ContentInfo.Chains.Clear();

        foreach (var guid in manifest.GetAllAssetBundles())
        {
            AssetInfo info;
            if (TryGetAssetInfoByGUID(guid, out info))
            {
                var di = new BundleInfo();
                di.AssetInfo = info;
                di.CRC = manifest.GetAssetBundleCrc(guid);
                var array = manifest.GetAllDependencies(info.guid);

                for (int i = 0; i < array.Length; i++)
                {
                    if (TryGetAssetInfoByGUID(array[i], out AssetInfo asset_info2))
                    {
                        var new_dep = new BundleInfo();
                        new_dep.AssetInfo = asset_info2;
                        new_dep.CRC = manifest.GetAssetBundleCrc(asset_info2.guid);
                        di.Dependencies.Add(new_dep);
                    }
                }

                m_ContentInfo.Chains.Add(di);
            }
        }
#endif
    }

    void RenameDependencyChains(Dictionary<string, string> dictionary)
    {
#if UNITY_EDITOR
        void Rename(BundleInfo info)
        {
            if (dictionary.ContainsKey(info.AssetInfo.guid))
            {
                info.Name = dictionary[info.AssetInfo.guid];
            }
            else
            {
                info.Name = "<unknown>";
            }
            for (int i = 0; i < info.Dependencies.Count; i++)
            {
                Rename(info.Dependencies[i]);
            }
        }

        for (int i = 0; i < m_ContentInfo.Chains.Count; i++)
        {
            Rename(m_ContentInfo.Chains[i]);
        }
#endif
    }
}

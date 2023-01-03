using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Build.Pipeline;

public partial class ContentDatabase : ScriptableObject
{
    [SerializeField]
    private List<DependencyInfo> m_Chains = new List<DependencyInfo>();
    
    void GenerateDependencyChains(CompatibilityAssetBundleManifest manifest)
    {
#if UNITY_EDITOR
        m_Chains.Clear();

        void CollectDependencies(ref DependencyInfo di, AssetInfo asset_info, bool direct = false)
        {
            var array = direct ?
                manifest.GetDirectDependencies(bundleNameMode == BundlePlaceMode.RawPath ? asset_info.path : asset_info.guid) : 
                manifest.GetAllDependencies(bundleNameMode == BundlePlaceMode.RawPath ? asset_info.path : asset_info.guid);

            for (int i = 0; i < array.Length; i++)
            {
                AssetInfo asset_info2;
                if (bundleNameMode == BundlePlaceMode.RawPath ? TryGetAssetInfoByPath(array[i], out asset_info2) : TryGetAssetInfoByGUID(array[i], out asset_info2))
                {
                    var new_dep = new DependencyInfo();
                    new_dep.AssetInfo = asset_info2;
                    di.Dependencies.Add(new_dep);
                    CollectDependencies(ref new_dep, new_dep.AssetInfo, true);
                }
            }
        }

        foreach (var guid in manifest.GetAllAssetBundles())
        {
            AssetInfo info;
            if (bundleNameMode == BundlePlaceMode.RawPath ? TryGetAssetInfoByPath(guid, out info) : TryGetAssetInfoByGUID(guid, out info))
            {
                var di = new DependencyInfo();
                di.AssetInfo = info;
                CollectDependencies(ref di, di.AssetInfo);
                m_Chains.Add(di);
            }
        }
#endif
    }

    void RenameDependencyChains(Dictionary<string, string> dictionary)
    {
#if UNITY_EDITOR
        void Rename(DependencyInfo info)
        {
            if (dictionary.ContainsKey(bundleNameMode == BundlePlaceMode.RawPath ? info.AssetInfo.path : info.AssetInfo.guid))
            {
                info.BundleName = dictionary[bundleNameMode == BundlePlaceMode.RawPath ? info.AssetInfo.path : info.AssetInfo.guid];
            }
            else
            {
                info.BundleName = "<unknown>";
            }
            for (int i = 0; i < info.Dependencies.Count; i++)
            {
                Rename(info.Dependencies[i]);
            }
        }

        for (int i = 0; i < m_Chains.Count; i++)
        {
            Rename(m_Chains[i]);
        }
#endif
    }
}

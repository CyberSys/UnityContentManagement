using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Build.Pipeline;

public partial class ContentDatabase : ScriptableObject
{
    public bool TryGetBundle(ContentInfo.Group group, out IBundleInfo chain, out ContentInfo contentInfo)
    {
        chain = null;
        contentInfo = null;

        for(var i = 0; i < m_ContentInfo.Bundles.Count; i++)
        {
            var bundle = m_ContentInfo.Bundles[i];

            if(bundle != null)
            {
                if(bundle.groupName == group.name)
                {
                    contentInfo = m_ContentInfo;
                    chain = bundle;
                    return true;
                }
            }
        }

        //find in custom
        for (var a = 0; a < m_ContentInfo.CustomContentInfo.Count; a++)
        {
            var custom = m_ContentInfo.CustomContentInfo[a];

            if (custom != null)
            {
                for (var i = 0; i < custom.Bundles.Count; i++)
                {
                    var bundle = custom.Bundles[i];

                    if (bundle != null)
                    {
                        if (bundle.groupName == group.name)
                        {
                            contentInfo = m_ContentInfo.CustomContentInfo[a];
                            chain = bundle;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
#if UNITY_EDITOR
    public void GenerateBundlesInfo(CompatibilityAssetBundleManifest manifest, ContentInfo contentInfo, Dictionary<string, string> bundle_name_to_group_name)
    {
        contentInfo.Bundles.Clear();

        foreach (var name in manifest.GetAllAssetBundles())
        {
            var bundle = new BundleInfo();
            bundle.name = name;
            bundle.groupName = bundle_name_to_group_name[name];
            bundle.CRC = manifest.GetAssetBundleCrc(name);

            var deps = manifest.GetAllDependencies(name);

            foreach (var dep_name in deps)
            {
                var dep_bundle = new DependencyBundleInfo();
                dep_bundle.name = dep_name;
                dep_bundle.groupName = bundle_name_to_group_name[dep_name];
                dep_bundle.CRC = manifest.GetAssetBundleCrc(dep_name);
                bundle.Dependencies.Add(dep_bundle);
            }

            contentInfo.Bundles.Add(bundle);
        }
    }
#endif

}

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Build.Pipeline;

public partial class ContentDatabase : ScriptableObject
{
    public bool TryGetBundle(ContentInfo.Group group, out IBundleInfo chain)
    {
        chain = null;

        foreach (var c in m_ContentInfo.Bundles)
        {
            if (c.groupName == group.name)
            {
                chain = c;
                return true;
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

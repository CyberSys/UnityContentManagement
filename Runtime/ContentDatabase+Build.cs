#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class ContentDatabase : ScriptableObject, IPostprocessBuildWithReport
{
    public enum BundleNamingMode
    {
        GroupName,
        MD5Name,
        ByOrderName,
        ByGroupNameHashCode
    }

    [Header("Use Editor Database")]
    public bool UseEditorDatabase = false;

    [SerializeField]
    private BundleNamingMode m_BundleNamingMode = BundleNamingMode.GroupName;

    [Header("Bundle File Extension")]
    [SerializeField]
    private string Extension = "bundle";

    [Header("Force Rebuild (slow!!)")]
    [SerializeField]
    private bool ForceRebuild = true;

    public enum CompressionType
    {
        Uncompressed,
        LZMA,
        LZ4
    }

    [Header("Compression Mode")]
    [Header("Uncompressed (speed++) (size++)")]
    [Header("LZ4 (speed+) (size-)")]
    [Header("LZMA (speed--) (size--)")]
    [SerializeField]
    private CompressionType Compression = CompressionType.Uncompressed;

    [Header("Don't write engine version")]
    [SerializeField]
    private bool StripUnityVersion = false;

    [Header("Remove .manifest")]
    [SerializeField]
    private bool RemoveManifest = false;

    [Header("Clear the content directory before building")]
    [SerializeField]
    private bool ClearContentDirectory = false;

    [Header("[Experimental] Generate Preview Icons")]
    [SerializeField]
    private bool GenerateIcons = false;

    void ClearDirectory(string target_dir)
    {
        string[] files = Directory.GetFiles(target_dir);
        string[] dirs = Directory.GetDirectories(target_dir);

        foreach (string file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in dirs)
        {
            ClearDirectory(dir);
        }
    }

    [ContextMenu("BuildContent")]
    public async void BuildContent()
    {

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorUtility.DisplayDialog("ContentDatabase - Building", "Unable to start archiving! There are unsaved scenes!", "Ok");
            return;
        }


        AssetDatabase.SaveAssets();

        try
        {
            var bundles = new List<AssetBundleBuild>();

            Dictionary<string, string> bundle_name_to_group_name = new Dictionary<string, string>();

            EditorUtility.DisplayProgressBar("ContentDatabase -> Preparing...", "...", 0);

            if (!Directory.Exists(ContentFolder))
            {
                EditorUtility.DisplayProgressBar("ContentDatabase -> Preparing...", "Creating folder " + ContentFolder, 0);
                Directory.CreateDirectory(ContentFolder);
            }
            else if (ClearContentDirectory)
            {
                EditorUtility.DisplayProgressBar("ContentDatabase -> Preparing...", "Clearing folder " + ContentFolder, 0);
                ClearDirectory(ContentFolder);
            }

            for (int group_index = 0; group_index < m_ContentInfo.Groups.Count; group_index++)
            {
                var group = m_ContentInfo.Groups[group_index];

                if (group != null && group.Assets.Count > 0)
                {
                    EditorUtility.DisplayProgressBar($"ContentDatabase -> Processing group {group.Name}", $"...", (float)group_index / (float)m_ContentInfo.Groups.Count);
                    var abb = new AssetBundleBuild();

                    if (m_BundleNamingMode == BundleNamingMode.GroupName)
                    {
                        abb.assetBundleName = $"{group.Name}.{Extension}";
                    }

                    if (m_BundleNamingMode == BundleNamingMode.MD5Name)
                    {
                        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                        {
                            byte[] inputBytes = Encoding.ASCII.GetBytes(group.Name);
                            byte[] hashBytes = md5.ComputeHash(inputBytes);

                            StringBuilder sb = new StringBuilder();
                            for (int i = 0; i < hashBytes.Length; i++)
                            {
                                sb.Append(hashBytes[i].ToString("x2"));
                            }
                            abb.assetBundleName = $"{sb}.{Extension}";
                        }
                    }

                    if (m_BundleNamingMode == BundleNamingMode.ByOrderName)
                    {
                        abb.assetBundleName = $"content_{group_index}.{Extension}";
                    }

                    if (m_BundleNamingMode == BundleNamingMode.ByGroupNameHashCode)
                    {
                        abb.assetBundleName = $"{group.Name.GetHashCode()}.{Extension}";
                    }

                    bundle_name_to_group_name.Add(abb.assetBundleName, group.Name);

                    abb.addressableNames = new string[group.Assets.Count];
                    abb.assetNames = new string[group.Assets.Count];

                    for (int asset_index = 0; asset_index < group.Assets.Count; asset_index++)
                    {
                        var asset = group.Assets[asset_index];

                        if (asset != null)
                        {
                            abb.assetNames[asset_index] = asset.path;
                            abb.addressableNames[asset_index] = asset.guid;
                        }

                        EditorUtility.DisplayProgressBar($"ContentDatabase -> Processing group {group.Name}", $"Processing asset {asset.path}", (float)asset_index / (float)group.Assets.Count);

                        await Task.Yield();
                    }

                    bundles.Add(abb);
                }
                else if (group != null)
                {
                    EditorUtility.DisplayProgressBar($"ContentDatabase -> Processing group {group.Name}", $"Skipping! No assets!", 0);
                }

                await Task.Yield();
            }

            var bopt = BuildAssetBundleOptions.None;

            if (Compression == CompressionType.Uncompressed)
            {
                bopt |= BuildAssetBundleOptions.UncompressedAssetBundle;
            }
            else if (Compression == CompressionType.LZ4)
            {
                bopt |= BuildAssetBundleOptions.ChunkBasedCompression;
            }
            else if (Compression == CompressionType.LZMA)
            {
                bopt |= BuildAssetBundleOptions.None;
            }

            if (StripUnityVersion)
            {
                bopt |= BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            }

            if (ForceRebuild)
            {
                bopt |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
            }

            var manifest = CompatibilityBuildPipeline.BuildAssetBundles(ContentFolder, bundles.ToArray(), bopt, EditorUserBuildSettings.activeBuildTarget);

            if (manifest != null)
            {
                //Building chains
                EditorUtility.DisplayProgressBar("ContentDatabase -> Generating", "Generating bundles chain", 0.5f);
                GenerateBundlesInfo(manifest, m_ContentInfo, bundle_name_to_group_name);
            }
            else
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            if (RemoveManifest)
            {
                var manifests = Directory.GetFiles(ContentFolder, "*.manifest*");

                for (var i = 0; i < manifests.Length; i++)
                {
                    File.Delete(manifests[i]);
                }
            }

            File.WriteAllText(ContentDBPath, JsonUtility.ToJson(m_ContentInfo, true));

            if (GenerateIcons)
            {
                var p = Path.Combine(ContentFolder, "Icons");
                if (!Directory.Exists(p))
                {
                    Directory.CreateDirectory(p);
                }

                AssetPreview.SetPreviewTextureCacheSize(2048);
                foreach(var group in m_ContentInfo.Groups)
                {
                    if(group != null)
                    {
                        var g_p = Path.Combine(p, group.Name);

                        if (!Directory.Exists(g_p))
                        {
                            Directory.CreateDirectory(g_p);
                        }

                        int asset_index = 0;
                        foreach(var asset in group.Assets)
                        {
                            if (asset != null)
                            {
                                var obj = AssetDatabase.LoadMainAssetAtPath(asset.path);
                                var icon = AssetPreview.GetAssetPreview(obj);

                                while (AssetPreview.IsLoadingAssetPreview(obj.GetInstanceID()))
                                {
                                    await Task.Yield();
                                }


                                if (icon != null)
                                {
                                    EditorUtility.DisplayProgressBar("Generating preview icons", "Processing " + asset.path, (float)asset_index / (float)group.Assets.Count);
                                    File.WriteAllBytes(Path.Combine(g_p, asset.name + ".png"), icon.EncodeToPNG());
                                }
                                else
                                {
                                    Debug.LogWarning("icon is unavailable! -> "+asset.path);
                                }
                                await Task.Yield();
                            }
                        }
                        await Task.Yield();
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.ClearProgressBar();
        }
    }


    public async void BuildContentCustom(string folderPath, List<ContentInfo.Group> groups)
    {
        AssetDatabase.SaveAssets();

        try
        {
            var temp_ContentInfo = new ContentInfo();
            temp_ContentInfo.Groups = groups;

            var bundles = new List<AssetBundleBuild>();

            Dictionary<string, string> bundle_name_to_group_name = new Dictionary<string, string>();

            EditorUtility.DisplayProgressBar("ContentDatabase -> Preparing...", "...", 0);

            if (!Directory.Exists(folderPath))
            {
                EditorUtility.DisplayProgressBar("ContentDatabase -> Preparing...", "Creating folder " + folderPath, 0);
                Directory.CreateDirectory(folderPath);
            }
            else if (ClearContentDirectory)
            {
                EditorUtility.DisplayProgressBar("ContentDatabase -> Preparing...", "Clearing folder " + folderPath, 0);
                ClearDirectory(folderPath);
            }

            for (int group_index = 0; group_index < temp_ContentInfo.Groups.Count; group_index++)
            {
                var group = temp_ContentInfo.Groups[group_index];

                if (group != null && group.Assets.Count > 0)
                {
                    EditorUtility.DisplayProgressBar($"ContentDatabase -> Processing group {group.Name}", $"...", (float)group_index / (float)temp_ContentInfo.Groups.Count);
                    var abb = new AssetBundleBuild();

                    if (m_BundleNamingMode == BundleNamingMode.GroupName)
                    {
                        abb.assetBundleName = $"{group.Name}.{Extension}";
                    }

                    if (m_BundleNamingMode == BundleNamingMode.MD5Name)
                    {
                        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                        {
                            byte[] inputBytes = Encoding.ASCII.GetBytes(group.Name);
                            byte[] hashBytes = md5.ComputeHash(inputBytes);

                            StringBuilder sb = new StringBuilder();
                            for (int i = 0; i < hashBytes.Length; i++)
                            {
                                sb.Append(hashBytes[i].ToString("x2"));
                            }
                            abb.assetBundleName = $"{sb}.{Extension}";
                        }
                    }

                    if (m_BundleNamingMode == BundleNamingMode.ByOrderName)
                    {
                        abb.assetBundleName = $"archive_{group_index}.{Extension}";
                    }

                    if (m_BundleNamingMode == BundleNamingMode.ByGroupNameHashCode)
                    {
                        abb.assetBundleName = $"{group.Name.GetHashCode()}.{Extension}";
                    }

                    bundle_name_to_group_name.Add(abb.assetBundleName, group.Name);

                    abb.addressableNames = new string[group.Assets.Count];
                    abb.assetNames = new string[group.Assets.Count];

                    for (int asset_index = 0; asset_index < group.Assets.Count; asset_index++)
                    {
                        var asset = group.Assets[asset_index];

                        if (asset != null)
                        {
                            abb.assetNames[asset_index] = asset.path;
                            abb.addressableNames[asset_index] = asset.guid;
                        }

                        EditorUtility.DisplayProgressBar($"ContentDatabase -> Processing group {group.Name}", $"Processing asset {asset.path}", (float)asset_index / (float)group.Assets.Count);

                        await Task.Yield();
                    }

                    bundles.Add(abb);
                }
                else if (group != null)
                {
                    EditorUtility.DisplayProgressBar($"ContentDatabase -> Processing group {group.Name}", $"Skipping! No assets!", 0);
                }

                await Task.Yield();
            }

            var bopt = BuildAssetBundleOptions.None;

            if (Compression == CompressionType.Uncompressed)
            {
                bopt |= BuildAssetBundleOptions.UncompressedAssetBundle;
            }
            else if (Compression == CompressionType.LZ4)
            {
                bopt |= BuildAssetBundleOptions.ChunkBasedCompression;
            }
            else if (Compression == CompressionType.LZMA)
            {
                bopt |= BuildAssetBundleOptions.None;
            }

            if (StripUnityVersion)
            {
                bopt |= BuildAssetBundleOptions.AssetBundleStripUnityVersion;
            }

            if (ForceRebuild)
            {
                bopt |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
            }

            var manifest = CompatibilityBuildPipeline.BuildAssetBundles(folderPath, bundles.ToArray(), bopt, EditorUserBuildSettings.activeBuildTarget);

            if (manifest != null)
            {
                //Building chains
                EditorUtility.DisplayProgressBar("ContentDatabase -> Generating", "Generating bundles chain", 0.5f);
                GenerateBundlesInfo(manifest,temp_ContentInfo, bundle_name_to_group_name);
            }
            else
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            if (RemoveManifest)
            {
                var manifests = Directory.GetFiles(folderPath, "*.manifest*");

                for (var i = 0; i < manifests.Length; i++)
                {
                    File.Delete(manifests[i]);
                }
            }

            var db_path = Path.Combine(folderPath, ContentInfoFileName);

            File.WriteAllText(db_path, JsonUtility.ToJson(temp_ContentInfo, true));
            EditorUtility.ClearProgressBar();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.ClearProgressBar();
        }
    }
}
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ManifestTest : MonoBehaviour
{
    AssetBundleManifest manifest;
    private void Awake()
    {
        if (File.Exists(Path.Combine(Environment.CurrentDirectory, "Content", "Content")))
        {
            manifest = AssetBundle.LoadFromFile(Path.Combine(Environment.CurrentDirectory, "Content", "Content")).LoadAsset<AssetBundleManifest>("assetbundlemanifest");

            foreach (var d in manifest.GetAllAssetBundles())
            {
                bundles.Add(new bundle()
                {
                    name = d,
                    alldep = manifest.GetAllDependencies(d),
                    directdep = manifest.GetDirectDependencies(d),
                });
            }
        }
    }

    public class bundle
    {
        public string name;
        public string[] alldep;
        public string[] directdep;
        public Vector2 scroll1;
        public bool show_all;

        public Vector2 scroll2;
        public bool show_direct;
    }

    public List<bundle> bundles = new List<bundle>();
    Vector2 scroll;
    private void OnGUI()
    {
        if (manifest)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            foreach(var i in bundles)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Box(i.name);
                i.show_all = GUILayout.Toggle(i.show_all, $"show_all ({i.alldep.Length})");
                i.show_direct = GUILayout.Toggle(i.show_direct, $"show_direct ({i.directdep.Length})");
                GUILayout.BeginVertical("box");
                if (i.show_all)
                {
                    i.scroll1 = GUILayout.BeginScrollView(i.scroll1);
                    foreach (var j in i.alldep)
                    {
                        GUILayout.Box(j);
                    }
                    GUILayout.EndScrollView();
                }
                GUILayout.EndVertical();
                GUILayout.BeginVertical("box");
                if (i.show_direct)
                {
                    i.scroll2 = GUILayout.BeginScrollView(i.scroll2);
                    foreach (var j in i.directdep)
                    {
                        GUILayout.Box(j);
                    }
                    GUILayout.EndScrollView();
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
    }
}

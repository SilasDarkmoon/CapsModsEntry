using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    public static class CapsPackageEditor
    {
        [Serializable]
        public class PackagesInfoList
        {
            public List<UnityEditor.PackageManager.PackageInfo> Packages;
        }

        private static Dictionary<string, UnityEditor.PackageManager.PackageInfo> _Packages;
        public static Dictionary<string, UnityEditor.PackageManager.PackageInfo> Packages { get { return _Packages; } }
        private static event Action _OnPackagesChanged = () => { };
        public static event Action OnPackagesChanged
        {
            add
            {
                _OnPackagesChanged += value;
                if (_Packages != null)
                {
                    value();
                }
            }
            remove
            {
                _OnPackagesChanged -= value;
            }
        }

        public static void RefreshPackages()
        {
            if (_Packages == null)
            {
                // Load cache
                if (System.IO.File.Exists("EditorOutput/Runtime/packages.txt"))
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText("EditorOutput/Runtime/packages.txt");
                        PackagesInfoList list = new PackagesInfoList();
                        EditorJsonUtility.FromJsonOverwrite(json, list);
                        List<UnityEditor.PackageManager.PackageInfo> packages = list.Packages;
                        if (packages.Count > 0)
                        {
                            var newinfos = new Dictionary<string, UnityEditor.PackageManager.PackageInfo>();
                            for (int i = 0; i < packages.Count; ++i)
                            {
                                var package = packages[i];
                                newinfos[package.name] = package;
                            }
                            _Packages = newinfos;
                            _OnPackagesChanged();
                        }
                    }
                    catch { }
                }
            }

            var req = UnityEditor.PackageManager.Client.List(true);
            EditorBridge.TerminableUpdate += () =>
            {
                if (req.IsCompleted)
                {
                    var newinfos = new Dictionary<string, UnityEditor.PackageManager.PackageInfo>();
                    foreach (var package in req.Result)
                    {
                        newinfos[package.name] = package;
                    }
                    if (_Packages == null || PackagesChanged(_Packages, newinfos))
                    {
                        // Save cache
                        try
                        {
                            System.IO.Directory.CreateDirectory("EditorOutput/Runtime/");
                            List<UnityEditor.PackageManager.PackageInfo> packages = new List<UnityEditor.PackageManager.PackageInfo>(newinfos.Values);
                            PackagesInfoList list = new PackagesInfoList();
                            list.Packages = packages;
                            var json = EditorJsonUtility.ToJson(list, true);
                            System.IO.File.WriteAllText("EditorOutput/Runtime/packages.txt", json);
                        }
                        catch { }
                        _Packages = newinfos;
                        _OnPackagesChanged();
                    }
                    //else
                    //{
                    //    _Packages = newinfos;
                    //}
                    return true;
                }
                else
                {
                    return false;
                }
            };
        }

        [MenuItem("Mods/Force Refresh Package", priority = 200000)]
        public static void ForceRefreshPackages()
        {
            _Packages = null;
            RefreshPackages();
        }

        private class PackageMonitor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (_Packages == null)
                {
                    RefreshPackages();
                    return;
                }
                if (importedAssets != null)
                {
                    for (int i = 0; i < importedAssets.Length; ++i)
                    {
                        var asset = importedAssets[i];
                        if (asset.StartsWith("Packages/"))
                        {
                            var pname = asset.Substring("Packages/".Length);
                            var index = pname.IndexOf('/');
                            if (index > 0)
                            {
                                pname = pname.Substring(0, index);
                                if (!_Packages.ContainsKey(pname))
                                {
                                    RefreshPackages();
                                    return;
                                }
                            }
                        }
                    }
                }
                if (deletedAssets != null)
                {
                    for (int i = 0; i < deletedAssets.Length; ++i)
                    {
                        var asset = deletedAssets[i];
                        if (asset.StartsWith("Packages/"))
                        {
                            var pname = asset.Substring("Packages/".Length);
                            var index = pname.IndexOf('/');
                            if (index > 0)
                            {
                                pname = pname.Substring(0, index);
                                if (asset == "Packages/" + pname + "/package.json")
                                {
                                    RefreshPackages();
                                    return;
                                }
                            }
                        }
                    }
                }
                if (movedAssets != null)
                {
                    for (int i = 0; i < movedAssets.Length; ++i)
                    {
                        var asset = movedAssets[i];
                        if (asset.StartsWith("Packages/"))
                        {
                            var pname = asset.Substring("Packages/".Length);
                            var index = pname.IndexOf('/');
                            if (index > 0)
                            {
                                pname = pname.Substring(0, index);
                                if (!_Packages.ContainsKey(pname))
                                {
                                    RefreshPackages();
                                    return;
                                }
                            }
                        }
                    }
                }
                if (movedFromAssetPaths != null)
                {
                    for (int i = 0; i < movedFromAssetPaths.Length; ++i)
                    {
                        var asset = movedFromAssetPaths[i];
                        if (asset.StartsWith("Packages/"))
                        {
                            var pname = asset.Substring("Packages/".Length);
                            var index = pname.IndexOf('/');
                            if (index > 0)
                            {
                                pname = pname.Substring(0, index);
                                if (asset == "Packages/" + pname + "/package.json")
                                {
                                    RefreshPackages();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool PackagesChanged(Dictionary<string, UnityEditor.PackageManager.PackageInfo> src, Dictionary<string, UnityEditor.PackageManager.PackageInfo> dst)
        {
            if (src.Count != dst.Count)
            {
                return true;
            }
            foreach (var kvp in dst)
            {
                UnityEditor.PackageManager.PackageInfo old;
                if (!src.TryGetValue(kvp.Key, out old))
                {
                    return true;
                }
                if (kvp.Value.resolvedPath != old.resolvedPath)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
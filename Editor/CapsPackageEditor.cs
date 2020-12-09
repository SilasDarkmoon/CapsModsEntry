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
                else
                {
                    var allassets = AssetDatabase.GetAllAssetPaths();
                    if (allassets != null)
                    {
                        HashSet<string> pnames = new HashSet<string>();
                        var newinfos = new Dictionary<string, UnityEditor.PackageManager.PackageInfo>();
                        for (int i = 0; i < allassets.Length; ++i)
                        {
                            var asset = allassets[i];
                            if (asset.StartsWith("Packages/"))
                            {
                                var sub = asset.Substring("Packages/".Length);
                                var split = sub.IndexOf('/');
                                if (split > 0)
                                {
                                    var pname = sub.Substring(0, split);
                                    if (!pnames.Contains(pname))
                                    {
                                        var pinfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asset);
                                        if (pinfo != null)
                                        {
                                            pnames.Add(pname);
                                            newinfos[pname] = pinfo;
                                        }
                                    }
                                }
                            }
                        }
                        if (newinfos.Count > 0)
                        {
                            _Packages = newinfos;
                            if (!System.IO.File.Exists("mcs.rsp"))
                            {
                                FixAssemblyReference();
                            }
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
                            _OnPackagesChanged();
                        }
                    }
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

        public static void FixAssemblyReference()
        {
            CapsModEditorEntry.FixAssemblyReference();
        }
        internal static HashSet<string> GetAllModsInternal()
        {
            HashSet<string> mods = new HashSet<string>();
            if (System.IO.Directory.Exists("Assets/Mods"))
            {
                var subs = System.IO.Directory.GetDirectories("Assets/Mods");
                if (subs != null)
                {
                    for (int i = 0; i < subs.Length; ++i)
                    {
                        var dir = subs[i];
                        mods.Add(System.IO.Path.GetFileName(dir));
                    }
                }
            }
            return mods;
        }

        [MenuItem("Mods/Force Refresh Package", priority = 200000)]
        public static void ForceRefreshPackages()
        {
            _Packages = null;
            System.IO.File.Delete("EditorOutput/Runtime/packages.txt");
            RefreshPackages();
        }

        public static void RemovePackage(string pname)
        {
            if (_Packages == null)
            {
                RefreshPackages();
                return;
            }
            if (_Packages.Remove(pname))
            {
                // Save cache
                try
                {
                    System.IO.Directory.CreateDirectory("EditorOutput/Runtime/");
                    List<UnityEditor.PackageManager.PackageInfo> packages = new List<UnityEditor.PackageManager.PackageInfo>(_Packages.Values);
                    PackagesInfoList list = new PackagesInfoList();
                    list.Packages = packages;
                    var json = EditorJsonUtility.ToJson(list, true);
                    System.IO.File.WriteAllText("EditorOutput/Runtime/packages.txt", json);
                }
                catch { }
                _OnPackagesChanged();
            }
        }
        public static void AddPackageOfAsset(string asset)
        {
            if (_Packages == null)
            {
                RefreshPackages();
                return;
            }
            var pname = asset.Substring("Packages/".Length);
            var index = pname.IndexOf('/');
            if (index > 0)
            {
                pname = pname.Substring(0, index);
                if (!_Packages.ContainsKey(pname))
                {
                    var pinfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asset);
                    if (pinfo != null)
                    {
                        _Packages[pname] = pinfo;
                        // Save cache
                        try
                        {
                            System.IO.Directory.CreateDirectory("EditorOutput/Runtime/");
                            List<UnityEditor.PackageManager.PackageInfo> packages = new List<UnityEditor.PackageManager.PackageInfo>(_Packages.Values);
                            PackagesInfoList list = new PackagesInfoList();
                            list.Packages = packages;
                            var json = EditorJsonUtility.ToJson(list, true);
                            System.IO.File.WriteAllText("EditorOutput/Runtime/packages.txt", json);
                        }
                        catch { }
                        _OnPackagesChanged();
                    }
                }
            }
        }

        private static void ResavePackageCache()
        {
            // Save cache
            try
            {
                System.IO.Directory.CreateDirectory("EditorOutput/Runtime/");
                List<UnityEditor.PackageManager.PackageInfo> packages = new List<UnityEditor.PackageManager.PackageInfo>(_Packages.Values);
                PackagesInfoList list = new PackagesInfoList();
                list.Packages = packages;
                var json = EditorJsonUtility.ToJson(list, true);
                System.IO.File.WriteAllText("EditorOutput/Runtime/packages.txt", json);
            }
            catch { }
            _OnPackagesChanged();
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
                bool changed = false;
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
                                    var pinfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asset);
                                    if (pinfo != null)
                                    {
                                        _Packages[pname] = pinfo;
                                        changed = true;
                                    }
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
                                    _Packages.Remove(pname);
                                    changed = true;
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
                                    var pinfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asset);
                                    if (pinfo != null)
                                    {
                                        _Packages[pname] = pinfo;
                                        changed = true;
                                    }
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
                                    _Packages.Remove(pname);
                                    changed = true;
                                }
                            }
                        }
                    }
                }
                if (changed)
                {
                    ResavePackageCache();
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
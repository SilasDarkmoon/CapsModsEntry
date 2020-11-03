using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    internal static class CapsModEditorEntry
    {
        private static readonly Dictionary<string, string> _PackageName2ModName = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _ModName2PackageName = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _PackageName2PackagePath = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _PackagePath2PackageName = new Dictionary<string, string>();
        private static readonly HashSet<string> _ShouldTreatAsModPackages = new HashSet<string>();
        private static void CheckPackages()
        {
            _PackageName2ModName.Clear();
            _ModName2PackageName.Clear();
            _PackageName2PackagePath.Clear();
            _PackagePath2PackageName.Clear();
            _ShouldTreatAsModPackages.Clear();

            var packages = CapsPackageEditor.Packages;
            if (packages != null)
            {
                foreach (var package in packages.Values)
                {
                    if (package.status == UnityEditor.PackageManager.PackageStatus.Available
                        && (package.source == UnityEditor.PackageManager.PackageSource.Embedded || package.source == UnityEditor.PackageManager.PackageSource.Git || package.source == UnityEditor.PackageManager.PackageSource.Local)
                        )
                    {
                        var pname = package.name;
                        var ppath = package.resolvedPath;
                        var mname = System.IO.Path.GetFileName(ppath);
                        if (mname.Contains("@"))
                        {
                            mname = mname.Substring(0, mname.IndexOf('@'));
                        }
                        var fpath = System.IO.Path.GetFullPath(ppath).Replace('\\', '/').ToLower();

                        _PackageName2ModName[pname] = mname;
                        _ModName2PackageName[mname] = pname;
                        _PackageName2PackagePath[pname] = ppath;
                        _PackagePath2PackageName[fpath] = pname;

                        if (ShouldTreatPackageAsMod(package))
                        {
                            _ShouldTreatAsModPackages.Add(pname);
                        }
                    }
                }
            }
        }

        private static readonly string[] UniqueSpecialFolders = new[] { "Plugins", "Standard Assets" };

        public static void FixAssemblyReference()
        {
            CheckPackages();

            HashSet<string> compilerOpLines = new HashSet<string>();

            var mods = GetAllModsOrPackages();
            for (int i = 0; i < mods.Length; ++i)
            {
                var mod = mods[i];
                if (!IsModOptional(mod))
                {
                    // enable
                    string defpath;
                    bool defPathExists = false;
                    var pdir = GetModRootInPackage(mod);
                    if (!string.IsNullOrEmpty(pdir))
                    {
                        defpath = pdir + "/mcs.rsp";
                        if (defPathExists = System.IO.File.Exists(defpath))
                        {
                            var pname = GetPackageName(mod);
                            compilerOpLines.Add("-define:MOD_" + pname.ToUpper().Replace(".", "_"));
                        }
                        else
                        {
                            defpath = "Assets/Mods/" + mod + "/Link/mcs.rsp";
                        }
                    }
                    else
                    {
                        defpath = "Assets/Mods/" + mod + "/Link/mcs.rsp";
                    }
                    if (defPathExists || System.IO.File.Exists(defpath))
                    {
                        compilerOpLines.Add("-define:MOD_" + mod.ToUpper().Replace(".", "_"));
                        try
                        {
                            compilerOpLines.UnionWith(System.IO.File.ReadAllLines(defpath));
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }

            compilerOpLines.Remove("");
            HashSet<string> existCompilerOpLines = new HashSet<string>();
            if (System.IO.File.Exists("Assets/mcs.rsp"))
            {
                try
                {
                    existCompilerOpLines.UnionWith(System.IO.File.ReadAllLines("Assets/mcs.rsp"));
                    existCompilerOpLines.Remove("");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            bool hasdiff = true;
            if (existCompilerOpLines.Count == compilerOpLines.Count)
            {
                var diff = new HashSet<string>(compilerOpLines);
                diff.ExceptWith(existCompilerOpLines);
                hasdiff = diff.Count > 0;
            }
            if (hasdiff)
            {
                if (System.IO.File.Exists("Assets/mcs.rsp"))
                {
                    System.IO.File.Delete("Assets/mcs.rsp");
                }
                if (System.IO.File.Exists("Assets/csc.rsp"))
                {
                    System.IO.File.Delete("Assets/csc.rsp");
                }
                var lines = compilerOpLines.ToArray();
                Array.Sort(lines);
                System.IO.File.WriteAllLines("Assets/mcs.rsp", lines);
                System.IO.File.WriteAllLines("Assets/csc.rsp", lines);
                AssetDatabase.ImportAsset("Assets/mcs.rsp");
                AssetDatabase.ImportAsset("Assets/csc.rsp");
                EditorApplication.LockReloadAssemblies();
                try
                {
                    AssetDatabase.ImportAsset(__ASSET__, ImportAssetOptions.ForceUpdate);
                }
                catch { }
                // Update all package...
                //foreach (var kvp in _PackageName2ModName)
                //{
                //    var pname = kvp.Key;
                //    AssetDatabase.ImportAsset("Packages/" + pname, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                //}
                ReimportAllAssemblyDefinitions();
                EditorApplication.UnlockReloadAssemblies();
            }
            AssetDatabase.Refresh();
        }

        public static void ReimportAllAssemblyFromCode()
        {
            var allasms = AssetDatabase.FindAssets("t:asmdef");
            HashSet<string> asmdirs = new HashSet<string>();
            for (int i = 0; i < allasms.Length; ++i)
            {
                var asmguid = allasms[i];
                var asmpath = AssetDatabase.GUIDToAssetPath(asmguid);
                if (!string.IsNullOrEmpty(asmpath))
                {
                    asmdirs.Add(System.IO.Path.GetDirectoryName(asmpath));
                }
            }
            foreach (var asmpath in asmdirs)
            {
                if (!string.IsNullOrEmpty(asmpath))
                {
                    bool firstFound = false;
                    var spts = System.IO.Directory.GetFiles(asmpath, "*.cs", System.IO.SearchOption.TopDirectoryOnly);
                    if (spts != null && spts.Length > 0)
                    {
                        AssetDatabase.ImportAsset(spts[0].Replace("\\", "/"), ImportAssetOptions.ForceUpdate);
                    }
                    else
                    {
                        spts = System.IO.Directory.GetFiles(asmpath, "*.cs", System.IO.SearchOption.AllDirectories);
                        for (int i = 0; i < spts.Length; ++i)
                        {
                            var sptpath = spts[i].Replace("\\", "/");
                            if (!string.IsNullOrEmpty(sptpath))
                            {
                                bool isInChildAsm = false;
                                string dir = sptpath;
                                try
                                {
                                    while (true)
                                    {
                                        dir = System.IO.Path.GetDirectoryName(dir);
                                        if (string.IsNullOrEmpty(dir) || dir == asmpath || dir.Length <= asmpath.Length)
                                        {
                                            break;
                                        }
                                        if (asmdirs.Contains(dir))
                                        {
                                            isInChildAsm = true;
                                            break;
                                        }
                                    }
                                }
                                catch { }
                                if (!isInChildAsm)
                                {
                                    if (!firstFound)
                                    {
                                        firstFound = true;
                                        AssetDatabase.ImportAsset(sptpath, ImportAssetOptions.ForceUpdate);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public static void ReimportAllAssemblyDefinitions()
        {
            var allasms = AssetDatabase.FindAssets("t:asmdef");
            Dictionary<string, byte[]> Contents = new Dictionary<string, byte[]>();
            for (int i = 0; i < allasms.Length; ++i)
            {
                var asmguid = allasms[i];
                var asmpath = AssetDatabase.GUIDToAssetPath(asmguid);
                if (!string.IsNullOrEmpty(asmpath))
                {
                    var content = System.IO.File.ReadAllBytes(asmpath);
                    Contents[asmpath] = content;
                    try
                    {
                        using (var stream = System.IO.File.OpenWrite(asmpath))
                        {
                            stream.Write(content, 0, content.Length);
                            stream.Write(new byte[] { (byte)'\n' }, 0, 1);
                        }
                    }
                    catch { }
                }
            }
            AssetDatabase.Refresh();
            for (int i = 0; i < allasms.Length; ++i)
            {
                var asmguid = allasms[i];
                var asmpath = AssetDatabase.GUIDToAssetPath(asmguid);
                if (!string.IsNullOrEmpty(asmpath))
                {
                    byte[] content;
                    if (Contents.TryGetValue(asmpath, out content))
                    {
                        try
                        {
                            System.IO.File.WriteAllBytes(asmpath, content);
                        }
                        catch { }
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// CapsResManager -> cn.capstones.resmanager
        /// </summary>
        public static string GetPackageName(string mod)
        {
            if (!string.IsNullOrEmpty(mod))
            {
                string pname;
                if (_ModName2PackageName.TryGetValue(mod, out pname))
                {
                    return pname;
                }
            }
            return null;
        }
        /// <summary>
        /// C:/XXXXX/CapsResManager -> cn.capstones.resmanager
        /// </summary>
        public static string GetPackageNameFromRootPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                path = System.IO.Path.GetFullPath(path).ToLower().Replace('\\', '/');
                string pname;
                if (_PackagePath2PackageName.TryGetValue(path, out pname))
                {
                    return pname;
                }
            }
            return null;
        }
        public static string GetPackageNameFromPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                path = System.IO.Path.GetFullPath(path).Replace('\\', '/');
                foreach (var kvp in _PackagePath2PackageName)
                {
                    if (path.StartsWith(kvp.Key, System.StringComparison.InvariantCultureIgnoreCase))
                    {
                        return kvp.Value;
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// cn.capstones.resmanager -> CapsResManager
        /// </summary>
        public static string GetPackageModName(string package)
        {
            if (!string.IsNullOrEmpty(package))
            {
                string mname;
                if (_PackageName2ModName.TryGetValue(package, out mname))
                {
                    return mname;
                }
            }
            return null;
        }
        /// <summary>
        /// cn.capstones.resmanager -> C:/XXXXX/CapsResManager
        /// </summary>
        public static string GetPackageRoot(string package)
        {
            if (!string.IsNullOrEmpty(package))
            {
                string path;
                if (_PackageName2PackagePath.TryGetValue(package, out path))
                {
                    return path;
                }
            }
            return null;
        }

        private static bool ShouldTreatPackageAsMod(UnityEditor.PackageManager.PackageInfo package)
        {
            //if (package.status == UnityEditor.PackageManager.PackageStatus.Available
            //    && (package.source == UnityEditor.PackageManager.PackageSource.Embedded || package.source == UnityEditor.PackageManager.PackageSource.Git || package.source == UnityEditor.PackageManager.PackageSource.Local)
            //    )
            {
                var path = package.resolvedPath;
                if (!string.IsNullOrEmpty(path))
                {
                    if (System.IO.Directory.Exists(path + "/Link~"))
                    {
                        return true;
                    }
                    if (System.IO.File.Exists(path + "/mcs.rsp"))
                    {
                        return true;
                    }
                    if (System.IO.File.Exists(path + "/Runtime/Resources/resdesc.asset"))
                    {
                        return true;
                    }
                    if (System.IO.File.Exists(path + "/Resources/resdesc.asset"))
                    {
                        return true;
                    }
                    if (System.IO.File.Exists(path + "/mod.readme.md"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool ShouldTreatPackageAsMod(string package)
        {
            return _ShouldTreatAsModPackages.Contains(package);
        }

        public static bool IsAssetInPackage(string asset)
        {
            if (!string.IsNullOrEmpty(asset))
            {
                return asset.StartsWith("Packages/");
            }
            return false;
        }
        public static string GetAssetPackage(string asset)
        {
            if (!string.IsNullOrEmpty(asset))
            {
                if (asset.StartsWith("Packages/"))
                {
                    var part = asset.Substring("Packages/".Length);
                    var iend = part.IndexOf('/');
                    if (iend >= 0)
                    {
                        part = part.Substring(0, iend);
                    }
                    return part;
                }
            }
            return null;
        }
        public static string GetAssetPath(string asset)
        {
            if (!string.IsNullOrEmpty(asset))
            {
                if (asset.StartsWith("Packages/"))
                {
                    var part = asset.Substring("Packages/".Length);
                    var iend = part.IndexOf('/');
                    if (iend >= 0)
                    {
                        var package = part.Substring(0, iend);
                        var root = GetPackageRoot(package);
                        if (!string.IsNullOrEmpty(root))
                        {
                            return root + part.Substring(iend);
                        }
                    }
                }
                else
                {
                    return asset;
                }
            }
            return null;
        }
        public static string GetAssetNameFromPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith("Assets/"))
                {
                    return path;
                }
                else
                {
                    path = System.IO.Path.GetFullPath(path).Replace('\\', '/');
                    foreach (var kvp in _PackagePath2PackageName)
                    {
                        if (path.StartsWith(kvp.Key, System.StringComparison.InvariantCultureIgnoreCase))
                        {
                            var package = kvp.Value;
                            return "Packages/" + package + path.Substring(kvp.Key.Length);
                        }
                    }
                    if (path.StartsWith(System.Environment.CurrentDirectory, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return path.Substring(System.Environment.CurrentDirectory.Length).TrimStart('/');
                    }
                }
            }
            return null;
        }

        public static string GetModRootInPackage(string mod)
        {
            return GetPackageRoot(GetPackageName(mod));
        }
        public static string GetModRoot(string mod)
        {
            if (string.IsNullOrEmpty(mod))
            {
                return "Assets";
            }
            return "Assets/Mods/" + mod;
        }
        public static string GetPackageOrModRoot(string mod)
        {
            var dir = GetModRootInPackage(mod);
            if (string.IsNullOrEmpty(dir))
            {
                return GetModRoot(mod);
            }
            else
            {
                return dir;
            }
        }
        public static string GetAssetRoot(string mod)
        {
            var package = GetPackageName(mod);
            if (string.IsNullOrEmpty(package))
            {
                return GetModRoot(mod);
            }
            else
            {
                return "Packages/" + package;
            }
        }
        public static string GetAssetModName(string path)
        {
            if (path != null)
            {
                var file = path;
                if (file.StartsWith("Assets/Mods/"))
                {
                    file = file.Substring("Assets/Mods/".Length);
                    var im = file.IndexOf("/");
                    if (im > 0)
                    {
                        file = file.Substring(0, im);
                    }
                    return file;
                }
                else if (file.StartsWith("Packages/"))
                {
                    return GetPackageModName(GetAssetPackage(file));
                }
                else
                {
                    foreach (var usf in UniqueSpecialFolders)
                    {
                        var pre = "Assets/" + usf + "/Mods/";
                        if (file.StartsWith(pre))
                        {
                            file = file.Substring(pre.Length);
                            var im = file.IndexOf("/");
                            if (im > 0)
                            {
                                file = file.Substring(0, im);
                            }
                            return file;
                        }
                    }
                }
            }
            return "";
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
        public static HashSet<string> GetAllModsInPackage()
        {
            HashSet<string> mods = new HashSet<string>(_ModName2PackageName.Keys);
            return mods;
        }
        public static HashSet<string> GetAllTreatAsModPackages()
        {
            return _ShouldTreatAsModPackages;
        }
        public static string[] GetAllModsOrPackages()
        {
            var mods = GetAllModsInternal();
            mods.UnionWith(GetAllModsInPackage());
            return mods.ToArray();
        }
        public static string[] GetAllMods()
        {
            var mods = GetAllModsInternal();
            return mods.ToArray();
        }
        public static string[] GetOptionalMods()
        {
            List<string> mods = new List<string>();
            var allmods = GetAllModsInternal();
            foreach (var mod in allmods)
            {
                if (IsModOptional(mod))
                {
                    mods.Add(mod);
                }
            }
            allmods = GetAllTreatAsModPackages();
            foreach (var mod in allmods)
            {
                if (IsModOptional(mod))
                {
                    mods.Add(mod);
                }
            }
            return mods.ToArray();
        }
        public static bool IsModOptional(string mod)
        {
            if (string.IsNullOrEmpty(mod))
            {
                return false;
            }
            var package = GetPackageName(mod);
            if (!string.IsNullOrEmpty(package))
            {
                var path = "Packages/" + package;
                var descpath = path + "/Runtime/Resources/resdesc.asset";
                bool descPathExists = false;
                if (!(descPathExists = System.IO.File.Exists(descpath)))
                {
                    descpath = path + "/Resources/resdesc.asset";
                }
                if (descPathExists || System.IO.File.Exists(descpath))
                {
                    bool inMain;
                    string[] deps;
                    if (TryParseModDesc(descpath, out inMain, out deps))
                    {
                        return !inMain || (deps != null && deps.Length > 0);
                    }
                }
                return false;
            }
            else
            {
                var descpath = "Assets/Mods/" + mod + "/Resources/resdesc.asset";
                if (!System.IO.File.Exists(descpath))
                {
                    return false;
                }
                if (ResManagerEditorEntryUtils.IsFileHidden("Assets/Mods/" + mod))
                {
                    return true;
                }
                bool inMain;
                string[] deps;
                if (TryParseModDesc(descpath, out inMain, out deps))
                {
                    return !inMain || (deps != null && deps.Length > 0);
                }
                return false;
            }
        }
        public static bool TryParseModDesc(string file, out bool InMain, out string[] deps)
        {
            Debug.LogWarning("Can not load mod desc: " + file + ", try parse it as text.");
            bool success = false;
            InMain = false;
            deps = null;
            try
            {
                if (!System.IO.File.Exists(file))
                {
                    return success;
                }
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(file);
                    if (lines == null || lines.Length <= 0)
                    {
                        return success;
                    }
                    success = true;
                    for (int i = 0; i < lines.Length; ++i)
                    {
                        var line = lines[i].Trim();
                        if (line.StartsWith("InMain:"))
                        {
                            var sub = line.Substring("InMain:".Length).Trim();
                            if (!string.IsNullOrEmpty(sub) && sub != "0")
                            {
                                InMain = true;
                            }
                        }
                        else if (line.StartsWith("Deps:"))
                        {
                            List<string> deplist = new List<string>();
                            HashSet<string> depset = new HashSet<string>();
                            var sub = line.Substring("Deps:".Length).Trim();
                            if (string.IsNullOrEmpty(sub))
                            {
                                var pos = lines[i].IndexOf("Deps:");
                                for (++i; i < lines.Length; ++i)
                                {
                                    var subline = lines[i].TrimStart();
                                    if (!string.IsNullOrEmpty(subline) && lines[i].Length - subline.Length != pos)
                                    {
                                        --i;
                                        break;
                                    }
                                    if (!subline.StartsWith("- "))
                                    {
                                        --i;
                                        break;
                                    }
                                    var ritem = subline.Substring("- ".Length).Trim();
                                    if (!string.IsNullOrEmpty(ritem))
                                    {
                                        if (depset.Add(ritem))
                                        {
                                            deplist.Add(ritem);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var items = sub.Split(new[] { ',', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                if (items != null)
                                {
                                    foreach (var item in items)
                                    {
                                        var ritem = item.Trim();
                                        if (!string.IsNullOrEmpty(ritem))
                                        {
                                            if (depset.Add(ritem))
                                            {
                                                deplist.Add(ritem);
                                            }
                                        }
                                    }
                                }
                            }
                            deps = deplist.ToArray();
                        }
                    }
                    return success;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return success;
                }
            }
            finally
            {
                if (!success)
                {
                    Debug.LogError("Can not load mod desc: " + file + " as text.");
                }
            }
        }

        public static int __LINE__
        {
            get
            {
                return new System.Diagnostics.StackTrace(1, true).GetFrame(0).GetFileLineNumber();
            }
        }
        public static string __FILE__
        {
            get
            {
                return new System.Diagnostics.StackTrace(1, true).GetFrame(0).GetFileName();
            }
        }
        public static string __ASSET__
        {
            get
            {
                var file = new System.Diagnostics.StackTrace(1, true).GetFrame(0).GetFileName();

                return GetAssetNameFromPath(file) ?? file;
            }
        }
        public static string __MOD__
        {
            get
            {
                var file = new System.Diagnostics.StackTrace(1, true).GetFrame(0).GetFileName();

                //return CapsModEditor.GetAssetModName(GetAssetNameFromPath(file));

                var package = GetPackageNameFromPath(file);
                if (string.IsNullOrEmpty(package))
                {
                    var rootdir = System.Environment.CurrentDirectory;
                    if (file.StartsWith(rootdir))
                    {
                        file = file.Substring(rootdir.Length).TrimStart('/', '\\');
                    }
                    file = file.Replace('\\', '/');
                    //var iassets = file.IndexOf("Assets/");
                    //if (iassets > 0)
                    //{
                    //    file = file.Substring(iassets);
                    //}
                    return GetAssetModName(file);
                }
                else
                {
                    return GetPackageModName(package);
                }
            }
        }
    }
}
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    [InitializeOnLoad]
    public static class ResManagerEditorEntry
    {
        public static void ShouldAlreadyInit() { }

        static ResManagerEditorEntry()
        {
            CapsPackageEditor.OnPackagesChanged += () =>
            {
                Dictionary<string, string> linked = new Dictionary<string, string>();
                bool linkupdated = false;
                if (System.IO.File.Exists("EditorOutput/Runtime/linked-package.txt"))
                {
                    try
                    {
                        var lines = System.IO.File.ReadAllLines("EditorOutput/Runtime/linked-package.txt");
                        if (lines != null)
                        {
                            for (int i = 0; i < lines.Length; ++i)
                            {
                                var line = lines[i];
                                if (line != null)
                                {
                                    var parts = line.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts != null && parts.Length >= 2)
                                    {
                                        linked[parts[0]] = parts[1];
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                HashSet<string> existingmods = new HashSet<string>();
                foreach (var package in CapsPackageEditor.Packages.Values)
                {
                    if (package.status == UnityEditor.PackageManager.PackageStatus.Available
                        && (package.source == UnityEditor.PackageManager.PackageSource.Embedded || package.source == UnityEditor.PackageManager.PackageSource.Git || package.source == UnityEditor.PackageManager.PackageSource.Local)
                        )
                    {
                        var path = package.resolvedPath;
                        var mod = System.IO.Path.GetFileName(path);
                        if (mod.Contains("@"))
                        {
                            mod = mod.Substring(0, mod.IndexOf('@'));
                        }
                        if (System.IO.Directory.Exists(path + "/Link~"))
                        {
                            existingmods.Add(mod);
                            bool isuptodate = linked.ContainsKey(package.name) && linked[package.name] == path;
                            if (!isuptodate)
                            {
                                UnlinkMod(mod);
                                if (linked.ContainsKey(package.name))
                                {
                                    var oldmod = System.IO.Path.GetFileName(linked[package.name]);
                                    if (oldmod.Contains("@"))
                                    {
                                        oldmod = oldmod.Substring(0, oldmod.IndexOf('@'));
                                    }
                                    UnlinkMod(oldmod);
                                }
                                linked[package.name] = path;
                                linkupdated = true;
                            }
                            LinkPackageToMod(package);
                        }
                    }
                }
                if (linked.Count != existingmods.Count)
                {
                    List<string> keystodel = new List<string>();
                    foreach (var kvp in linked)
                    {
                        if (!existingmods.Contains(kvp.Key))
                        {
                            keystodel.Add(kvp.Key);
                            var mod = System.IO.Path.GetFileName(kvp.Value);
                            if (mod.Contains("@"))
                            {
                                mod = mod.Substring(0, mod.IndexOf('@'));
                            }
                            UnlinkMod(mod);
                        }
                    }
                    linkupdated = true;
                    for (int i = 0; i < keystodel.Count; ++i)
                    {
                        linked.Remove(keystodel[i]);
                    }
                }

                if (linkupdated)
                {
                    System.IO.Directory.CreateDirectory("EditorOutput/Runtime");
                    using (var sw = new System.IO.StreamWriter("EditorOutput/Runtime/linked-package.txt"))
                    {
                        foreach (var kvp in linked)
                        {
                            sw.Write(kvp.Key);
                            sw.Write('|');
                            sw.Write(kvp.Value);
                            sw.WriteLine();
                        }
                    }
                    AssetDatabase.Refresh();
                }
            };
            CapsPackageEditor.RefreshPackages();
        }

        private static readonly string[] UniqueSpecialFolders = new[] { "Plugins", "Standard Assets" };

        private static void UnlinkMod(string mod)
        {
            var moddir = "Assets/Mods/" + mod;
            if (UnlinkDir(moddir))
            {
                ResManagerEditorEntryUtils.RemoveGitIgnore("Assets/Mods/.gitignore", mod);
            }
            else
            {
                if (System.IO.Directory.Exists(moddir))
                {
                    var subs = System.IO.Directory.GetDirectories(moddir);
                    foreach (var sub in subs)
                    {
                        if (UnlinkDir(sub))
                        {
                            var part = System.IO.Path.GetFileName(sub);
                            ResManagerEditorEntryUtils.RemoveGitIgnore("Assets/Mods/.gitignore", mod + "/" + part);
                            ResManagerEditorEntryUtils.RemoveGitIgnore("Assets/Mods/.gitignore", mod + "/" + part + ".meta");
                        }
                    }
                }
            }
            //UnlinkOrDeleteDir("Assets/Mods/" + mod);
            //ResManagerEditorEntryUtils.RemoveGitIgnore("Assets/Mods/.gitignore", mod);
            for (int i = 0; i < UniqueSpecialFolders.Length; ++i)
            {
                var usdir = UniqueSpecialFolders[i];
                var udir = "Assets/" + usdir + "/Mods/" + mod;
                UnlinkOrDeleteDir(udir + "/Content");
                if (System.IO.Directory.Exists(udir))
                {
                    System.IO.Directory.Delete(udir, true);
                }
            }
        }
        private static bool UnlinkDir(string path)
        {
            if (ResManagerEditorEntryUtils.IsDirLink(path))
            {
                ResManagerEditorEntryUtils.DeleteDirLink(path);
                return true;
            }
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                return true;
            }
            return false;
        }
        private static void UnlinkOrDeleteDir(string path)
        {
            if (ResManagerEditorEntryUtils.IsDirLink(path))
            {
                ResManagerEditorEntryUtils.DeleteDirLink(path);
            }
            else
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
                else if (System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.Delete(path, true);
                }
            }
        }

        private static void LinkPackageToMod(UnityEditor.PackageManager.PackageInfo package)
        {
            var path = package.resolvedPath;
            var mod = System.IO.Path.GetFileName(path);
            if (mod.Contains("@"))
            {
                mod = mod.Substring(0, mod.IndexOf('@'));
            }
            var moddir = "Assets/Mods/" + mod;
            if (System.IO.Directory.Exists(path + "/Link~/Mod"))
            {
                var link = moddir;
                if (!System.IO.Directory.Exists(link) && !System.IO.File.Exists(link))
                {
                    System.IO.Directory.CreateDirectory("Assets/Mods/");
                    ResManagerEditorEntryUtils.MakeDirLink(link, path + "/Link~/Mod");
                    ResManagerEditorEntryUtils.AddGitIgnore("Assets/Mods/.gitignore", mod);
                }
            }
            for (int i = 0; i < UniqueSpecialFolders.Length; ++i)
            {
                var usdir = UniqueSpecialFolders[i];
                var link = "Assets/" + usdir + "/Mods/" + mod + "/Content";
                var target = path + "/Link~/" + usdir;
                if (System.IO.Directory.Exists(target) && !System.IO.Directory.Exists(link) && !System.IO.File.Exists(link))
                {
                    System.IO.Directory.CreateDirectory("Assets/" + usdir + "/Mods/" + mod);
                    ResManagerEditorEntryUtils.MakeDirLink(link, target);
                }
            }
            Dictionary<string, string> dirMap = null;
            if (System.IO.File.Exists(path + "/Link~/link.config"))
            {
                dirMap = new Dictionary<string, string>();
                var lines = System.IO.File.ReadLines(path + "/Link~/link.config");
                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        var parts = line.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts != null && parts.Length > 0)
                        {
                            var dirname = parts[0];
                            if (!string.IsNullOrEmpty(dirname))
                            {
                                var linkname = dirname;
                                if (parts.Length > 1)
                                {
                                    linkname = parts[1];
                                    if (string.IsNullOrEmpty(linkname))
                                    {
                                        linkname = dirname;
                                    }
                                }
                                dirMap[dirname.ToLower()] = linkname;
                            }
                        }
                    }
                }
            }
            {
                var subs = System.IO.Directory.GetDirectories(path + "/Link~/");
                foreach (var sub in subs)
                {
                    var part = System.IO.Path.GetFileName(sub);
                    if (part.Equals("Mod", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                    if (dirMap != null && !dirMap.ContainsKey(part.ToLower()))
                    {
                        continue;
                    }
                    bool isudir = false;
                    for (int i = 0; i < UniqueSpecialFolders.Length; ++i)
                    {
                        var usdir = UniqueSpecialFolders[i];
                        if (part.Equals(usdir, StringComparison.InvariantCultureIgnoreCase))
                        {
                            isudir = true;
                            break;
                        }
                    }
                    if (isudir)
                    {
                        continue;
                    }
                    var link = moddir + "/" + part;
                    if (dirMap != null)
                    {
                        link = moddir + "/" + dirMap[part.ToLower()];
                    }
                    var target = sub;
                    if (System.IO.Directory.Exists(target) && !System.IO.Directory.Exists(link) && !System.IO.File.Exists(link))
                    {
                        if (!System.IO.Directory.Exists(moddir))
                        {
                            System.IO.Directory.CreateDirectory(moddir);
                        }
                        if (part.Contains("\\") || part.Contains("/"))
                        {
                            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(link));
                        }
                        ResManagerEditorEntryUtils.MakeDirLink(link, target);
                        ResManagerEditorEntryUtils.AddGitIgnore("Assets/Mods/.gitignore", mod + "/" + part);
                        ResManagerEditorEntryUtils.AddGitIgnore("Assets/Mods/.gitignore", mod + "/" + part + ".meta");
                    }
                }
            }
        }
    }
}
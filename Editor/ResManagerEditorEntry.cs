using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Capstones.UnityEditorEx
{
    [InitializeOnLoad]
    public static class ResManagerEditorEntry
    {
        static ResManagerEditorEntry()
        {
            LinkPackageToMod();
        }

        [MenuItem("Mods/Link Package to Mod", priority = 200000)]
        public static void LinkPackageToMod()
        {
            var req = UnityEditor.PackageManager.Client.List(true);
            EditorApplication.QueuePlayerLoopUpdate();
            EditorApplication.update = () =>
            {
                if (req.IsCompleted)
                {
                    EditorApplication.update = null;

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

                    foreach (var package in req.Result)
                    {
                        if (package.status == UnityEditor.PackageManager.PackageStatus.Available
                            && (package.source == UnityEditor.PackageManager.PackageSource.Embedded || package.source == UnityEditor.PackageManager.PackageSource.Git || package.source == UnityEditor.PackageManager.PackageSource.Local)
                            )
                        {
                            var path = package.resolvedPath;
                            var mod = System.IO.Path.GetFileNameWithoutExtension(path);
                            if (System.IO.Directory.Exists(path + "/Link~"))
                            {
                                bool isuptodate = linked.ContainsKey(package.name) && linked[package.name] == path;
                                if (!isuptodate)
                                {
                                    linkupdated = true;
                                    UnlinkOrDeleteDir("Assets/Mod/" + mod + "/Content");
                                    for (int i = 0; i < UniqueSpecialFolders.Length; ++i)
                                    {
                                        var usdir = UniqueSpecialFolders[i];
                                        UnlinkOrDeleteDir("Assets/" + usdir + "/Mods/" + mod + "/Content");
                                    }
                                    if (linked.ContainsKey(package.name))
                                    {
                                        var oldmod = System.IO.Path.GetFileNameWithoutExtension(linked[package.name]);
                                        UnlinkOrDeleteDir("Assets/Mod/" + oldmod + "/Content");
                                        for (int i = 0; i < UniqueSpecialFolders.Length; ++i)
                                        {
                                            var usdir = UniqueSpecialFolders[i];
                                            UnlinkOrDeleteDir("Assets/" + usdir + "/Mods/" + oldmod + "/Content");
                                        }
                                    }
                                }
                                LinkPackageToMod(package);
                            }
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
                }
                else
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                }
            };
        }

        private static readonly string[] UniqueSpecialFolders = new[] { "Plugins", "Standard Assets" };

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
            var mod = System.IO.Path.GetFileNameWithoutExtension(path);
            if (System.IO.Directory.Exists(path + "/Link~/Mod"))
            {
                var link = "Assets/Mod/" + mod + "/Content";
                if (!System.IO.Directory.Exists(link) && !System.IO.File.Exists(link))
                {
                    System.IO.Directory.CreateDirectory("Assets/Mod/" + mod);
                    ResManagerEditorEntryUtils.MakeDirLink(link, path + "/Link~/Mod");
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
        }
    }
}
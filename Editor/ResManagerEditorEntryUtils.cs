using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Capstones.UnityEditorEx
{
    public static class ResManagerEditorEntryUtils
    {
        public static void HideFile(string path)
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                var si = new System.Diagnostics.ProcessStartInfo("chflags", "hidden \"" + path + "\"");
                var p = System.Diagnostics.Process.Start(si);
                p.WaitForExit();
            }
            else
            {
                if (System.IO.Directory.Exists(path))
                {
                    var di = new System.IO.DirectoryInfo(path);
                    di.Attributes |= System.IO.FileAttributes.Hidden;
                }
                else
                {
                    var fi = new System.IO.FileInfo(path);
                    if (fi.Exists)
                    {
                        fi.Attributes |= System.IO.FileAttributes.Hidden;
                    }
                }
            }
        }
        public static void UnhideFile(string path)
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                var si = new System.Diagnostics.ProcessStartInfo("chflags", "nohidden \"" + path + "\"");
                var p = System.Diagnostics.Process.Start(si);
                p.WaitForExit();
            }
            else
            {
                if (System.IO.Directory.Exists(path))
                {
                    var di = new System.IO.DirectoryInfo(path);
                    di.Attributes &= ~System.IO.FileAttributes.Hidden;
                }
                else
                {
                    var fi = new System.IO.FileInfo(path);
                    if (fi.Exists)
                    {
                        fi.Attributes &= ~System.IO.FileAttributes.Hidden;
                    }
                }
            }
        }
        public static bool IsFileHidden(string path)
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                var si = new System.Diagnostics.ProcessStartInfo("ls", "-lOd \"" + path + "\"");
                si.UseShellExecute = false;
                si.RedirectStandardOutput = true;
                var p = System.Diagnostics.Process.Start(si);
                p.WaitForExit();
                var output = p.StandardOutput.ReadToEnd();
                if (string.IsNullOrEmpty(output))
                {
                    return false;
                }
                output = output.Trim();
                if (output.EndsWith(path, System.StringComparison.InvariantCultureIgnoreCase))
                {
                    output = output.Substring(0, output.Length - path.Length).Trim();
                }
                var idsplit = output.IndexOfAny(new[] { '/', '\\' });
                if (idsplit >= 0)
                {
                    output = output.Substring(0, idsplit);
                }
                return output.Contains("hidden");
            }
            else
            {
                if (System.IO.Directory.Exists(path))
                {
                    var di = new System.IO.DirectoryInfo(path);
                    return (di.Attributes & System.IO.FileAttributes.Hidden) != 0;
                }
                else
                {
                    var fi = new System.IO.FileInfo(path);
                    if (fi.Exists)
                    {
                        return (fi.Attributes & System.IO.FileAttributes.Hidden) != 0;
                    }
                }
                return false;
            }
        }

        public static void DeleteDirLink(string path)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var si = new System.Diagnostics.ProcessStartInfo("cmd", "/C \"rmdir \"" + path.Replace('/', '\\') + "\"\"");
                si.CreateNoWindow = true;
                si.UseShellExecute = false;
                var p = System.Diagnostics.Process.Start(si);
                p.WaitForExit();
            }
            else
            {
                var si = new System.Diagnostics.ProcessStartInfo("rm", "\"" + path + "\"");
                var p = System.Diagnostics.Process.Start(si);
                p.WaitForExit();
            }
        }
        public static void MakeDirLink(string link, string target)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var si = new System.Diagnostics.ProcessStartInfo("cmd", "/C \"mklink /D \"" + link.Replace('/', '\\') + "\"" + " \"" + target.Replace('/', '\\') + "\"\"");
                si.CreateNoWindow = true;
                si.UseShellExecute = false;
                var p = System.Diagnostics.Process.Start(si);
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    si = new System.Diagnostics.ProcessStartInfo("cmd", "/C \"mklink /J \"" + link.Replace('/', '\\') + "\"" + " \"" + (target.StartsWith(".") ? System.IO.Path.GetDirectoryName(link.Replace('/', '\\').TrimEnd('\\')) + "\\" : "") + target.Replace('/', '\\') + "\"\"");
                    si.CreateNoWindow = true;
                    si.UseShellExecute = false;
                    p = System.Diagnostics.Process.Start(si);
                    p.WaitForExit();
                }
            }
            else
            {
                var si = new System.Diagnostics.ProcessStartInfo("ln", "-s \"" + target + "\"" + " \"" + link + "\"");
                var p = System.Diagnostics.Process.Start(si);
                p.WaitForExit();
            }
        }
        public static bool IsDirLink(string path)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var di = new System.IO.DirectoryInfo(path);
                return di.Exists && (di.Attributes & System.IO.FileAttributes.ReparsePoint) == System.IO.FileAttributes.ReparsePoint;
            }
            else
            {
                if (System.IO.Directory.Exists(path) || System.IO.File.Exists(path))
                {
                    var di = new System.IO.DirectoryInfo(path);
                    return (di.Attributes & System.IO.FileAttributes.ReparsePoint) == System.IO.FileAttributes.ReparsePoint;
                }
                return false;
            }
        }

        public static bool ExecuteProcess(System.Diagnostics.ProcessStartInfo si)
        {
            bool safeWaitMode = true;
#if UNITY_EDITOR_WIN
            safeWaitMode = false;
#endif
            // TODO: on Apple M1, we must use safeWaitMode. we should test non-safeMode on Mac-on-Intel and Linux and add "#if" here. NOTICE: use SystemInfo.processorType to get cpu model name.

            si.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            si.UseShellExecute = false;
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;
            si.CreateNoWindow = true;

            var process = new System.Diagnostics.Process();
            process.StartInfo = si;

            process.OutputDataReceived += (s, e) => WriteProcessOutput(s as System.Diagnostics.Process, e.Data, false);

            process.ErrorDataReceived += (s, e) => WriteProcessOutput(s as System.Diagnostics.Process, e.Data, true);

            System.Threading.ManualResetEventSlim waitHandleForProcess = null;
            if (safeWaitMode)
            {
                waitHandleForProcess = new System.Threading.ManualResetEventSlim();
                process.Exited += (s, e) => waitHandleForProcess.Set();
            }

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (waitHandleForProcess)
            {
                while (!process.HasExited)
                {
                    if (safeWaitMode)
                    {
                        waitHandleForProcess.Wait(1000);
                    }
                    else
                    {
                        process.WaitForExit(1000);
                    }
                }
            }

            if (process.ExitCode != 0)
            {
                Debug.LogErrorFormat("Error when execute process {0} {1}", si.FileName, si.Arguments);
                return false;
            }
            else
            {
                return true;
            }
        }
        private static void WriteProcessOutput(System.Diagnostics.Process p, string data, bool isError)
        {
            if (!string.IsNullOrEmpty(data))
            {
                string processName = System.IO.Path.GetFileName(p.StartInfo.FileName);
                if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    if (processName == "wine" || processName == "mono")
                    {
                        var parts = p.StartInfo.Arguments.Split(' ');
                        if (parts != null && parts.Length > 0)
                        {
                            processName = System.IO.Path.GetFileName(parts[0]);
                        }
                    }
                }
                if (!isError)
                {
                    Debug.LogFormat("[{0}] {1}", processName, data);
                }
                else
                {
                    Debug.LogErrorFormat("[{0} Error] {1}", processName, data);
                }
            }
        }

        public static void AddGitIgnore(string gitignorepath, params string[] items)
        {
            List<string> lines = new List<string>();
            HashSet<string> lineset = new HashSet<string>();
            if (System.IO.File.Exists(gitignorepath))
            {
                try
                {
                    using (var sr = new System.IO.StreamReader(gitignorepath))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            lines.Add(line);
                            lineset.Add(line);
                        }
                    }
                }
                catch { }
            }

            if (items != null)
            {
                for (int i = 0; i < items.Length; ++i)
                {
                    var item = items[i];
                    if (lineset.Add(item))
                    {
                        lines.Add(item);
                    }
                }
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(gitignorepath));
            using (var sw = new System.IO.StreamWriter(gitignorepath))
            {
                for (int i = 0; i < lines.Count; ++i)
                {
                    sw.WriteLine(lines[i]);
                }
            }
        }

        public static void RemoveGitIgnore(string gitignorepath, params string[] items)
        {
            List<string> lines = new List<string>();
            HashSet<string> removes = new HashSet<string>();
            if (items != null)
            {
                removes.UnionWith(items);
            }
            if (System.IO.File.Exists(gitignorepath))
            {
                try
                {
                    using (var sr = new System.IO.StreamReader(gitignorepath))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (!removes.Contains(line))
                            {
                                lines.Add(line);
                            }
                        }
                    }
                }
                catch { }
            }
            if (lines.Count == 0)
            {
                if (System.IO.File.Exists(gitignorepath))
                {
                    System.IO.File.Delete(gitignorepath);
                }
            }
            else
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(gitignorepath));
                using (var sw = new System.IO.StreamWriter(gitignorepath))
                {
                    for (int i = 0; i < lines.Count; ++i)
                    {
                        sw.WriteLine(lines[i]);
                    }
                }
            }
        }

        public static string GetFileMD5(string path)
        {
            try
            {
                byte[] hash = null;
                using (var stream = System.IO.File.OpenRead(path))
                {
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        hash = md5.ComputeHash(stream);
                    }
                }
                if (hash == null || hash.Length <= 0) return "";
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < hash.Length; ++i)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString();
            }
            catch { }
            return "";
        }
        public static long GetFileLength(string path)
        {
            try
            {
                var f = new System.IO.FileInfo(path);
                return f.Length;
            }
            catch { }
            return 0;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace JarStart
{
    /// <summary>解析要使用的 java 可执行文件</summary>
    public static class JdkResolver
    {
        /// <summary>
        /// 顺序：单项指定的 JDK → 全局默认 JDK → 环境变量 JAVA_HOME → PATH 上的 java。
        /// 返回的是可直接用于启动的 java 路径（找不到时返回 "java"，交给系统去 PATH 里找）。
        /// </summary>
        public static string ResolveJava(string itemJava, string globalJava)
        {
            var p = ToJavaExe(itemJava);
            if (!string.IsNullOrEmpty(p)) return p;

            p = ToJavaExe(globalJava);
            if (!string.IsNullOrEmpty(p)) return p;

            p = ToJavaExe(Environment.GetEnvironmentVariable("JAVA_HOME"));
            if (!string.IsNullOrEmpty(p)) return p;

            return "java";
        }

        /// <summary>把用户填的路径（目录或 java.exe）转成 java.exe 全路径</summary>
        public static string ToJavaExe(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = path.Trim().Trim('"');
            try
            {
                if (File.Exists(path))
                {
                    if (string.Equals(Path.GetFileName(path), "java.exe", StringComparison.OrdinalIgnoreCase))
                        return path;
                    // 指向的是某个可执行文件（可能不是 java），直接用
                    return path;
                }
                if (Directory.Exists(path))
                {
                    var exe = Path.Combine(path, "bin", "java.exe");
                    if (File.Exists(exe)) return exe;
                    var exe2 = Path.Combine(path, "java.exe");
                    if (File.Exists(exe2)) return exe2;
                }
            }
            catch { }
            return null;
        }

        /// <summary>执行 java -version，返回版本文本（失败返回错误说明）</summary>
        public static string ProbeVersion(string javaPath)
        {
            try
            {
                var exe = ToJavaExe(javaPath);
                if (string.IsNullOrEmpty(exe))
                {
                    var raw = (javaPath ?? "").Trim().Trim('"');
                    if (string.Equals(raw, "java", StringComparison.OrdinalIgnoreCase)) exe = "java";
                    else return "路径无效：找不到 java.exe";
                }
                if (File.Exists(exe) && !string.Equals(Path.GetFileName(exe), "java.exe", StringComparison.OrdinalIgnoreCase))
                    return "该文件不是 java.exe：" + exe;

                var psi = new ProcessStartInfo(exe, "-version");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.StandardOutputEncoding = Encoding.UTF8;
                using (var p = Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    var text = string.IsNullOrWhiteSpace(err) ? outp : err;
                    text = (text ?? "").Trim();
                    if (text.Length > 400) text = text.Substring(0, 400);
                    return text;
                }
            }
            catch (Exception ex)
            {
                return "检测失败：" + ex.Message;
            }
        }

        /// <summary>
        /// 把 java -version 的原始输出压缩成一行好读的摘要，例如
        /// "openjdk 17.0.8 · 64 位 Server VM"
        /// </summary>
        public static string SummarizeVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var lines = raw.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return "";

            string first = lines[0].Trim();
            var sb = new StringBuilder();

            // 第一行形如： openjdk version "17.0.8" 2024-07-16
            var m = Regex.Match(first, @"^(.*?)\s+version\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string vendor = m.Groups[1].Value.Trim();
                sb.Append(vendor.Length > 0 ? vendor : "java");
                sb.Append(' ');
                sb.Append(m.Groups[2].Value);
            }
            else
            {
                sb.Append(first);
            }

            if (raw.IndexOf("64-Bit", StringComparison.OrdinalIgnoreCase) >= 0)
                sb.Append(" · 64 位");
            if (raw.IndexOf("Server VM", StringComparison.OrdinalIgnoreCase) >= 0)
                sb.Append(" Server VM");

            return sb.ToString();
        }

        /// <summary>扫描本机常见的 JDK 安装位置，供设置界面下拉选择</summary>
        public static List<string> ScanJdkCandidates()
        {
            var list = new List<string>();
            try
            {
                var home = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrWhiteSpace(home)) list.Add(home);

                string[] roots =
                {
                    @"C:\Program Files\Java",
                    @"C:\Program Files\Eclipse Adoptium",
                    @"C:\Program Files\Microsoft",
                    @"C:\Program Files\Zulu",
                    @"C:\Program Files\Amazon Corretto",
                    @"C:\Program Files (x86)\Java",
                    @"D:\green",
                    @"D:\java",
                    @"D:\jdk",
                    @"C:\java",
                    @"C:\jdk",
                };
                foreach (var root in roots)
                {
                    try
                    {
                        if (!Directory.Exists(root)) continue;
                        var dirs = Directory.GetDirectories(root);
                        Array.Sort(dirs);
                        foreach (var d in dirs)
                        {
                            if (File.Exists(Path.Combine(d, "bin", "java.exe"))) list.Add(d);
                            // 有些目录下面还有一层版本目录
                            try
                            {
                                var sub = Directory.GetDirectories(d);
                                foreach (var s in sub)
                                    if (File.Exists(Path.Combine(s, "bin", "java.exe"))) list.Add(s);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            var uniq = new List<string>();
            foreach (var p in list)
            {
                if (!uniq.Contains(p)) uniq.Add(p);
            }
            return uniq;
        }
    }
}

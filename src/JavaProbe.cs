using System;
using System.Drawing;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>
    /// 「检测」按钮的共用逻辑：执行 java -version，把版本号显示在标签上，
    /// 完整输出放进 tooltip。jar 配置对话框和全局设置用的是同一套。
    /// </summary>
    public static class JavaProbe
    {
        /// <summary>刷新版本显示；javaPath 为空时改用环境变量里的 java</summary>
        public static void Apply(Label info, ToolTip tip, string javaPath)
        {
            if (info == null) return;

            string path = (javaPath ?? "").Trim();
            bool custom = path.Length > 0;
            if (!custom)
            {
                // 没指定就看看环境变量里的那个是哪个版本
                path = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (string.IsNullOrWhiteSpace(path)) path = "java";
            }

            string text = JdkResolver.ProbeVersion(path);
            bool bad = text.StartsWith("检测失败") || text.StartsWith("路径无效");

            if (bad)
            {
                info.Text = "✖ " + text;
                info.ForeColor = Theme.Current.Danger;
                if (tip != null) tip.SetToolTip(info, text);
                return;
            }

            string summary = JdkResolver.SummarizeVersion(text);
            info.Text = "✔ " + summary + (custom ? "" : "（系统环境变量）");
            info.ForeColor = Theme.Current.Success;
            if (tip != null) tip.SetToolTip(info, text);
        }

        /// <summary>没有指定 JDK 时显示的提示语</summary>
        public static void ShowHint(Label info, ToolTip tip)
        {
            if (info == null) return;
            info.Text = "留空则使用系统环境变量里的 java（JAVA_HOME → PATH）";
            info.ForeColor = Theme.Current.SubText;
            if (tip != null) tip.SetToolTip(info, "");
        }
    }
}

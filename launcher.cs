// GuangXIW 桌面启动器
// 作用：把同目录的 index.html 用浏览器的 app 模式打开成独立窗口（无地址栏、无标签页），
// 并用独立 profile 目录保存数据，保证 localStorage 稳定可复用。
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

static class Launcher
{
    [STAThread]
    static void Main()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string html = Path.Combine(baseDir, "index.html");
        if (!File.Exists(html))
        {
            MessageBox.Show("找不到 index.html，请把 GuangXIW.exe 和 index.html 放在同一目录。",
                "GuangXIW", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GuangXIW", "profile");
        Directory.CreateDirectory(profile);

        string url = new Uri(html).AbsoluteUri;   // 自动处理中文路径与空格

        string[] candidates = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Google\Chrome\Application\chrome.exe")
        };

        foreach (string exe in candidates)
        {
            if (!File.Exists(exe)) continue;
            try
            {
                var psi = new ProcessStartInfo(exe);
                psi.Arguments = "--app=\"" + url + "\" --user-data-dir=\"" + profile +
                                "\" --window-size=1360,900 --disable-features=Translate";
                psi.UseShellExecute = false;
                Process.Start(psi);
                return;
            }
            catch { }
        }

        // 兜底：交给系统默认浏览器
        try { Process.Start(new ProcessStartInfo(html) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show("启动失败：" + ex.Message, "GuangXIW");
        }
    }
}
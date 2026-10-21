// GuangXIW portable launcher (single file)
// index.html 作为资源内嵌进 exe；运行时释放到 %LOCALAPPDATA%\GuangXIW\app\index.html，
// 因为路径固定，file:// 源不变，localStorage 里的关系网数据可以一直沿用。
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

static class Launcher
{
    [STAThread]
    static void Main()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GuangXIW");
        string appDir = Path.Combine(root, "app");
        string profile = Path.Combine(root, "profile");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(profile);

        string html = Path.Combine(appDir, "index.html");
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream("index.html"))
            {
                if (s == null) throw new Exception("内嵌资源缺失");
                using (var fs = new FileStream(html, FileMode.Create, FileAccess.Write))
                    s.CopyTo(fs);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("释放应用失败：" + ex.Message, "GuangXIW");
            return;
        }

        string url = new Uri(html).AbsoluteUri;
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
                                "\" --window-size=1360,900 --disable-features=Translate,msEdgeIdentityIntegration";
                psi.UseShellExecute = false;
                Process.Start(psi);
                return;
            }
            catch { }
        }
        try { Process.Start(new ProcessStartInfo(html) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("启动失败：" + ex.Message, "GuangXIW"); }
    }
}
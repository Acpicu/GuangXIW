using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Launcher
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    static string _root;

    internal static string Root
    {
        get { if (_root == null) _root = ResolveRoot(); return _root; }
    }

    static string LegacyRoot
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuangXIW");
        }
    }

    // 数据放哪儿，按这个顺序决定，用户永远能自己定，程序不擅自往 C 盘塞：
    //   1. 环境变量 GUANGXIW_DATA
    //   2. exe 同目录有 guangxiw.portable  → exe 旁边 GuangXIW-data（便携模式，什么都不装系统）
    //   3. exe 同目录 guangxiw.cfg 里写 dataDir=D:\你的目录
    //   4. exe 同目录可写 → exe 旁边 GuangXIW-data
    //   5. 以上都不行 → 才退回 %LOCALAPPDATA%\GuangXIW
    static string ResolveRoot()
    {
        try
        {
            string env = Environment.GetEnvironmentVariable("GUANGXIW_DATA");
            if (!string.IsNullOrWhiteSpace(env))
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(env.Trim()));
        }
        catch { }

        string exeDir = null;
        try { exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); } catch { }
        if (string.IsNullOrEmpty(exeDir)) exeDir = Directory.GetCurrentDirectory();

        try
        {
            if (File.Exists(Path.Combine(exeDir, "guangxiw.portable")))
                return Path.Combine(exeDir, "GuangXIW-data");
        }
        catch { }

        try
        {
            string cfg = Path.Combine(exeDir, "guangxiw.cfg");
            if (File.Exists(cfg))
            {
                foreach (string raw in File.ReadAllLines(cfg))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    if (line.Substring(0, eq).Trim().ToLowerInvariant() == "datadir")
                    {
                        string v = line.Substring(eq + 1).Trim().Trim('"');
                        if (v.Length > 0)
                            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(v));
                    }
                }
            }
        }
        catch { }

        try
        {
            string probe = Path.Combine(exeDir, ".gx_write_test");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return Path.Combine(exeDir, "GuangXIW-data");
        }
        catch { }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuangXIW");
    }

    // 老版本把数据放在 %LOCALAPPDATA%\GuangXIW\profile，换目录时把老数据整体搬过来，只搬一次
    internal static void MigrateLegacyData(string targetProfile)
    {
        try
        {
            string legacy = Path.Combine(LegacyRoot, "profile");
            if (!Directory.Exists(legacy)) return;
            if (string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(targetProfile), StringComparison.OrdinalIgnoreCase)) return;
            string marker = Path.Combine(targetProfile, ".migrated");
            if (File.Exists(marker)) return;
            if (Directory.Exists(targetProfile) && Directory.GetFileSystemEntries(targetProfile).Length > 0) return;

            Directory.CreateDirectory(targetProfile);
            CopyDir(legacy, targetProfile);
            try { File.WriteAllText(marker, "migrated from " + legacy); } catch { }
        }
        catch { }
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
        {
            try { File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true); } catch { }
        }
        foreach (string d in Directory.GetDirectories(from))
        {
            try { CopyDir(d, Path.Combine(to, Path.GetFileName(d))); } catch { }
        }
    }

    private static System.Threading.Mutex singleInstance;

    [STAThread]
    static void Main()
    {
        // 单实例：已经开着一个就直接退出（不弹窗、不提示）
        bool created;
        singleInstance = new System.Threading.Mutex(true, "Global\\GuangXIW.SingleInstance", out created);
        if (!created) return;

        string appDir = Path.Combine(Root, "app");
        Directory.CreateDirectory(appDir);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;

        Extract("Microsoft.Web.WebView2.Core.dll", appDir);
        Extract("Microsoft.Web.WebView2.WinForms.dll", appDir);
        Extract("WebView2Loader.dll", appDir);
        Extract("app.ico", appDir);
        string html = Extract("index.html", appDir);

        AppHost.Run(html, Path.Combine(appDir, "app.ico"));
        GC.KeepAlive(singleInstance);
    }

    static Assembly ResolveEmbedded(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        string path = Path.Combine(Path.Combine(Root, "app"), name);
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }

    static string Extract(string name, string dir)
    {
        string target = Path.Combine(dir, name);
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(name))
            {
                if (s == null) return target;
                using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write))
                    s.CopyTo(fs);
            }
        }
        catch { }
        return target;
    }

    public static void DarkTitleBar(Form form)
    {
        try
        {
            int on = 1;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE（Win11/新版 Win10），19 = 旧版

            if (DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
        }
        catch { }
    }
}

static class AdbPush
{
    static string AdbPath()
    {
        string adb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Android\Sdk\platform-tools\adb.exe");
        return File.Exists(adb) ? adb : "adb";
    }

    static string Run(string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(AdbPath(), args)
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        using (var pr = System.Diagnostics.Process.Start(psi))
        {
            string o = pr.StandardOutput.ReadToEnd() + pr.StandardError.ReadToEnd();
            pr.WaitForExit(20000);
            return pr.ExitCode == 0 ? "OK" : o;
        }
    }

    /// 把导出目录里最新的 guangxiw-relations-*.json 推到手机 /sdcard/Download。
    public static string PushLatest()
    {
        try
        {
            string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(dl)) return "找不到下载目录";
            var files = Directory.GetFiles(dl, "guangxiw-relations-*.json");
            if (files.Length == 0) return "没找到关系摘要，先点导出";
            Array.Sort(files);
            string newest = files[files.Length - 1];
            string r = Run("push \"" + newest + "\" /sdcard/Download/");
            return r == "OK" ? "已推送到手机：" + Path.GetFileName(newest) : "推送失败：" + r;
        }
        catch (Exception ex) { return "推送失败：" + ex.Message; }
    }

    /// 从手机拉取速记 JSON，内容直接回给页面。
    public static string PullMemos()
    {
        try
        {
            string tmpDir = Path.Combine(Launcher.Root, "tmp");
            Directory.CreateDirectory(tmpDir);
            string tmp = Path.Combine(tmpDir, "sj-memos.json");
            if (File.Exists(tmp)) File.Delete(tmp);
            string r = Run("pull /sdcard/Android/data/com.sj.followclip/files/sj-memos.json \"" + tmp + "\"");
            if (r != "OK" || !File.Exists(tmp)) return "拉取失败：" + r;
            return "MEMOS:" + File.ReadAllText(tmp);
        }
        catch (Exception ex) { return "拉取失败：" + ex.Message; }
    }
}

static class AppHost
{
    [STAThread]
    public static void Run(string html, string ico)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var form = new Form
        {
            Text = "GuangXIW",
            Width = 1360,
            Height = 900,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = System.Drawing.Color.FromArgb(15, 17, 21),
            MinimumSize = new System.Drawing.Size(820, 560)
        };

        // 窗口图标：优先用内嵌释放出来的 ico，双击窗口标题栏也能看到同一个图标
        try { if (File.Exists(ico)) form.Icon = new System.Drawing.Icon(ico); } catch { }

        var web = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
        web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 17, 21);
        form.Controls.Add(web);

        form.Load += async delegate
        {
            Launcher.DarkTitleBar(form);
            try
            {
                // 数据目录由 Launcher.Root 决定（exe 旁边 / guangxiw.cfg / GUANGXIW_DATA），
                // 老版本放在 %LOCALAPPDATA%\GuangXIW\profile 的会自动搬过来，不丢数据
                string profileDir = Path.Combine(Launcher.Root, "profile");
                Directory.CreateDirectory(profileDir);
                Launcher.MigrateLegacyData(profileDir);
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, profileDir);
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                web.CoreWebView2.WebMessageReceived += delegate (object s2, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e2)
                {
                    string msg = e2.TryGetWebMessageAsString();
                    if (msg == "push") web.CoreWebView2.PostWebMessageAsString(AdbPush.PushLatest());
                    if (msg == "pullMemos") web.CoreWebView2.PostWebMessageAsString(AdbPush.PullMemos());
                    if (msg == "NEED_BACKUP") web.CoreWebView2.PostWebMessageAsString("RESTORE:" + Backup.Latest());
                    if (msg != null && msg.StartsWith("BACKUP:")) Backup.Save(msg.Substring(7));
                };
                web.Source = new Uri(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 初始化失败：" + ex.Message, "GuangXIW");
                form.Close();
            }
        };
        form.Activated += delegate { Launcher.DarkTitleBar(form); };
        Application.Run(form);
    }

    /// <summary>数据自动备份：每次保存写一份到 我的文档\GuangXIW，保留最近 60 份。</summary>
    static class Backup
    {
        static string Dir
        {
            get
            {
                string d = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GuangXIW");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static void Save(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length < 8) return;
                string f = Path.Combine(Dir, "auto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                string tmp = f + ".tmp";
                File.WriteAllText(tmp, json, new System.Text.UTF8Encoding(false));
                if (File.Exists(f)) File.Delete(f);
                File.Move(tmp, f);
                var files = new DirectoryInfo(Dir).GetFiles("auto-*.json");
                if (files.Length > 60)
                {
                    Array.Sort(files, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                    for (int i = 0; i < files.Length - 60; i++) { try { files[i].Delete(); } catch { } }
                }
            }
            catch { }
        }

        public static string Latest()
        {
            try
            {
                var files = new DirectoryInfo(Dir).GetFiles("auto-*.json");
                if (files.Length == 0) return "";
                Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                foreach (var f in files)
                {
                    try
                    {
                        string s = File.ReadAllText(f.FullName, System.Text.Encoding.UTF8);
                        if (s.Contains("\"folders\"")) return s;
                    }
                    catch { }
                }
            }
            catch { }
            return "";
        }
    }
}
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Launcher
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    static string Root
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GuangXIW");
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
    /// 把导出目录里最新的 guangxiw-relations-*.json 推到手机 /sdcard/Download。
    public static string PushLatest()
    {
        try
        {
            string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(dl)) return "找不到下载目录";
            var files = Directory.GetFiles(dl, "guangxiw-relations-*.json");
            if (files.Length == 0) return "没找到关系摘要，先点「导出 → 给 SJ Lens（手机）」";
            Array.Sort(files);
            string newest = files[files.Length - 1];

            string adb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Android\Sdk\platform-tools\adb.exe");
            if (!File.Exists(adb)) adb = "adb";

            var psi = new System.Diagnostics.ProcessStartInfo(adb,
                "push \"" + newest + "\" /sdcard/Download/")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var pr = System.Diagnostics.Process.Start(psi))
            {
                string outp = pr.StandardOutput.ReadToEnd() + pr.StandardError.ReadToEnd();
                pr.WaitForExit(15000);
                if (pr.ExitCode == 0) return "已推送到手机：" + Path.GetFileName(newest);
                return "推送失败：" + (outp.Length > 90 ? outp.Substring(0, 90) : outp);
            }
        }
        catch (Exception ex) { return "推送失败：" + ex.Message; }
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
            string tmp = Path.Combine(Path.GetTempPath(), "sj-memos.json");
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
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                    null, Path.Combine(Path.GetDirectoryName(html), "wvprofile"));
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                web.CoreWebView2.WebMessageReceived += delegate (object s2, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e2)
                {
                    string msg = e2.TryGetWebMessageAsString();
                    if (msg == "push") web.CoreWebView2.PostWebMessageAsString(AdbPush.PushLatest());
                    if (msg == "pullMemos") web.CoreWebView2.PostWebMessageAsString(AdbPush.PullMemos());
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
}
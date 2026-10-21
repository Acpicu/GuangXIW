// GuangXIW 桌面宿主（WebView2 内嵌，不使用 Edge 浏览器窗口）
// index.html 与 WebView2 托管库都作为资源内嵌；运行时释放到
// %LOCALAPPDATA%\GuangXIW\app 再加载，所以分发只需要这一个 exe。
//
// 注意：Main 里绝不能出现 WebView2 类型。JIT 在方法入口就会解析类型引用，
// 那时 AssemblyResolve 还没注册，单文件运行时必然失败 —— 因此真正干活的代码
// 放在 AppHost.Run 里，由 Main 在注册事件之后再调用。
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

static class Launcher
{
    static string Root
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GuangXIW");
        }
    }

    [STAThread]
    static void Main()
    {
        string appDir = Path.Combine(Root, "app");
        Directory.CreateDirectory(appDir);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;

        Extract("Microsoft.Web.WebView2.Core.dll", appDir);
        Extract("Microsoft.Web.WebView2.WinForms.dll", appDir);
        Extract("WebView2Loader.dll", appDir);
        string html = Extract("index.html", appDir);

        AppHost.Run(html);
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
}

static class AppHost
{
    [STAThread]
    public static void Run(string html)
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

        var web = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
        // 关键：WebView2 默认白底，窗口失焦/重绘时会闪白，必须显式设成应用底色
        web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 17, 21);
        form.Controls.Add(web);

        form.Load += async delegate
        {
            try
            {
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                    null,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 "GuangXIW", "wvprofile"));
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                web.Source = new Uri(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "WebView2 初始化失败：" + ex.Message +
                    "\n\n此功能需要 WebView2 运行时（Win11 自带；Win10 装过 Edge 即具备）。",
                    "GuangXIW");
                form.Close();
            }
        };

        Application.Run(form);
    }
}

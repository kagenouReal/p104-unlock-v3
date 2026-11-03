using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

class P104Installer
{
    internal const string SvcName = "P104Gen2x4";
    internal const string TargetExe = @"C:\Program Files\P104Gen2x4\P104Gen2x4.exe";
    internal const string LogFile = @"C:\ProgramData\P104Gen2x4\status.txt";
    internal const string InstallerLog = @"C:\ProgramData\P104Gen2x4\installer.log";
    internal const string OriginalRepo = "https://github.com/skbskhud/p104-unlock-v3";

    [STAThread]
    static int Main(string[] args)
    {
        if (!IsAdmin())
        {
            const string msg = "This installer must run as administrator.";
            try { Console.Error.WriteLine(msg); } catch { }
            try { MessageBox.Show(msg, "P104 Setup", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            return 5;
        }

        if (args.Length > 0)
            return RunCli(args);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { Application.Run(new MainForm()); }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "P104 Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        return 0;
    }

    static int RunCli(string[] args)
    {
        string mode = args[0].ToLowerInvariant().TrimStart('-', '/');
        string payload = null;
        int rc = 0;
        try
        {
            payload = ExtractPayload();
            string exe = ToolExe(payload);
            if (mode == "verify" || mode == "check")
            {
                string raw = RunTool(exe, "-check");
                SaveDetails("check", SummarizeCheck(raw) ?? "Link status unavailable.");
                Console.WriteLine(SummarizeCheck(raw) ?? "Link status unavailable.");
            }
            else if (mode == "install" || mode == "install-service")
            {
                string raw = RunTool(Path.Combine(payload, "P104Gen2x4.exe"), "-install-service");
                SaveDetails("install", raw);
                string state = GetServiceState();
                Console.WriteLine("Service: " + state);
                rc = state == "Running" ? 0 : 1;
            }
            else if (mode == "uninstall" || mode == "uninstall-service")
            {
                string raw = RunTool(exe, "-uninstall-service");
                SaveDetails("uninstall", raw);
                string state = GetServiceState();
                Console.WriteLine("Service: " + state);
                rc = (state == "Not installed" || state == "Stopped") ? 0 : 1;
            }
            else if (mode == "selftest")
            {
                Console.WriteLine("Resources:");
                foreach (string n in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                    Console.WriteLine("  " + n);
                Console.WriteLine("Payload: " + payload);
                Console.WriteLine("Service: " + GetServiceState());
                string raw = RunTool(exe, "-check");
                SaveDetails("selftest", SummarizeCheck(raw) ?? "Link status unavailable.");
                Console.WriteLine(SummarizeCheck(raw) ?? "Link status unavailable.");
            }
            else
            {
                Console.Error.WriteLine("Usage: P104Gen2x4-Setup.exe [--install|--uninstall|--verify|--selftest]");
                rc = 2;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error: " + e.Message);
            rc = 1;
        }
        finally
        {
            try { if (payload != null) Directory.Delete(payload, true); } catch { }
        }
        return rc;
    }

    internal static bool IsAdmin()
    {
        try
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    internal static string ExtractPayload()
    {
        string dir = Path.Combine(Path.GetTempPath(), "P104Gen2x4-Setup-" + Process.GetCurrentProcess().Id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "drivers"));
        Extract("P104Gen2x4.exe", dir);
        Extract("WinRing0x64.sys", Path.Combine(dir, "drivers"));
        Extract("ThrottleStop.sys", Path.Combine(dir, "drivers"));
        Extract("README.md", dir);
        Extract("NOTICE.md", dir);
        return dir;
    }

    internal static void Extract(string name, string dir)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (s == null) throw new InvalidOperationException("Missing embedded resource: " + name);
            using (FileStream f = File.Create(Path.Combine(dir, name)))
                s.CopyTo(f);
        }
    }

    internal static string ToolExe(string payload)
    {
        return File.Exists(TargetExe) ? TargetExe : Path.Combine(payload, "P104Gen2x4.exe");
    }

    internal static string RunTool(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.Default,
            StandardErrorEncoding = Encoding.Default,
            WorkingDirectory = Path.GetDirectoryName(exe)
        };
        using (Process p = Process.Start(psi))
        {
            Task<string> so = p.StandardOutput.ReadToEndAsync();
            Task<string> se = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(180000))
            {
                try { p.Kill(); } catch { }
                return "Timed out after 180 seconds.";
            }
            string o = "", e = "";
            try { o = so.Result; } catch { }
            try { e = se.Result; } catch { }
            return (o + e).Trim();
        }
    }

    internal static string GetServiceState()
    {
        try
        {
            using (ServiceController sc = new ServiceController(SvcName))
                return sc.Status.ToString();
        }
        catch { return "Not installed"; }
    }

    internal static string SummarizeCheck(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        foreach (string line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.IndexOf("RESULT:", StringComparison.OrdinalIgnoreCase) < 0) continue;
            string speed = null;
            if (line.IndexOf("Gen3", StringComparison.OrdinalIgnoreCase) >= 0) speed = "Gen3";
            else if (line.IndexOf("Gen2", StringComparison.OrdinalIgnoreCase) >= 0) speed = "Gen2";
            else if (line.IndexOf("Gen1", StringComparison.OrdinalIgnoreCase) >= 0) speed = "Gen1";
            if (speed == null)
            {
                if (line.IndexOf("PASS", StringComparison.OrdinalIgnoreCase) >= 0) return "Link: PASS";
                if (line.IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) >= 0) return "Link: FAIL";
                return null;
            }
            MatchCollection widths = Regex.Matches(line, @"x(\d+)");
            string current = widths.Count > 0 ? "x" + widths[0].Groups[1].Value : null;
            string declared = widths.Count > 1 ? "x" + widths[widths.Count - 1].Groups[1].Value : null;
            string text = "Link: " + speed;
            if (current != null) text += " " + current;
            if (declared != null && declared != current) text += " (" + declared + " declared)";
            return text;
        }
        return null;
    }

    internal static void SaveDetails(string action, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(InstallerLog));
            File.AppendAllText(InstallerLog,
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + action + Environment.NewLine +
                (text ?? "") + Environment.NewLine + Environment.NewLine);
        }
        catch { }
    }

    internal class MainForm : Form
    {
        readonly string _payload;
        Label _serviceValue, _linkValue;
        TextBox _log;
        Button _install, _uninstall, _verify, _openLog, _readme, _exit;
        CheckBox _ack;
        bool _busy;

        public MainForm()
        {
            _payload = ExtractPayload();
            BuildUi();
            FormClosed += (s, e) => { try { Directory.Delete(_payload, true); } catch { } };
            Shown += async (s, e) => { await RefreshStatusAsync(); };
        }

        void BuildUi()
        {
            Text = "P104-100 PCIe Gen2 x4 Service Installer";
            ClientSize = new Size(800, 620);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(245, 245, 245);
            Font = new Font("Segoe UI", 9F);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.FromArgb(20, 20, 20) };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                Text = "P104-100  PCIe Gen2 x4",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(118, 185, 0),
                AutoSize = true, Location = new Point(18, 16)
            });
            header.Controls.Add(new Label
            {
                Text = "Windows Service Installer",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(210, 210, 210),
                AutoSize = true, Location = new Point(20, 52)
            });
            LinkLabel fork = new LinkLabel
            {
                Text = "fork of skbskhud/p104-unlock-v3",
                Font = new Font("Segoe UI", 8F),
                LinkColor = Color.FromArgb(160, 160, 160),
                ActiveLinkColor = Color.FromArgb(200, 200, 200),
                AutoSize = true, Location = new Point(585, 20)
            };
            fork.LinkClicked += (s, e) => OpenUrl(OriginalRepo);
            header.Controls.Add(fork);

            GroupBox status = new GroupBox
            {
                Text = "Status", Location = new Point(16, 108), Size = new Size(768, 100),
                BackColor = Color.White
            };
            Controls.Add(status);
            status.Controls.Add(new Label { Text = "Service", Location = new Point(16, 26), AutoSize = true, ForeColor = Color.DimGray });
            _serviceValue = new Label { Text = "...", Location = new Point(110, 26), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            status.Controls.Add(_serviceValue);
            status.Controls.Add(new Label { Text = "Link", Location = new Point(16, 52), AutoSize = true, ForeColor = Color.DimGray });
            _linkValue = new Label { Text = "...", Location = new Point(110, 52), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            status.Controls.Add(_linkValue);
            status.Controls.Add(new Label { Text = "GPU", Location = new Point(16, 78), AutoSize = true, ForeColor = Color.DimGray });
            status.Controls.Add(new Label { Text = "P104-100 (10DE:1B87)", Location = new Point(110, 78), AutoSize = true });

            _ack = new CheckBox
            {
                Text = "I understand this uses BYOVD drivers (WinRing0/ThrottleStop) and may trigger antivirus software.",
                Location = new Point(16, 218), Size = new Size(768, 22),
                ForeColor = Color.FromArgb(120, 60, 0)
            };
            _ack.CheckedChanged += (s, e) => { if (_install != null) _install.Enabled = _ack.Checked && !_busy; };
            Controls.Add(_ack);

            _install = MakeButton("Install or repair", 16, 248, 150, Color.FromArgb(118, 185, 0), Color.White);
            _uninstall = MakeButton("Uninstall", 174, 248, 100, Color.FromArgb(200, 60, 60), Color.White);
            _verify = MakeButton("Verify", 282, 248, 90, Color.FromArgb(60, 60, 60), Color.White);
            _openLog = MakeButton("Open log", 380, 248, 100, Color.FromArgb(90, 90, 90), Color.White);
            _readme = MakeButton("README", 488, 248, 100, Color.FromArgb(90, 90, 90), Color.White);
            _exit = MakeButton("Exit", 596, 248, 100, Color.FromArgb(120, 120, 120), Color.White);
            _install.Enabled = false;
            Controls.Add(_install); Controls.Add(_uninstall); Controls.Add(_verify);
            Controls.Add(_openLog); Controls.Add(_readme); Controls.Add(_exit);

            _log = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 298), Size = new Size(768, 250),
                Font = new Font("Consolas", 8.5F), BackColor = Color.White
            };
            Controls.Add(_log);

            Controls.Add(new Label
            {
                Text = "Unsigned build. Verify the release SHA256 before running.",
                Location = new Point(16, 552), AutoSize = true, ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8F)
            });
            Controls.Add(new Label
            {
                Text = "Based on skbskhud/p104-unlock-v3. Original work by skbskhud and contributors.",
                Location = new Point(16, 574), AutoSize = true, ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8F)
            });

            _install.Click += async (s, e) => await InstallAsync();
            _uninstall.Click += async (s, e) => await UninstallAsync();
            _verify.Click += async (s, e) => await VerifyAsync();
            _openLog.Click += (s, e) => OpenLog();
            _readme.Click += (s, e) => OpenReadme();
            _exit.Click += (s, e) => Close();
        }

        Button MakeButton(string text, int x, int y, int w, Color back, Color fore)
        {
            Button b = new Button
            {
                Text = text, Location = new Point(x, y), Size = new Size(w, 34),
                FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            _install.Enabled = !busy && _ack.Checked;
            _uninstall.Enabled = !busy;
            _verify.Enabled = !busy;
            _openLog.Enabled = !busy;
            _readme.Enabled = !busy;
            UseWaitCursor = busy;
        }

        void Log(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s.TrimEnd() + Environment.NewLine);
        }

        async Task RefreshStatusAsync()
        {
            string state = GetServiceState();
            _serviceValue.Text = state;
            _serviceValue.ForeColor = state == "Running" ? Color.FromArgb(0, 128, 0) : Color.FromArgb(180, 40, 40);

            string check = "";
            try
            {
                string exe = ToolExe(_payload);
                check = await Task.Run(() => RunTool(exe, "-check"));
            }
            catch { }

            string result = SummarizeCheck(check);
            if (result == null) result = state == "Running" ? "Service running; waiting for link status" : "Not unlocked";
            _linkValue.Text = result;
            _linkValue.ForeColor = (result.IndexOf("Gen2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    result.IndexOf("Gen3", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    result.IndexOf("PASS", StringComparison.OrdinalIgnoreCase) >= 0)
                ? Color.FromArgb(0, 128, 0) : Color.FromArgb(180, 40, 40);
        }

        async Task InstallAsync()
        {
            if (!_ack.Checked) return;
            if (MessageBox.Show("Install or repair the Windows service now?\n\nIt starts immediately and again at every boot.",
                "P104 Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            SetBusy(true);
            Log("Installing service...");
            try
            {
                string exe = Path.Combine(_payload, "P104Gen2x4.exe");
                string outp = await Task.Run(() => RunTool(exe, "-install-service"));
                SaveDetails("install", outp);
            }
            catch (Exception e) { Log("Error: " + e.Message); }
            SetBusy(false);
            await RefreshStatusAsync();
            string state = GetServiceState();
            Log("Service: " + state);
            if (state == "Running")
                MessageBox.Show("Installation complete. The service is running.",
                    "P104 Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Installation finished, but the service is not running.\n\nSee " + InstallerLog + " for details.",
                    "P104 Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        async Task UninstallAsync()
        {
            if (MessageBox.Show("Stop and remove the Windows service?\n\nDriver policy keys will be kept.",
                "P104 Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            SetBusy(true);
            Log("Uninstalling service...");
            try
            {
                string outp = await Task.Run(() => RunTool(ToolExe(_payload), "-uninstall-service"));
                SaveDetails("uninstall", outp);
            }
            catch (Exception e) { Log("Error: " + e.Message); }
            SetBusy(false);
            await RefreshStatusAsync();
            string state = GetServiceState();
            Log("Service: " + state);
            bool removed = state == "Not installed" || state == "Stopped";
            MessageBox.Show(removed
                    ? "Service removed. Driver policy keys were kept."
                    : "Uninstall finished, but the service is still present.\n\nSee " + InstallerLog + " for details.",
                "P104 Setup", MessageBoxButtons.OK,
                removed ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        async Task VerifyAsync()
        {
            SetBusy(true);
            Log("Verifying...");
            try
            {
                string outp = await Task.Run(() => RunTool(ToolExe(_payload), "-check"));
                SaveDetails("check", SummarizeCheck(outp) ?? "Link status unavailable.");
                Log(SummarizeCheck(outp) ?? "Link status unavailable.");
            }
            catch (Exception e) { Log("Error: " + e.Message); }
            SetBusy(false);
            await RefreshStatusAsync();
        }

        void OpenLog()
        {
            try
            {
                if (!File.Exists(LogFile)) { MessageBox.Show("Log file not found:\n" + LogFile, "P104 Setup"); return; }
                Process.Start("notepad.exe", LogFile);
            }
            catch (Exception e) { MessageBox.Show(e.Message, "P104 Setup"); }
        }

        void OpenReadme()
        {
            try
            {
                string p = Path.Combine(_payload, "README.md");
                if (!File.Exists(p)) { MessageBox.Show("README not found.", "P104 Setup"); return; }
                Process.Start("notepad.exe", p);
            }
            catch (Exception e) { MessageBox.Show(e.Message, "P104 Setup"); }
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(url); } catch { }
        }
    }
}

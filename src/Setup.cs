// Hollywood Monsters Enhanced - setup.
// Installs the game from your own disc (a CD drive, a mounted ISO or an .iso file) or adds the enhancements
// to a game you already installed, then puts a shortcut on the desktop.
// Build (C# 5, .NET Framework 4):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:"Setup.exe" Setup.cs
// The files it adds are in the "files" folder next to Setup.exe.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class SetupProgram
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new SetupForm());
    }
}

class SetupForm : Form
{
    readonly RadioButton fromDisc = new RadioButton(), fromFolder = new RadioButton();
    readonly TextBox source = new TextBox(), target = new TextBox();
    readonly Label targetLabel = new Label(), status = new Label();
    readonly Button browseSource = new Button(), browseTarget = new Button(), install = new Button(), close = new Button();
    readonly ProgressBar bar = new ProgressBar();
    readonly CheckBox shortcut = new CheckBox();
    static readonly string Files = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files");

    public SetupForm()
    {
        Text = "Hollywood Monsters Enhanced - Setup";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 400);
        Font = new Font("Segoe UI", 9.5f);

        Label intro = Add(new Label(), "You need your own copy of Hollywood Monsters (Italian FX Interactive edition, 2004). "
            + "This setup copies the game from your disc, or uses a game you already installed, and adds the new launcher, "
            + "the graphics filters and the sound fixes.", 20, 15, 480, 60);

        fromDisc.Text = "Install from the disc, a mounted ISO or an .iso file";
        fromFolder.Text = "Add the enhancements to a game I already installed";
        Add(fromDisc, null, 20, 82, 480, 24); Add(fromFolder, null, 20, 108, 480, 24);
        fromDisc.Checked = true;

        Add(new Label(), "Where is it?", 20, 145, 120, 22);
        Add(source, null, 20, 168, 380, 26);
        browseSource.Text = "Browse..."; Add(browseSource, null, 410, 166, 90, 28);

        Add(targetLabel, "Install the game in", 20, 205, 300, 22);
        Add(target, null, 20, 228, 380, 26);
        target.Text = @"C:\Games\Hollywood Monsters";
        browseTarget.Text = "Browse..."; Add(browseTarget, null, 410, 226, 90, 28);

        shortcut.Text = "Put a shortcut on the desktop"; shortcut.Checked = true; Add(shortcut, null, 20, 265, 300, 24);
        Add(bar, null, 20, 300, 480, 18);
        Add(status, "", 20, 322, 480, 40); status.ForeColor = Color.DimGray;

        install.Text = "Install"; Add(install, null, 300, 360, 95, 30);
        close.Text = "Close"; Add(close, null, 405, 360, 95, 30);

        source.Text = FindDisc() ?? "";
        fromDisc.CheckedChanged += delegate { Mode(); };
        Mode();
        browseSource.Click += delegate { BrowseSource(); };
        browseTarget.Click += delegate
        {
            using (var d = new FolderBrowserDialog { Description = "Where should the game go?" })
                if (d.ShowDialog(this) == DialogResult.OK) target.Text = Path.Combine(d.SelectedPath, "Hollywood Monsters");
        };
        close.Click += delegate { Close(); };
        install.Click += delegate { Start(); };
    }

    T Add<T>(T c, string text, int x, int y, int w, int h) where T : Control
    {
        if (text != null) c.Text = text;
        c.SetBounds(x, y, w, h); Controls.Add(c); return c;
    }

    void Mode()
    {
        bool disc = fromDisc.Checked;
        targetLabel.Enabled = target.Enabled = browseTarget.Enabled = disc;
        if (!disc && !IsGameFolder(source.Text)) source.Text = "";
        if (disc && source.Text == "") source.Text = FindDisc() ?? "";
    }

    void BrowseSource()
    {
        if (fromDisc.Checked)
        {
            using (var d = new OpenFileDialog { Title = "Choose the disc image", Filter = "Disc image (*.iso)|*.iso|All files|*.*" })
            {
                if (d.ShowDialog(this) == DialogResult.OK) { source.Text = d.FileName; return; }
            }
            using (var d = new FolderBrowserDialog { Description = "Or choose the CD drive" })
                if (d.ShowDialog(this) == DialogResult.OK) source.Text = d.SelectedPath;
        }
        else
            using (var d = new FolderBrowserDialog { Description = "Choose the folder with monsters.exe" })
                if (d.ShowDialog(this) == DialogResult.OK) source.Text = d.SelectedPath;
    }

    static bool IsGameFolder(string dir)
    {
        return dir != "" && File.Exists(Path.Combine(dir, "monsters.exe")) && File.Exists(Path.Combine(dir, "RESOURCE.004"));
    }

    // A drive that holds the game disc: \Monsters\monsters.exe
    static string FindDisc()
    {
        foreach (DriveInfo d in DriveInfo.GetDrives())
            try { if (d.IsReady && File.Exists(Path.Combine(d.RootDirectory.FullName, @"Monsters\monsters.exe"))) return d.RootDirectory.FullName; }
            catch { }
        return null;
    }

    void Say(string text) { Invoke((Action)delegate { status.Text = text; }); }
    void Progress(int v) { Invoke((Action)delegate { bar.Value = Math.Max(0, Math.Min(100, v)); }); }

    void Start()
    {
        if (!Directory.Exists(Files)) { MessageBox.Show(this, "The \"files\" folder is missing next to Setup.exe. Download the whole package again.", Text); return; }
        string src = source.Text.Trim().Trim('"'), dst = fromDisc.Checked ? target.Text.Trim().Trim('"') : src;
        if (src == "") { MessageBox.Show(this, fromDisc.Checked ? "Insert the disc, or choose the .iso file." : "Choose the folder of the installed game.", Text); return; }
        install.Enabled = close.Enabled = fromDisc.Enabled = fromFolder.Enabled = browseSource.Enabled = browseTarget.Enabled = false;
        bool disc = fromDisc.Checked, link = shortcut.Checked;
        new Thread(delegate ()
        {
            string error = null;
            try { Work(disc, src, dst, link); }
            catch (Exception e) { error = e.Message; }
            Invoke((Action)delegate
            {
                close.Enabled = true;
                if (error != null)
                {
                    status.Text = "Not finished: " + error;
                    install.Enabled = fromDisc.Enabled = fromFolder.Enabled = browseSource.Enabled = true; Mode();
                    MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    bar.Value = 100;
                    status.Text = (warning != null ? warning + " " : "") + "Done. Start the game with the shortcut, or with \"Hollywood Monsters Launcher.exe\" in " + dst;
                    close.Text = "Play";
                    close.Click += delegate { Process.Start(new ProcessStartInfo(Path.Combine(dst, "Hollywood Monsters Launcher.exe")) { WorkingDirectory = dst }); };
                }
            });
        }) { IsBackground = true }.Start();
    }

    string warning;

    void Work(bool disc, string src, string dst, bool link)
    {
        string mounted = null;
        try
        {
            if (disc)
            {
                string root = src;
                if (File.Exists(src) && src.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                {
                    Say("Opening the disc image...");
                    mounted = src; root = Mount(src);
                }
                string game = Path.Combine(root, "Monsters");
                if (!IsGameFolder(game)) throw new Exception("This is not the Hollywood Monsters disc (no Monsters\\monsters.exe on it).");
                CopyGame(game, dst);
            }
            else if (!IsGameFolder(dst)) throw new Exception("monsters.exe was not found in that folder.");

            Say("Adding the launcher, the graphics filters and the sound fixes...");
            CopyTree(Files, dst, null);
            Progress(92);

            Say("Saving the original start screen for the launcher...");
            string art = Path.Combine(dst, "launcher-art.png");
            bool saved = File.Exists(art);
            for (int attempt = 0; attempt < 3 && !saved; attempt++) saved = CaptureArt(dst, art);   // the old window is sometimes slow to draw
            if (!saved)
                warning = "The original start screen could not be saved, so the launcher opens its settings window. Run the setup again to retry.";
            Progress(97);

            if (link) Shortcut(dst);
            if (!new FileInfo(Path.Combine(dst, "monsters.exe")).Length.Equals(1167360L))
                warning = (warning == null ? "" : warning + " ") + "This monsters.exe is not the Italian FX 2004 one: the in-game sound sliders fix stays off.";
        }
        finally { if (mounted != null) Unmount(mounted); }
    }

    void CopyGame(string from, string to)
    {
        long total = 0, done = 0;
        foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) total += new FileInfo(f).Length;
        DriveInfo drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(to)));
        if (drive.AvailableFreeSpace < total + 50000000) throw new Exception("Not enough free space on " + drive.Name + " (about " + (total / 1000000 + 50) + " MB needed).");
        CopyTree(from, to, delegate (long n) { done += n; Progress((int)(done * 90 / total)); Say("Copying the game... " + (done / 1000000) + " of " + (total / 1000000) + " MB"); });
    }

    static void CopyTree(string from, string to, Action<long> step)
    {
        Directory.CreateDirectory(to);
        foreach (string d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, d.Substring(from.Length).TrimStart('\\')));
        foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string t = Path.Combine(to, f.Substring(from.Length).TrimStart('\\'));
            if (File.Exists(t)) File.SetAttributes(t, FileAttributes.Normal);
            File.Copy(f, t, true);
            File.SetAttributes(t, FileAttributes.Normal);   // files from a CD are read-only
            if (step != null) step(new FileInfo(t).Length);
        }
    }

    static string PowerShell(string script)
    {
        var p = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + script.Replace("\"", "\\\"") + "\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
        string o = p.StandardOutput.ReadToEnd(); string e = p.StandardError.ReadToEnd(); p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception(e.Trim() == "" ? "PowerShell failed" : e.Trim());
        return o.Trim();
    }

    static string Mount(string iso)
    {
        string q = iso.Replace("'", "''");
        string letter = PowerShell("$i = Get-DiskImage -ImagePath '" + q + "'; if (-not $i.Attached) { $i = Mount-DiskImage -ImagePath '" + q + "' -Access ReadOnly -PassThru }; ($i | Get-Volume).DriveLetter");
        if (letter.Length < 1) throw new Exception("The disc image could not be opened.");
        return letter.Substring(letter.Length - 1) + @":\";
    }

    static void Unmount(string iso)
    {
        try { PowerShell("Dismount-DiskImage -ImagePath '" + iso.Replace("'", "''") + "' | Out-Null"); } catch { }
    }

    // The original FX start window keeps its picture packed inside a Macromedia Director file, so the setup opens it
    // for a few seconds and saves what it shows. Nothing is clicked in it.
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref Point p);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    static bool CaptureArt(string dir, string file)
    {
        string exe = Path.Combine(dir, "Inizio del Gioco.exe");
        if (!File.Exists(exe)) return false;
        Process p = null;
        try
        {
            p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = dir });
            IntPtr h = IntPtr.Zero;
            for (int i = 0; i < 60 && h == IntPtr.Zero; i++) { Thread.Sleep(250); h = FindWindow("ImlWinCls", "Inizio del Gioco"); }
            if (h == IntPtr.Zero) { Log(dir, "art: start window not found"); return false; }
            Thread.Sleep(3500);
            RECT r; GetClientRect(h, out r);
            if (r.R < 400 || r.B < 300) { Log(dir, "art: window too small " + r.R + "x" + r.B); return false; }
            Point o = new Point(0, 0); ClientToScreen(h, ref o);
            using (Bitmap b = new Bitmap(r.R, r.B))
            {
                using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(o, Point.Empty, b.Size);
                // refuse a capture covered by another window: the corners must show the dark FX frame
                Color c = b.GetPixel(r.R / 2, r.B - 3);
                if (c.R + c.G + c.B > 200) { Log(dir, "art: covered by another window"); return false; }
                b.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            }
            return true;
        }
        catch (Exception e) { Log(dir, "art: " + e.Message); return false; }
        finally { try { if (p != null && !p.HasExited) p.Kill(); } catch { } }
    }

    static void Log(string dir, string line)
    {
        try { File.AppendAllText(Path.Combine(dir, "setup.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + line + Environment.NewLine); } catch { }
    }

    static void Shortcut(string dir)
    {
        string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Hollywood Monsters.lnk");
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(t);
        object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
        Type st = sc.GetType();
        st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { Path.Combine(dir, "Hollywood Monsters Launcher.exe") });
        st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { dir });
        string ico = Path.Combine(dir, "MVM.ico");
        if (File.Exists(ico)) st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { ico });
        st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
    }
}

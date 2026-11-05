// Hollywood Monsters launcher: screen, mouse speed and volume settings, then it starts the game.
// Build (C# 5, .NET Framework 4):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:"..\Hollywood Monsters Launcher.exe" Launcher.cs
// Files next to the exe: launcher.ini (choices), launcher.log (what it did), mouse-restore.txt (only while the game runs).
// "--play" starts the game at once with the saved choices (no window).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    public static readonly string Dir = AppDomain.CurrentDomain.BaseDirectory;
    public static string GameExe { get { return Path.Combine(Dir, "monsters.exe"); } }
    public static string SettingsFile { get { return Path.Combine(Dir, "launcher.ini"); } }
    static string LogFile { get { return Path.Combine(Dir, "launcher.log"); } }
    static string RestoreFile { get { return Path.Combine(Dir, "mouse-restore.txt"); } }

    [STAThread]
    static void Main(string[] args)
    {
        RestoreMouseAfterCrash();
        Settings s = Settings.Load(SettingsFile);
        bool play = args.Length > 0 && args[0] == "--play";
        if (!play)
        {
            Application.EnableVisualStyles();
            // the start window uses the original artwork saved by the setup (launcher-art.png); without it, the settings window
            Form f = File.Exists(ArtForm.ArtFile) ? (Form)new ArtForm(s) : new LauncherForm(s);
            Application.Run(f);
            if (!(f is ArtForm ? ((ArtForm)f).PlayChosen : ((LauncherForm)f).PlayChosen)) return;
        }
        Run(s);
    }

    public static void Log(string line)
    {
        try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + line + "\r\n"); } catch { }
    }

    // If the launcher was closed while the game ran, put the Windows mouse speed back as it was.
    static void RestoreMouseAfterCrash()
    {
        try
        {
            if (!File.Exists(RestoreFile)) return;
            int old = int.Parse(File.ReadAllText(RestoreFile).Trim());
            Mouse.SetSpeed(old);
            File.Delete(RestoreFile);
            Log("mouse speed restored after an unfinished run: " + old);
        }
        catch (Exception e) { Log("restore after crash failed: " + e.Message); }
    }

    // After the game closes, copy its saved games (SAVEGAME.*) into "Save backups\<date time>".
    // A copy is made only when the saves changed since the last one; the newest 20 copies are kept.
    public static void BackupSaves()
    {
        try
        {
            string[] saves = Directory.GetFiles(Dir, "SAVEGAME.*");
            if (saves.Length == 0) { Log("backup: no saved games yet"); return; }
            string root = Path.Combine(Dir, "Save backups");
            Directory.CreateDirectory(root);
            string[] old = Directory.GetDirectories(root);
            Array.Sort(old);
            if (old.Length > 0 && SameFiles(saves, old[old.Length - 1])) { Log("backup: saves unchanged, no new copy"); return; }
            string dest = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss"));
            Directory.CreateDirectory(dest);
            foreach (string f in saves) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            Log("backup: " + saves.Length + " saved game file(s) copied to " + dest);
            old = Directory.GetDirectories(root);
            Array.Sort(old);
            for (int i = 0; i < old.Length - 20; i++) Directory.Delete(old[i], true);
        }
        catch (Exception e) { Log("backup failed: " + e.Message); }
    }

    static bool SameFiles(string[] saves, string folder)
    {
        if (Directory.GetFiles(folder).Length != saves.Length) return false;
        foreach (string f in saves)
        {
            string b = Path.Combine(folder, Path.GetFileName(f));
            if (!File.Exists(b)) return false;
            byte[] x = File.ReadAllBytes(f), y = File.ReadAllBytes(b);
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
        }
        return true;
    }

    static void Run(Settings s)
    {
        s.Save(SettingsFile);
        Ddraw.Write(Path.Combine(Dir, "ddraw.ini"), s);
        Log(string.Format("start: screen={0} filter={1} mouse={2} volume={3}", Settings.DisplayNames[s.Display], Settings.Filters[s.Filter][0], s.MouseSpeed, s.Volume));
        int oldSpeed = Mouse.GetSpeed();
        bool mouseChanged = false;
        if (s.MouseSpeed >= 1 && s.MouseSpeed <= 20 && s.MouseSpeed != oldSpeed)
        {
            File.WriteAllText(RestoreFile, oldSpeed.ToString());
            Mouse.SetSpeed(s.MouseSpeed);
            mouseChanged = true;
            Log("mouse speed " + oldSpeed + " -> " + Mouse.GetSpeed());
        }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(GameExe);
            psi.WorkingDirectory = Dir;
            psi.UseShellExecute = false;
            Process game = Process.Start(psi);
            Log("game started, pid " + game.Id);
            float got = -1;
            for (int i = 0; i < 60 && !game.HasExited && got < 0; i++)
            {
                Thread.Sleep(500);
                got = AudioSessions.SetVolume(game.Id, s.Volume / 100f);
            }
            Log(got >= 0 ? "game volume now " + Math.Round(got * 100) + "% (wanted " + s.Volume + "%)" : "game volume NOT set (no sound session found in 30 s)");
            GameMemory.ApplyAndFollow(game, s);
            s.Save(SettingsFile);
            Log(string.Format("kept for next time: music={0} sfx={1} voice={2} subtitles={3}", s.Music, s.Sfx, s.Voice, s.Subtitles));
            Log("game ended, exit code " + game.ExitCode);
            BackupSaves();
        }
        catch (Exception e)
        {
            Log("error: " + e.Message);
            MessageBox.Show("The game could not start:\n" + e.Message, "Hollywood Monsters", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (mouseChanged)
            {
                Mouse.SetSpeed(oldSpeed);
                try { File.Delete(RestoreFile); } catch { }
                Log("mouse speed back to " + Mouse.GetSpeed());
            }
        }
    }
}

class Settings
{
    public static readonly string[] DisplayNames = {
        "Fullscreen (keep 4:3)", "Fullscreen (stretched)", "Window 640 x 480", "Window 960 x 720", "Window 1280 x 960" };
    // name, renderer, shader file (used by OpenGL), d3d9_filter (used by Direct3D 9: 0 sharp, 1 smooth)
    public static readonly string[][] Filters = {
        new[] { "Sharp pixels (original)", "auto", @"Shaders\nearest-neighbor.glsl", "0" },
        new[] { "Smooth (bilinear)", "auto", @"Shaders\interpolation\bilinear.glsl", "1" },
        new[] { "Bicubic (soft)", "opengl", @"Shaders\interpolation\catmull-rom-bilinear.glsl", "1" },
        new[] { "Lanczos (sharper)", "opengl", @"Shaders\interpolation\lanczos2-sharp.glsl", "1" },
        new[] { "Jinc2 (removes dithering)", "opengl", @"Shaders\interpolation\jinc2-dedither.glsl", "1" },
        new[] { "AMD FSR (modern upscaler)", "opengl", @"Shaders\interpolation\fsr.glsl", "1" },
        new[] { "xBR (smooth edges)", "opengl", @"Shaders\xbr\xbr-lv2-noblend.glsl", "1" },
        new[] { "xBRZ (smoothest edges)", "opengl", @"Shaders\xbrz\xbrz-freescale-multipass.glsl", "1" },
        new[] { "SABR (smooth edges, softer)", "opengl", @"Shaders\sabr\sabr-v3.0.glsl", "1" },
        new[] { "OmniScale (clean lines)", "opengl", @"Shaders\omniscale\omniscale.glsl", "1" },
        new[] { "ScaleHQ (soft, detailed)", "opengl", @"Shaders\scalehq\4xScaleHQ.glsl", "1" },
        new[] { "Sharpen", "opengl", @"Shaders\sharpen\rca-sharpen.glsl", "0" },
        new[] { "CRT TV", "opengl", @"Shaders\crt\crt-lottes-fast-no-warp-bilinear.glsl", "1" },
        new[] { "Scanlines", "opengl", @"Shaders\scanlines\scanline.glsl", "0" } };
    public int Display = 0;
    public int Filter = 0;
    public int MouseSpeed = 0;   // 1-20; 0 = leave the Windows speed alone
    public int Volume = 100;     // 0-100, the whole game
    public static readonly string[] SubtitleNames = { "Voice only", "Voice and text", "Text only" };
    public int Music = 40, Sfx = 50, Voice = 100;   // 0-100, the game's own sliders (its defaults)
    public int Subtitles = 1;                        // 0 voice, 1 both, 2 text (game value)

    public static Settings Load(string path)
    {
        Settings s = new Settings();
        try
        {
            if (File.Exists(path))
                foreach (string raw in File.ReadAllLines(path))
                {
                    int eq = raw.IndexOf('=');
                    if (eq < 0) continue;
                    string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                    int n;
                    if (!int.TryParse(v, out n)) continue;
                    if (k == "display" && n >= 0 && n < DisplayNames.Length) s.Display = n;
                    else if (k == "sharp") s.Filter = n != 0 ? 0 : 1;
                    else if (k == "filter" && n >= 0 && n < Filters.Length) s.Filter = n;
                    else if (k == "mousespeed" && n >= 0 && n <= 20) s.MouseSpeed = n;
                    else if (k == "volume" && n >= 0 && n <= 100) s.Volume = n;
                    else if (k == "music" && n >= 0 && n <= 100) s.Music = n;
                    else if (k == "sfx" && n >= 0 && n <= 100) s.Sfx = n;
                    else if (k == "voice" && n >= 0 && n <= 100) s.Voice = n;
                    else if (k == "subtitles" && n >= 0 && n <= 2) s.Subtitles = n;
                }
        }
        catch { }
        return s;
    }

    public void Save(string path)
    {
        File.WriteAllText(path, string.Format(
            "; Hollywood Monsters launcher choices\r\ndisplay={0}\r\nfilter={1}\r\nmousespeed={2}\r\nvolume={3}\r\nmusic={4}\r\nsfx={5}\r\nvoice={6}\r\nsubtitles={7}\r\n",
            Display, Filter, MouseSpeed, Volume, Music, Sfx, Voice, Subtitles));
    }
}

static class Ddraw
{
    // Changes only the keys the launcher controls and keeps every other line of ddraw.ini.
    public static void Write(string path, Settings s)
    {
        bool full = s.Display <= 1;
        int[] w = { 0, 0, 640, 960, 1280 }, h = { 0, 0, 480, 720, 960 };
        Dictionary<string, string> want = new Dictionary<string, string>();
        want["fullscreen"] = full ? "true" : "false";
        want["windowed"] = "true";
        want["width"] = w[s.Display].ToString();
        want["height"] = h[s.Display].ToString();
        want["maintas"] = s.Display == 1 ? "false" : "true";
        want["border"] = full ? "false" : "true";
        string[] f = Settings.Filters[s.Filter];
        want["renderer"] = f[1];
        want["shader"] = f[2];
        want["d3d9_filter"] = f[3];
        want["savesettings"] = "0";
        List<string> lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string> { "[ddraw]" };
        HashSet<string> done = new HashSet<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            int eq = lines[i].IndexOf('=');
            if (eq <= 0 || lines[i].TrimStart().StartsWith(";")) continue;
            string key = lines[i].Substring(0, eq).Trim().ToLowerInvariant();
            if (want.ContainsKey(key)) { lines[i] = key + "=" + want[key]; done.Add(key); }
        }
        int at = lines.FindIndex(l => l.Trim().ToLowerInvariant() == "[ddraw]");
        foreach (KeyValuePair<string, string> kv in want)
            if (!done.Contains(kv.Key)) lines.Insert(at + 1, kv.Key + "=" + kv.Value);
        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
    }
}

static class Mouse
{
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint ini);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint ini);
    public static int GetSpeed() { int v = 10; SystemParametersInfo(0x0070, 0, ref v, 0); return v; }
    // Only for this Windows session (not written to the registry): a restart always brings back the user's speed.
    public static void SetSpeed(int v) { SystemParametersInfo(0x0071, 0, (IntPtr)v, 0); }
}

static class AudioSessions
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        void GetCount(out int count);
        void Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        void GetAudioSessionControl(IntPtr sessionGuid, int streamFlags, out IntPtr control);
        void GetSimpleAudioVolume(IntPtr sessionGuid, int streamFlags, out IntPtr volume);
        void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        void GetState(out int state);
        void GetDisplayName(out IntPtr name);
        void SetDisplayName(IntPtr name, IntPtr context);
        void GetIconPath(out IntPtr path);
        void SetIconPath(IntPtr path, IntPtr context);
        void GetGroupingParam(out Guid group);
        void SetGroupingParam(IntPtr group, IntPtr context);
        void RegisterAudioSessionNotification(IntPtr client);
        void UnregisterAudioSessionNotification(IntPtr client);
        void GetSessionIdentifier(out IntPtr id);
        void GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint pid);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        void SetMasterVolume(float level, IntPtr context);
        void GetMasterVolume(out float level);
    }

    // Sets the volume of the game's own sound session on every active output device
    // (the same slider as in the Windows volume mixer). level < 0 only reads it.
    // Returns the volume found (0-1), or -1 if the game has no sound session yet.
    public static float SetVolume(int pid, float level)
    {
        float result = -1;
        try
        {
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDeviceCollection devices;
            en.EnumAudioEndpoints(0, 1, out devices);
            int devCount;
            devices.GetCount(out devCount);
            for (int d = 0; d < devCount; d++)
            {
                IMMDevice dev;
                devices.Item(d, out dev);
                Guid iid = typeof(IAudioSessionManager2).GUID;
                object o;
                dev.Activate(ref iid, 23, IntPtr.Zero, out o);
                IAudioSessionEnumerator sessions;
                ((IAudioSessionManager2)o).GetSessionEnumerator(out sessions);
                int count;
                sessions.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 c;
                    sessions.GetSession(i, out c);
                    uint p;
                    if (c.GetProcessId(out p) < 0 || p != (uint)pid) continue;
                    ISimpleAudioVolume v = (ISimpleAudioVolume)c;
                    if (level >= 0) v.SetMasterVolume(level, IntPtr.Zero);
                    v.GetMasterVolume(out result);
                }
            }
        }
        catch (Exception e) { Program.Log("volume error: " + e.Message); }
        return result;
    }
}

class LauncherForm : Form
{
    public bool PlayChosen;
    readonly Settings s;
    readonly ComboBox display = new ComboBox();
    readonly ComboBox filter = new ComboBox();
    readonly TrackBar mouse = new TrackBar(), volume = new TrackBar();
    readonly Label mouseValue = new Label(), volumeValue = new Label();
    readonly TrackBar music = new TrackBar(), sfx = new TrackBar(), voice = new TrackBar();
    readonly ComboBox subtitles = new ComboBox();

    public LauncherForm(Settings settings, bool optionsOnly = false)
    {
        s = settings;
        Text = "Hollywood Monsters";
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 545);
        try { Icon = Icon.ExtractAssociatedIcon(Program.GameExe); } catch { }

        AddLabel("Screen", 20, 22);
        display.DropDownStyle = ComboBoxStyle.DropDownList;
        display.Items.AddRange(Settings.DisplayNames);
        display.SelectedIndex = s.Display;
        display.SetBounds(160, 18, 255, 28);
        Controls.Add(display);

        AddLabel("Graphics filter", 20, 62);
        filter.DropDownStyle = ComboBoxStyle.DropDownList;
        filter.MaxDropDownItems = Settings.Filters.Length;
        foreach (string[] f in Settings.Filters) filter.Items.Add(f[0]);
        filter.SelectedIndex = s.Filter;
        filter.SetBounds(160, 58, 255, 28);
        Controls.Add(filter);

        int windowsSpeed = Mouse.GetSpeed();
        AddLabel("Mouse speed", 20, 108);
        mouse.SetBounds(150, 100, 200, 45);
        mouse.Minimum = 1; mouse.Maximum = 20; mouse.TickFrequency = 1;
        mouse.Value = s.MouseSpeed >= 1 ? s.MouseSpeed : windowsSpeed;
        mouseValue.SetBounds(355, 108, 80, 24);
        mouse.ValueChanged += delegate { mouseValue.Text = mouse.Value + " / 20"; };
        mouseValue.Text = mouse.Value + " / 20";
        Controls.Add(mouse); Controls.Add(mouseValue);

        AddLabel("Game volume", 20, 163);
        volume.SetBounds(150, 155, 200, 45);
        volume.Minimum = 0; volume.Maximum = 100; volume.TickFrequency = 10; volume.SmallChange = 5; volume.LargeChange = 10;
        volume.Value = s.Volume;
        volumeValue.SetBounds(355, 163, 80, 24);
        volume.ValueChanged += delegate { volumeValue.Text = volume.Value + "%"; };
        volumeValue.Text = volume.Value + "%";
        Controls.Add(volume); Controls.Add(volumeValue);

        AddSlider("Music", music, s.Music, 210);
        AddSlider("Sound effects", sfx, s.Sfx, 265);
        AddSlider("Voices", voice, s.Voice, 320);

        AddLabel("Subtitles", 20, 382);
        subtitles.DropDownStyle = ComboBoxStyle.DropDownList;
        subtitles.Items.AddRange(Settings.SubtitleNames);
        subtitles.SelectedIndex = s.Subtitles;
        subtitles.SetBounds(160, 378, 255, 28);
        Controls.Add(subtitles);

        Label note = AddLabel("Music, sound effects, voices and subtitles are set in the game when it starts. "
            + "If you change them in the game (Esc), the launcher keeps your new choice for next time. "
            + "Mouse speed applies only while the game runs. Alt+Enter: window / fullscreen.", 20, 418);
        note.SetBounds(20, 418, 400, 75);
        note.ForeColor = Color.DimGray;

        Button play = new Button(), quit = new Button();
        play.Text = optionsOnly ? "Save" : "Play"; play.SetBounds(220, 497, 95, 34);
        quit.Text = optionsOnly ? "Cancel" : "Quit"; quit.SetBounds(325, 497, 95, 34);
        if (optionsOnly) Text = "Hollywood Monsters - Options";
        play.Click += delegate { Save(); PlayChosen = true; Close(); };
        quit.Click += delegate { Close(); };
        Controls.Add(play); Controls.Add(quit);
        AcceptButton = play;
        CancelButton = quit;
    }

    void AddSlider(string name, TrackBar bar, int value, int y)
    {
        AddLabel(name, 20, y + 8);
        Label shown = new Label();
        bar.SetBounds(150, y, 200, 45);
        bar.Minimum = 0; bar.Maximum = 100; bar.TickFrequency = 10; bar.SmallChange = 5; bar.LargeChange = 10;
        bar.Value = value;
        shown.SetBounds(355, y + 8, 80, 24);
        bar.ValueChanged += delegate { shown.Text = bar.Value + "%"; };
        shown.Text = bar.Value + "%";
        Controls.Add(bar); Controls.Add(shown);
    }

    Label AddLabel(string text, int x, int y)
    {
        Label l = new Label();
        l.Text = text;
        l.SetBounds(x, y, 135, 24);
        Controls.Add(l);
        return l;
    }

    void Save()
    {
        s.Display = display.SelectedIndex;
        s.Filter = filter.SelectedIndex;
        s.MouseSpeed = mouse.Value;
        s.Volume = volume.Value;
        s.Music = music.Value;
        s.Sfx = sfx.Value;
        s.Voice = voice.Value;
        s.Subtitles = subtitles.SelectedIndex;
    }
}

// The game keeps music, sound effects and voice volume (0-200) and the subtitle mode in fixed places in its memory
// and resets them at every start. Its own arrows move far too fast on modern PCs (one step per loop while the
// button is held), so the launcher writes the chosen values once the game has set its defaults, then reads them
// every second so changes made in the game's menu are kept for next time.
// Addresses found for this monsters.exe (FX 2004 Italian edition, 1167360 bytes); any other exe is left alone.
static class GameMemory
{
    const int Music = 0x5B84B0;      // int32, 0-200
    const int Sfx = 0x6005C8;        // int32, 16.16 fixed point (0-200 in the high word)
    const int Voice = 0x5BDB41;      // byte, 0-200
    const int Subs = 0x5F4DF1;       // byte: 0 voice, 1 both, 2 text

    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out IntPtr read);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out IntPtr written);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static int Read(IntPtr h, int addr, int size)
    {
        byte[] b = new byte[4]; IntPtr n;
        if (!ReadProcessMemory(h, (IntPtr)addr, b, size, out n)) return int.MinValue;
        return BitConverter.ToInt32(b, 0);
    }

    static bool Write(IntPtr h, int addr, int value, int size)
    {
        IntPtr n; return WriteProcessMemory(h, (IntPtr)addr, BitConverter.GetBytes(value), size, out n);
    }

    public static void ApplyAndFollow(Process game, Settings s)
    {
        if (new FileInfo(Program.GameExe).Length != 1167360) { Program.Log("sound sliders: unknown monsters.exe, not touched"); game.WaitForExit(); return; }
        IntPtr h = OpenProcess(0x0010 | 0x0020 | 0x0008 | 0x0400, false, game.Id);
        if (h == IntPtr.Zero) { Program.Log("sound sliders: cannot open the game"); game.WaitForExit(); return; }
        try
        {
            // wait for the game's own defaults (music 80, sfx 100, voice 200), at most 60 s
            bool ready = false;
            for (int i = 0; i < 300 && !game.HasExited && !ready; i++)
            {
                ready = Read(h, Music, 4) == 80 && Read(h, Sfx, 4) == 100 << 16 && Read(h, Voice, 1) == 200;
                if (!ready) Thread.Sleep(200);
            }
            if (!ready) { Program.Log("sound sliders: game defaults not seen, not touched"); game.WaitForExit(); return; }
            bool ok = Write(h, Music, s.Music * 2, 4) & Write(h, Sfx, (s.Sfx * 2) << 16, 4) & Write(h, Voice, s.Voice * 2, 1) & Write(h, Subs, s.Subtitles, 1);
            Program.Log(string.Format("sound sliders written ({0}): music={1} sfx={2} voice={3} subtitles={4}", ok ? "ok" : "FAILED", s.Music, s.Sfx, s.Voice, s.Subtitles));
            while (!game.WaitForExit(1000))
            {
                int m = Read(h, Music, 4), f = Read(h, Sfx, 4), v = Read(h, Voice, 1), t = Read(h, Subs, 1);
                if (m < 0 || m > 200 || f < 0 || (f >> 16) > 200 || v < 0 || v > 200 || t < 0 || t > 2) continue;
                s.Music = (m + 1) / 2; s.Sfx = ((f >> 16) + 1) / 2; s.Voice = (v + 1) / 2; s.Subtitles = t;
            }
        }
        finally { CloseHandle(h); }
    }
}

// The start window: the original "Inizio del gioco" artwork (FX 2004), cleaned up and scaled for today's screens.
// Trailer buttons removed (the trailers are not installed); "Rimuovi" became "Options". Hover and press highlights.
class ArtForm : Form
{
    public static string ArtFile { get { return Path.Combine(Program.Dir, "launcher-art.png"); } }
    public bool PlayChosen;
    readonly Settings s;
    readonly Bitmap art;
    readonly float scale;
    // name, box in the original 510 x 418 artwork
    static readonly string[] Names = { "options", "play", "quit", "manual" };
    static readonly Rectangle[] Boxes = {
        new Rectangle(14, 378, 137, 33), new Rectangle(165, 378, 181, 33),
        new Rectangle(360, 378, 137, 33), new Rectangle(358, 325, 139, 34) };
    int hover = -1, pressed = -1;
    readonly System.Windows.Forms.Timer fade = new System.Windows.Forms.Timer();

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

    public ArtForm(Settings settings)
    {
        s = settings;
        Bitmap src;
        using (Bitmap file = new Bitmap(ArtFile)) src = new Bitmap(file);
        Clean(src);
        Rectangle screen = Screen.PrimaryScreen.WorkingArea;
        scale = Math.Max(1f, Math.Min(2f, (float)Math.Floor(screen.Height * 0.6f / src.Height * 4) / 4));
        art = new Bitmap((int)(src.Width * scale), (int)(src.Height * scale));
        using (Graphics g = Graphics.FromImage(art))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, art.Width, art.Height));
        }
        src.Dispose();
        Text = "Hollywood Monsters";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = art.Size;
        DoubleBuffered = true;
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Program.GameExe); } catch { }
        Opacity = 0;
        fade.Interval = 15;
        fade.Tick += delegate { Opacity = Math.Min(1, Opacity + 0.08); if (Opacity >= 1) fade.Stop(); };
        Shown += delegate { fade.Start(); };
        KeyDown += (o, e) => { if (e.KeyCode == Keys.Enter) Do(1); else if (e.KeyCode == Keys.Escape) Do(2); };
    }

    // Paint over what does not work any more, using the pixels around it.
    static void Clean(Bitmap b)
    {
        foreach (Rectangle r in new[] { new Rectangle(26, 327, 112, 27), new Rectangle(199, 327, 112, 27) })
            for (int y = r.Top; y < r.Bottom; y++)
            {
                Color a = b.GetPixel(r.Left - 2, y), c = b.GetPixel(r.Right + 1, y);
                for (int x = r.Left; x < r.Right; x++)
                {
                    float t = (x - r.Left) / (float)(r.Width - 1);
                    b.SetPixel(x, y, Color.FromArgb((int)(a.R + (c.R - a.R) * t), (int)(a.G + (c.G - a.G) * t), (int)(a.B + (c.B - a.B) * t)));
                }
            }
        // "Rimuovi" -> "Options": copy the clean part of the button over the old icon and text, then write the new label
        Rectangle btn = Boxes[0];
        for (int y = btn.Top + 3; y < btn.Bottom - 3; y++)
        {
            Color c = b.GetPixel(btn.Right - 8, y);
            for (int x = btn.Left + 3; x < btn.Right - 8; x++) b.SetPixel(x, y, c);
        }
        Rectangle gioca = Boxes[1];
        using (Graphics g = Graphics.FromImage(b))
        {
            // reuse the icon frame of the "Gioca" button, emptied, as the frame for a gear
            Rectangle iconSrc = new Rectangle(gioca.Left + 2, gioca.Top + 2, 31, 29);
            Bitmap frame = b.Clone(iconSrc, b.PixelFormat);
            Color inside = frame.GetPixel(4, frame.Height / 2);
            using (Graphics fg = Graphics.FromImage(frame)) using (SolidBrush br = new SolidBrush(inside)) fg.FillRectangle(br, 4, 4, frame.Width - 8, frame.Height - 8);
            g.DrawImage(frame, btn.Left + 2, btn.Top + 2);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (Font gear = new Font("Segoe UI Symbol", 20f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("\u2699", gear, Brushes.Black, new RectangleF(btn.Left + 2, btn.Top + 2, 31, 29), c);
            using (Font f = new Font("Arial", 13f, FontStyle.Italic, GraphicsUnit.Pixel))
            using (SolidBrush ink = new SolidBrush(Color.Black))
            using (StringFormat c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("Options", f, ink, new RectangleF(btn.Left + 34, btn.Top, btn.Width - 40, btn.Height), c);
        }
    }

    Rectangle Scaled(int i)
    {
        Rectangle r = Boxes[i];
        return new Rectangle((int)(r.X * scale), (int)(r.Y * scale), (int)(r.Width * scale), (int)(r.Height * scale));
    }

    int Hit(Point p) { for (int i = 0; i < Boxes.Length; i++) if (Scaled(i).Contains(p)) return i; return -1; }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(art, 0, 0);
        int i = pressed >= 0 ? pressed : hover;
        if (i < 0) return;
        Rectangle r = Scaled(i);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (System.Drawing.Drawing2D.GraphicsPath path = Round(r, (int)(6 * scale)))
        {
            using (SolidBrush b = new SolidBrush(pressed >= 0 ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(45, 255, 255, 255))) e.Graphics.FillPath(b, path);
            using (Pen glow = new Pen(Color.FromArgb(pressed >= 0 ? 120 : 200, 255, 200, 40), 2f * scale)) e.Graphics.DrawPath(glow, path);
        }
    }

    static System.Drawing.Drawing2D.GraphicsPath Round(Rectangle r, int d)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { hover = -1; pressed = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        int h = Hit(e.Location);
        if (h >= 0) { pressed = h; Invalidate(); return; }
        ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0);   // drag the window from anywhere else
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        int p = pressed; pressed = -1; Invalidate();
        if (p >= 0 && Hit(e.Location) == p) Do(p);
    }

    void Do(int i)
    {
        if (i == 0)
        {
            using (LauncherForm f = new LauncherForm(s, true))
                { f.ShowDialog(this); if (f.PlayChosen) s.Save(Program.SettingsFile); }
        }
        else if (i == 1) { PlayChosen = true; Close(); }
        else if (i == 2) Close();
        else
        {
            string pdf = Path.Combine(Program.Dir, "MVM04_010_manual.pdf");
            try { Process.Start(new ProcessStartInfo(pdf) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, "The manual could not be opened:\n" + ex.Message, "Hollywood Monsters"); }
        }
    }
}

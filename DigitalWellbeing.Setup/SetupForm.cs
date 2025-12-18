using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DigitalWellbeing.Setup
{
    public class SetupForm : Form
    {
        private Label statusLabel;
        private ProgressBar progressBar;
        private Button btnInstall;
        private Button btnRepair;
        private Button btnUninstall;
        private string[] _args;

        private const string AppName = "DigitalWellbeing";
        private const string PublisherName = "David Cepero";
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;

        public SetupForm(string[] args)
        {
            _args = args;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "DigitalWellbeing Setup";
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            statusLabel = new Label();
            statusLabel.Text = "Checking system...";
            statusLabel.Location = new Point(20, 20);
            statusLabel.AutoSize = true;
            this.Controls.Add(statusLabel);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(20, 50);
            progressBar.Size = new Size(340, 30);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = false; 
            this.Controls.Add(progressBar);

            // Buttons
            int btnY = 100;
            int btnHeight = 30;
            int btnWidth = 100;

            btnInstall = new Button();
            btnInstall.Text = "Install";
            btnInstall.Location = new Point(20, btnY);
            btnInstall.Size = new Size(btnWidth, btnHeight);
            btnInstall.Click += async (s, e) => await StartOperation(RunInstall, "Installing...");
            this.Controls.Add(btnInstall);

            btnRepair = new Button();
            btnRepair.Text = "Repair";
            btnRepair.Location = new Point(20, btnY);
            btnRepair.Size = new Size(btnWidth, btnHeight);
            btnRepair.Click += async (s, e) => await StartOperation(RunInstall, "Repairing...");
            this.Controls.Add(btnRepair);

            btnUninstall = new Button();
            btnUninstall.Text = "Uninstall";
            btnUninstall.Location = new Point(130, btnY);
            btnUninstall.Size = new Size(btnWidth, btnHeight);
            btnUninstall.Click += async (s, e) => await StartOperation(RunUninstall, "Uninstalling...");
            this.Controls.Add(btnUninstall);

            this.Load += SetupForm_Load;
        }

        private async void SetupForm_Load(object sender, EventArgs e)
        {
            try
            {
                if (_args != null && _args.Contains("/uninstall"))
                {
                    this.Text = "DigitalWellbeing Uninstall";
                    ToggleButtons(false);
                    progressBar.Visible = true;
                    await RunUninstall();
                }
                else
                {
                    CheckInstallationState();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void CheckInstallationState()
        {
            bool isInstalled = IsAppInstalled();

            if (isInstalled)
            {
                statusLabel.Text = "DigitalWellbeing is currently installed.";
                btnInstall.Visible = false;
                btnRepair.Visible = true;
                btnUninstall.Visible = true;
            }
            else
            {
                statusLabel.Text = "Ready to install DigitalWellbeing.";
                btnInstall.Visible = true;
                btnRepair.Visible = false;
                btnUninstall.Visible = false;
            }
        }

        private bool IsAppInstalled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                return key != null;
            }
        }

        private async Task StartOperation(Func<Task> operation, string statusText)
        {
            ToggleButtons(false);
            progressBar.Visible = true;
            statusLabel.Text = statusText;

            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                ShowError(ex);
                // Reset UI on error if we didn't exit
                progressBar.Visible = false;
                CheckInstallationState(); 
            }
        }

        private void ToggleButtons(bool enabled)
        {
            btnInstall.Enabled = enabled;
            btnRepair.Enabled = enabled;
            btnUninstall.Enabled = enabled;
        }

        private async Task RunInstall()
        {
            string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            string exePath = Path.Combine(installPath, "DigitalWellbeingWinUI3.exe");
            string uninstallExePath = Path.Combine(installPath, "Uninstall.exe");

            UpdateStatus("Stopping application...");
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingWinUI3")) { try { proc.Kill(); } catch { } }
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingService")) { try { proc.Kill(); } catch { } }
            await Task.Delay(1000);

            UpdateStatus("Extracting files...");
            await Task.Run(() => 
            {
                // Try clean install, but don't fail if we can't delete (we will overwrite)
                if (Directory.Exists(installPath))
                {
                    try { Directory.Delete(installPath, true); } catch { }
                }
                Directory.CreateDirectory(installPath);

                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("DigitalWellbeing_Portable.zip"));

                if (string.IsNullOrEmpty(resourceName)) throw new Exception("Embedded zip resource not found!");

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (ZipArchive archive = new ZipArchive(stream))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(installPath, entry.FullName);
                        string destinationDirectory = Path.GetDirectoryName(destinationPath);

                        if (!Directory.Exists(destinationDirectory))
                            Directory.CreateDirectory(destinationDirectory);

                        // If it's a directory entry, ignore (created above)
                        if (entry.Name == "") continue;

                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                // Only copy if we are not running from the target location (self-copy)
                if (!string.Equals(currentExe, uninstallExePath, StringComparison.OrdinalIgnoreCase))
                {
                     try { File.Copy(currentExe, uninstallExePath, true); } catch { }
                }
            });

            UpdateStatus("Creating shortcuts...");
            await Task.Run(() =>
            {
                // UI Shortcuts
                CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing.lnk", exePath, installPath);
                CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DigitalWellbeing.lnk", exePath, installPath);
                
                // Service Shortcut (Critical for data logging)
                string serviceExePath = Path.Combine(installPath, "DigitalWellbeingService.exe");
                CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing Service.lnk", serviceExePath, installPath);

                string infoPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);
                if (!Directory.Exists(infoPrograms)) Directory.CreateDirectory(infoPrograms);
                CreateShortcut(infoPrograms, "DigitalWellbeing.lnk", exePath, installPath);
            });

            UpdateStatus("Registering app...");
            RegisterUninstaller(installPath, uninstallExePath);

            UpdateStatus("Starting application...");
            
            // Start Service
            string servicePath = Path.Combine(installPath, "DigitalWellbeingService.exe");
            if (File.Exists(servicePath))
            {
                ProcessStartInfo serviceInfo = new ProcessStartInfo(servicePath);
                serviceInfo.WorkingDirectory = Path.GetDirectoryName(servicePath);
                // serviceInfo.WindowStyle = ProcessWindowStyle.Hidden; // WinExe handles this automatically but explicit doesn't hurt
                Process.Start(serviceInfo);
            }

            // Start UI
            if (File.Exists(exePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(exePath);
                startInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
                Process.Start(startInfo);
            }
            else
            {
                MessageBox.Show($"Executable not found at: {exePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            await Task.Delay(1000);
            Application.Exit();
        }

        private async Task RunUninstall()
        {
            UpdateStatus("Stopping application...");
            
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingWinUI3")) { try { proc.Kill(); } catch { } }
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingService")) { try { proc.Kill(); } catch { } }

            await Task.Delay(2000);

            UpdateStatus("Removing shortcuts...");
            string startupLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing.lnk");
            string startupServiceLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing Service.lnk");
            string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DigitalWellbeing.lnk");
            
            if (File.Exists(startupLnk)) File.Delete(startupLnk);
            if (File.Exists(startupServiceLnk)) File.Delete(startupServiceLnk);
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);
            
            string infoPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);
            if (Directory.Exists(infoPrograms)) Directory.Delete(infoPrograms, true);

            UpdateStatus("Unregistering...");
            RemoveUninstallerRegistry();

            UpdateStatus("Removing files...");
            string installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "cmd.exe";
            info.Arguments = $"/C ping 1.1.1.1 -n 1 -w 3000 > Nul & rmdir /S /Q \"{installPath}\"";
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.CreateNoWindow = true;
            Process.Start(info);

            Application.Exit();
        }

        private void RegisterUninstaller(string installLocation, string uninstallExePath)
        {
            try
            {
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    if (parent == null) return;

                    using (RegistryKey key = parent.CreateSubKey(AppName))
                    {
                        key.SetValue("DisplayName", AppName);
                        key.SetValue("ApplicationVersion", "0.8.0.0");
                        key.SetValue("Publisher", PublisherName);
                        key.SetValue("DisplayIcon", Path.Combine(installLocation, "DigitalWellbeingWinUI3.exe"));
                        key.SetValue("DisplayVersion", "0.8.0.0");
                        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                        key.SetValue("UninstallString", $"\"{uninstallExePath}\" /uninstall");
                        key.SetValue("QuietUninstallString", $"\"{uninstallExePath}\" /uninstall");
                        key.SetValue("InstallLocation", installLocation);
                        key.SetValue("NoModify", 1);
                        key.SetValue("NoRepair", 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registry Error: {ex.Message}");
            }
        }

        private void RemoveUninstallerRegistry()
        {
            try
            {
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    if (parent == null) return;
                    parent.DeleteSubKeyTree(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registry Error: {ex.Message}");
            }
        }

        private void UpdateStatus(string text)
        {
            if (this.InvokeRequired) this.Invoke(new Action<string>(UpdateStatus), text);
            else statusLabel.Text = text;
        }

        private void ShowError(Exception ex)
        {
            MessageBox.Show($"Operation Failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void CreateShortcut(string folder, string name, string target, string workDir)
        {
            try
            {
                string lnkPath = Path.Combine(folder, name);
                Type type = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(type);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = target;
                shortcut.WorkingDirectory = workDir;
                shortcut.Save();
            }
            catch { }
        }
    }
}

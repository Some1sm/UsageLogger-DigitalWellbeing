using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
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

        // Install scope UI
        private RadioButton rbDeviceInstall;
        private RadioButton rbUserInstall;
        private TextBox txtInstallPath;
        private Button btnBrowse;
        private Label lblInstallPath;

        private const string AppName = "DigitalWellbeing";
        private const string PublisherName = "David Cepero";
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;

        private bool IsDeviceInstall => rbDeviceInstall.Checked;
        
        private string DefaultDevicePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);
        private string DefaultUserPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

        public SetupForm(string[] args)
        {
            _args = args;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "DigitalWellbeing Setup";
            this.Size = new Size(450, 280);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            statusLabel = new Label();
            statusLabel.Text = "Choose installation type:";
            statusLabel.Location = new Point(20, 15);
            statusLabel.AutoSize = true;
            this.Controls.Add(statusLabel);

            // Install scope radio buttons
            rbDeviceInstall = new RadioButton();
            rbDeviceInstall.Text = "Install for all users (recommended)";
            rbDeviceInstall.Location = new Point(20, 40);
            rbDeviceInstall.AutoSize = true;
            rbDeviceInstall.Checked = true;
            rbDeviceInstall.CheckedChanged += InstallScope_Changed;
            this.Controls.Add(rbDeviceInstall);

            rbUserInstall = new RadioButton();
            rbUserInstall.Text = "Install for current user only";
            rbUserInstall.Location = new Point(20, 65);
            rbUserInstall.AutoSize = true;
            rbUserInstall.CheckedChanged += InstallScope_Changed;
            this.Controls.Add(rbUserInstall);

            // Install path
            lblInstallPath = new Label();
            lblInstallPath.Text = "Install location:";
            lblInstallPath.Location = new Point(20, 95);
            lblInstallPath.AutoSize = true;
            this.Controls.Add(lblInstallPath);

            txtInstallPath = new TextBox();
            txtInstallPath.Location = new Point(20, 115);
            txtInstallPath.Size = new Size(320, 25);
            txtInstallPath.Text = DefaultDevicePath;
            this.Controls.Add(txtInstallPath);

            btnBrowse = new Button();
            btnBrowse.Text = "Browse...";
            btnBrowse.Location = new Point(345, 113);
            btnBrowse.Size = new Size(80, 25);
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(20, 150);
            progressBar.Size = new Size(405, 25);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = false; 
            this.Controls.Add(progressBar);

            // Buttons
            int btnY = 190;
            int btnHeight = 35;
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

        private void InstallScope_Changed(object sender, EventArgs e)
        {
            if (rbDeviceInstall.Checked)
                txtInstallPath.Text = DefaultDevicePath;
            else
                txtInstallPath.Text = DefaultUserPath;
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select installation folder";
                fbd.SelectedPath = txtInstallPath.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtInstallPath.Text = fbd.SelectedPath;
                }
            }
        }

        private async void SetupForm_Load(object sender, EventArgs e)
        {
            try
            {
                if (_args != null && _args.Contains("/uninstall"))
                {
                    this.Text = "DigitalWellbeing Uninstall";
                    HideInstallOptions();
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

        private void HideInstallOptions()
        {
            rbDeviceInstall.Visible = false;
            rbUserInstall.Visible = false;
            txtInstallPath.Visible = false;
            btnBrowse.Visible = false;
            lblInstallPath.Visible = false;
        }

        private void CheckInstallationState()
        {
            bool isInstalled = IsAppInstalled(out string existingPath, out bool isUserInstall);

            if (isInstalled)
            {
                statusLabel.Text = "DigitalWellbeing is currently installed.";
                HideInstallOptions();
                btnInstall.Visible = false;
                
                // For user installs, don't allow modifications (repair)
                btnRepair.Visible = !isUserInstall;
                btnUninstall.Visible = true;
            }
            else
            {
                statusLabel.Text = "Choose installation type:";
                btnInstall.Visible = true;
                btnRepair.Visible = false;
                btnUninstall.Visible = false;
            }
        }

        private bool IsAppInstalled(out string installPath, out bool isUserInstall)
        {
            installPath = null;
            isUserInstall = false;

            // Check HKCU first (user install)
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    installPath = key.GetValue("InstallLocation") as string;
                    isUserInstall = true;
                    return true;
                }
            }

            // Check HKLM (device install)
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    installPath = key.GetValue("InstallLocation") as string;
                    isUserInstall = false;
                    return true;
                }
            }

            return false;
        }

        private bool IsAppInstalled()
        {
            return IsAppInstalled(out _, out _);
        }

        private bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void RestartAsAdmin()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.Arguments = "/device \"" + txtInstallPath.Text + "\"";
            
            try
            {
                Process.Start(startInfo);
                Application.Exit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC
                MessageBox.Show("Administrator privileges are required to install for all users.", "Elevation Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task StartOperation(Func<Task> operation, string statusText)
        {
            ToggleButtons(false);
            HideInstallOptions();
            progressBar.Visible = true;
            statusLabel.Text = statusText;

            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                ShowError(ex);
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
            string installPath = txtInstallPath.Text.Trim();
            bool isDeviceInstall = IsDeviceInstall;

            // Require elevation for device install
            if (isDeviceInstall && !IsRunningAsAdmin())
            {
                RestartAsAdmin();
                return;
            }

            string exePath = Path.Combine(installPath, "DigitalWellbeingWinUI3.exe");
            string uninstallExePath = Path.Combine(installPath, "Uninstall.exe");

            UpdateStatus("Stopping application...");
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingWinUI3")) { try { proc.Kill(); } catch { } }
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingService")) { try { proc.Kill(); } catch { } }
            await Task.Delay(1000);

            UpdateStatus("Extracting files...");
            await Task.Run(() => 
            {
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

                        if (entry.Name == "") continue;

                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.Equals(currentExe, uninstallExePath, StringComparison.OrdinalIgnoreCase))
                {
                     try { File.Copy(currentExe, uninstallExePath, true); } catch { }
                }
            });

            UpdateStatus("Creating shortcuts...");
            await Task.Run(() =>
            {
                // Choose appropriate folders based on install scope
                Environment.SpecialFolder startupFolder = isDeviceInstall ? Environment.SpecialFolder.CommonStartup : Environment.SpecialFolder.Startup;
                Environment.SpecialFolder desktopFolder = isDeviceInstall ? Environment.SpecialFolder.CommonDesktopDirectory : Environment.SpecialFolder.DesktopDirectory;
                Environment.SpecialFolder programsFolder = isDeviceInstall ? Environment.SpecialFolder.CommonPrograms : Environment.SpecialFolder.Programs;

                // Desktop shortcut for UI app
                CreateShortcut(Environment.GetFolderPath(desktopFolder), "DigitalWellbeing.lnk", exePath, installPath);
                
                // Service Startup Shortcut (background service always runs on startup)
                string serviceExePath = Path.Combine(installPath, "DigitalWellbeingService.exe");
                CreateShortcut(Environment.GetFolderPath(startupFolder), "DigitalWellbeing Service.lnk", serviceExePath, installPath);
                
                // NOTE: UI app startup is managed through Settings > "Run on Startup" toggle (uses registry)
                // This prevents duplicate startup entries

                string infoPrograms = Path.Combine(Environment.GetFolderPath(programsFolder), AppName);
                if (!Directory.Exists(infoPrograms)) Directory.CreateDirectory(infoPrograms);
                CreateShortcut(infoPrograms, "DigitalWellbeing.lnk", exePath, installPath);
            });

            UpdateStatus("Registering app...");
            RegisterUninstaller(installPath, uninstallExePath, isDeviceInstall);

            UpdateStatus("Starting application...");
            
            string servicePath = Path.Combine(installPath, "DigitalWellbeingService.exe");
            if (File.Exists(servicePath))
            {
                ProcessStartInfo serviceInfo = new ProcessStartInfo(servicePath);
                serviceInfo.WorkingDirectory = Path.GetDirectoryName(servicePath);
                Process.Start(serviceInfo);
            }

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

            // Determine install type from registry
            bool isDeviceInstall = false;
            string installPath = null;

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    isDeviceInstall = true;
                    installPath = key.GetValue("InstallLocation") as string;
                }
            }

            if (installPath == null)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        installPath = key.GetValue("InstallLocation") as string;
                    }
                }
            }

            if (installPath == null)
            {
                installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            }

            UpdateStatus("Removing shortcuts...");
            
            // Remove from both possible locations
            string[] startupPaths = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing Service.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "DigitalWellbeing.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "DigitalWellbeing Service.lnk")
            };

            string[] desktopPaths = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DigitalWellbeing.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "DigitalWellbeing.lnk")
            };

            foreach (var path in startupPaths.Concat(desktopPaths))
            {
                if (File.Exists(path)) try { File.Delete(path); } catch { }
            }
            
            string[] programPaths = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName)
            };

            foreach (var path in programPaths)
            {
                if (Directory.Exists(path)) try { Directory.Delete(path, true); } catch { }
            }

            UpdateStatus("Unregistering...");
            RemoveUninstallerRegistry(isDeviceInstall);

            UpdateStatus("Removing files...");

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "cmd.exe";
            info.Arguments = $"/C ping 1.1.1.1 -n 1 -w 3000 > Nul & rmdir /S /Q \"{installPath}\"";
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.CreateNoWindow = true;
            Process.Start(info);

            Application.Exit();
        }

        private void RegisterUninstaller(string installLocation, string uninstallExePath, bool isDeviceInstall)
        {
            try
            {
                RegistryKey root = isDeviceInstall ? Registry.LocalMachine : Registry.CurrentUser;
                
                using (RegistryKey parent = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
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

        private void RemoveUninstallerRegistry(bool isDeviceInstall)
        {
            try
            {
                // Try both locations
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    parent?.DeleteSubKeyTree(AppName, false);
                }

                using (RegistryKey parent = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    parent?.DeleteSubKeyTree(AppName, false);
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

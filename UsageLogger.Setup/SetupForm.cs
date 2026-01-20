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

namespace UsageLogger.Setup
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

        private const string AppName = "UsageLogger";
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
            this.Text = "UsageLogger Setup";
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
                    this.Text = "UsageLogger Uninstall";
                    HideInstallOptions();
                    ToggleButtons(false);
                    progressBar.Visible = true;
                    await RunUninstall();
                }
                else
                {
                    // Check if payload is present
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("UsageLogger_Portable.zip"));
                    bool hasPayload = !string.IsNullOrEmpty(resourceName);

                    CheckInstallationState();

                    if (!hasPayload)
                    {
                        btnInstall.Enabled = false;
                        btnRepair.Enabled = false;
                        statusLabel.Text += " (Installer payload missing)";
                    }
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
            bool isInstalled = IsAppInstalled(out string existingPath, out bool isUserInstall, out bool isLegacyInstall);

            if (isInstalled)
            {
                if (isLegacyInstall)
                {
                    statusLabel.Text = "DigitalWellbeing detected. Click Install to migrate to UsageLogger.";
                }
                else
                {
                    statusLabel.Text = "UsageLogger is currently installed.";
                }
                
                // Set the install path and scope to match existing installation (for repair/migrate)
                txtInstallPath.Text = existingPath ?? (isUserInstall ? DefaultUserPath : DefaultDevicePath);
                rbUserInstall.Checked = isUserInstall;
                rbDeviceInstall.Checked = !isUserInstall;
                
                HideInstallOptions();
                
                if (isLegacyInstall)
                {
                    // Show Install button for migration
                    btnInstall.Text = "Migrate";
                    btnInstall.Visible = true;
                    btnRepair.Visible = false;
                }
                else
                {
                    btnInstall.Visible = false;
                    btnRepair.Visible = true;
                }
                btnUninstall.Visible = true;
            }
            else
            {
                statusLabel.Text = "Choose installation type:";
                btnInstall.Text = "Install";
                btnInstall.Visible = true;
                btnRepair.Visible = false;
                btnUninstall.Visible = false;
            }
        }

        private const string LegacyRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DigitalWellbeing";

        private bool IsAppInstalled(out string installPath, out bool isUserInstall, out bool isLegacyInstall)
        {
            installPath = null;
            isUserInstall = false;
            isLegacyInstall = false;

            // Check for new UsageLogger install first
            // Check HKCU (user install)
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

            // Check for legacy DigitalWellbeing install
            // Check HKCU (user install)
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(LegacyRegistryKeyPath))
            {
                if (key != null)
                {
                    installPath = key.GetValue("InstallLocation") as string;
                    isUserInstall = true;
                    isLegacyInstall = true;
                    return true;
                }
            }

            // Check HKLM (device install)
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(LegacyRegistryKeyPath))
            {
                if (key != null)
                {
                    installPath = key.GetValue("InstallLocation") as string;
                    isUserInstall = false;
                    isLegacyInstall = true;
                    return true;
                }
            }

            return false;
        }

        private bool IsAppInstalled(out string installPath, out bool isUserInstall)
        {
            return IsAppInstalled(out installPath, out isUserInstall, out _);
        }

        private bool IsAppInstalled()
        {
            return IsAppInstalled(out _, out _, out _);
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

        private void RestartAsAdminForUninstall()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.Arguments = "/uninstall";
            
            try
            {
                Process.Start(startInfo);
                Application.Exit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC
                MessageBox.Show("Administrator privileges are required to uninstall a system-wide installation.", "Elevation Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            string exePath = Path.Combine(installPath, "UsageLogger.exe");
            string uninstallExePath = Path.Combine(installPath, "Uninstall.exe");

            UpdateStatus("Stopping application...");
            // Kill both new and legacy process names for migration
            foreach (var proc in Process.GetProcessesByName("UsageLogger")) { try { proc.Kill(); } catch { } }
            foreach (var proc in Process.GetProcessesByName("UsageLoggerService")) { try { proc.Kill(); } catch { } }
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingWinUI3")) { try { proc.Kill(); } catch { } }
            foreach (var proc in Process.GetProcessesByName("DigitalWellbeingService")) { try { proc.Kill(); } catch { } }
            await Task.Delay(1000);

            // Clean up legacy DigitalWellbeing installation
            UpdateStatus("Cleaning up legacy installation...");
            await Task.Run(() => CleanupLegacyInstallation());

            UpdateStatus("Extracting files...");
            await Task.Run(() => 
            {
                if (Directory.Exists(installPath))
                {
                    // SAFE CLEANUP: Preserve user data in target folder (in case of reinstall over data)
                    var preserveFolders = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                    { 
                        "dailylogs", "settings", "Icons", "Debug", "CustomIcons", "processicons", "UsageLoggerData" 
                    };
                    var preserveFiles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                    { 
                        "user_preferences.json", "custom_log_path.txt", "known_apps.json" 
                    };

                    try
                    {
                        foreach (var file in Directory.GetFiles(installPath))
                        {
                            string name = Path.GetFileName(file);
                            if (!preserveFiles.Contains(name))
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }

                        foreach (var dir in Directory.GetDirectories(installPath))
                        {
                            string name = Path.GetFileName(dir);
                            if (!preserveFolders.Contains(name))
                            {
                                try { Directory.Delete(dir, true); } catch { }
                            }
                        }
                    }
                    catch { }
                }
                Directory.CreateDirectory(installPath);

                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("UsageLogger_Portable.zip"));

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
                    // Only copy myself if Uninstall.exe doesn't already exist or I am the full installer
                    // This prevents overwriting a specialized small uninstaller (if we shipped one) with a large installer
                    if (!File.Exists(uninstallExePath))
                    {
                        try { File.Copy(currentExe, uninstallExePath, true); } catch { }
                    }
                }
            });

            UpdateStatus("Creating shortcuts...");
            await Task.Run(() =>
            {
                // Choose appropriate folders based on install scope
                Environment.SpecialFolder desktopFolder = isDeviceInstall ? Environment.SpecialFolder.CommonDesktopDirectory : Environment.SpecialFolder.DesktopDirectory;
                Environment.SpecialFolder programsFolder = isDeviceInstall ? Environment.SpecialFolder.CommonPrograms : Environment.SpecialFolder.Programs;

                // Desktop shortcut for UI app
                CreateShortcut(Environment.GetFolderPath(desktopFolder), "UsageLogger.lnk", exePath, installPath);
                
                // NOTE: Startup behavior is managed through Settings > "Run on Startup" toggle (uses registry)
                // We no longer create a hardcoded startup shortcut here to prevent duplicate entries
                // and allow the user to control startup behavior from the app settings

                string infoPrograms = Path.Combine(Environment.GetFolderPath(programsFolder), AppName);
                if (!Directory.Exists(infoPrograms)) Directory.CreateDirectory(infoPrograms);
                CreateShortcut(infoPrograms, "UsageLogger.lnk", exePath, installPath);
            });

            UpdateStatus("Registering app...");
            RegisterUninstaller(installPath, uninstallExePath, isDeviceInstall);

            UpdateStatus("Starting application...");
            
            string servicePath = Path.Combine(installPath, "UsageLoggerService.exe");
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
            // Check if device install first to determine if elevation is needed
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

            // Require elevation for device uninstall
            if (isDeviceInstall && !IsRunningAsAdmin())
            {
                RestartAsAdminForUninstall();
                return;
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

            UpdateStatus("Stopping application...");
            
            foreach (var proc in Process.GetProcessesByName("UsageLogger")) { try { proc.Kill(); } catch { } }
            foreach (var proc in Process.GetProcessesByName("UsageLoggerService")) { try { proc.Kill(); } catch { } }

            await Task.Delay(2000);

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
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "UsageLogger.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "UsageLogger Service.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "UsageLogger.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "UsageLogger Service.lnk")
            };

            string[] desktopPaths = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "UsageLogger.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "UsageLogger.lnk")
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

            UpdateStatus("Cleaning up files...");
            
            // Safe Cleanup: Delete known app files/folders, but PRESERVE user data
            if (Directory.Exists(installPath))
            {
                var preserveFolders = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                { 
                    "dailylogs", "settings", "Icons", "Debug", "CustomIcons", "processicons" 
                };
                var preserveFiles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                { 
                    "user_preferences.json", "custom_log_path.txt", "known_apps.json" 
                };

                try
                {
                    // 1. Files
                    foreach (var file in Directory.GetFiles(installPath))
                    {
                        string name = Path.GetFileName(file);
                        // Don't delete self yet
                        if (file.Equals(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) continue;
                        
                        if (!preserveFiles.Contains(name))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }

                    // 2. Directories (Recursive delete for non-preserved ones)
                    foreach (var dir in Directory.GetDirectories(installPath))
                    {
                        string name = Path.GetFileName(dir);
                        if (!preserveFolders.Contains(name))
                        {
                            try { Directory.Delete(dir, true); } catch { }
                        }
                    }
                }
                catch { }
            }

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "cmd.exe";
            // Self-delete execution and try to remove folder safely (will fail if data exists)
            // rmdir (without /S) only removes Empty directories.
            info.Arguments = $"/C ping 1.1.1.1 -n 1 -w 3000 > Nul & del /F /Q \"{Application.ExecutablePath}\" & rmdir \"{installPath}\"";
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
                        key.SetValue("ApplicationVersion", "0.8.1.0");
                        key.SetValue("Publisher", PublisherName);
                        key.SetValue("DisplayIcon", Path.Combine(installLocation, "UsageLogger.exe"));
                        key.SetValue("DisplayVersion", "0.8.1.0");
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
                // Remove both new and legacy registry keys
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    parent?.DeleteSubKeyTree(AppName, false);
                    parent?.DeleteSubKeyTree("DigitalWellbeing", false); // Legacy
                }

                using (RegistryKey parent = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    parent?.DeleteSubKeyTree(AppName, false);
                    parent?.DeleteSubKeyTree("DigitalWellbeing", false); // Legacy
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

        private void CleanupLegacyInstallation()
        {
            try
            {
                // Remove legacy shortcuts
                string[] legacyShortcuts = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing Service.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "DigitalWellbeing.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "DigitalWellbeing Service.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DigitalWellbeing.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "DigitalWellbeing.lnk"),
                };

                foreach (var path in legacyShortcuts)
                {
                    if (File.Exists(path)) try { File.Delete(path); } catch { }
                }

                // Remove legacy program menu folder
                string[] legacyProgramPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "DigitalWellbeing"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "DigitalWellbeing")
                };

                foreach (var path in legacyProgramPaths)
                {
                    if (Directory.Exists(path)) try { Directory.Delete(path, true); } catch { }
                }

                // Get legacy install path from registry before removing
                string legacyInstallPath = null;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(LegacyRegistryKeyPath))
                {
                    legacyInstallPath = key?.GetValue("InstallLocation") as string;
                }
                if (legacyInstallPath == null)
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(LegacyRegistryKeyPath))
                    {
                        legacyInstallPath = key?.GetValue("InstallLocation") as string;
                    }
                }

                // Remove legacy registry entry
                try
                {
                    using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                    {
                        parent?.DeleteSubKeyTree("DigitalWellbeing", false);
                    }
                    using (RegistryKey parent = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                    {
                        parent?.DeleteSubKeyTree("DigitalWellbeing", false);
                    }
                }
                catch { }

                // Remove legacy install folder (different from new path)
                if (!string.IsNullOrEmpty(legacyInstallPath) && Directory.Exists(legacyInstallPath))
                {
                    // Only delete if it's actually the legacy folder (contains old exe name)
                    string legacyExe = Path.Combine(legacyInstallPath, "DigitalWellbeingWinUI3.exe");
                    if (File.Exists(legacyExe))
                    {
                        // SAFETY CHECK: Do not delete if it contains ANY user data
                        bool hasLogs = Directory.Exists(Path.Combine(legacyInstallPath, "dailylogs")) && Directory.GetFiles(Path.Combine(legacyInstallPath, "dailylogs")).Length > 0;
                        bool hasSettings = Directory.Exists(Path.Combine(legacyInstallPath, "settings"));
                        bool hasPreferences = File.Exists(Path.Combine(legacyInstallPath, "user_preferences.json"));

                        if (hasLogs || hasSettings || hasPreferences)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Setup] Preserving legacy folder '{legacyInstallPath}' because it contains data/settings.");
                            // Only delete the executable to avoid confusion, but keep data
                            try { File.Delete(legacyExe); } catch { }
                        }
                        else
                        {
                            try { Directory.Delete(legacyInstallPath, true); } catch { }
                        }
                    }
                }
            }
            catch { }
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

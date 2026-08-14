#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using UsageLoggerService.Helpers;

namespace UsageLoggerService.UI
{
    public class QuickGlanceForm : Form
    {
        private static QuickGlanceForm? _currentInstance;

        private readonly TimeSpan _totalActive;
        private readonly List<(string AppName, TimeSpan Duration)> _topApps;
        private readonly bool _isFocusActive;
        private readonly string _focusName;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public QuickGlanceForm(TimeSpan totalActive, List<(string AppName, TimeSpan Duration)> topApps, bool isFocusActive, string focusName)
        {
            _totalActive = totalActive;
            _topApps = topApps;
            _isFocusActive = isFocusActive;
            _focusName = focusName;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(32, 32, 36);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Size = new Size(310, 260);

            // Close on losing focus
            this.Deactivate += (s, e) => this.Close();

            BuildUI();
        }

        public static void ShowQuickGlance()
        {
            if (_currentInstance != null && !_currentInstance.IsDisposed)
            {
                _currentInstance.Close();
                _currentInstance = null;
                return;
            }

            var summary = ActivityLogger.Instance?.GetTodaySummary() 
                          ?? (TimeSpan.Zero, new List<(string, TimeSpan)>(), false, "Focus Mode");

            _currentInstance = new QuickGlanceForm(summary.TotalActive, summary.TopApps, summary.IsFocusActive, summary.FocusName);
            
            // Position near cursor / taskbar notification area
            Point cursor = Cursor.Position;
            Screen screen = Screen.FromPoint(cursor);
            Rectangle workingArea = screen.WorkingArea;

            int x = cursor.X - _currentInstance.Width / 2;
            if (x + _currentInstance.Width > workingArea.Right - 10) x = workingArea.Right - _currentInstance.Width - 10;
            if (x < workingArea.Left + 10) x = workingArea.Left + 10;

            int y = cursor.Y - _currentInstance.Height - 15;
            if (y < workingArea.Top + 10) y = cursor.Y + 25; // Taskbar is at top

            _currentInstance.Location = new Point(x, y);
            _currentInstance.Show();
            SetForegroundWindow(_currentInstance.Handle);
        }

        private void BuildUI()
        {
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = Color.Transparent
            };

            // 1. Header (Title + Total Duration)
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 42 };
            
            var titleLabel = new Label
            {
                Text = "Today's Usage",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 240),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            string totalFormatted = $"{(int)_totalActive.TotalHours}h {_totalActive.Minutes}m";
            if (_totalActive.TotalHours < 1) totalFormatted = $"{_totalActive.Minutes}m";
            if (_totalActive.TotalSeconds < 60) totalFormatted = $"{_totalActive.Seconds}s";

            var totalLabel = new Label
            {
                Text = totalFormatted,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 160, 255),
                AutoSize = true,
                Location = new Point(0, 20)
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(totalLabel);

            // 2. Top Apps List
            var appsPanel = new Panel { Dock = DockStyle.Top, Height = 105, Padding = new Padding(0, 4, 0, 0) };
            
            if (_topApps.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = "No active usage recorded yet today.",
                    ForeColor = Color.FromArgb(160, 160, 170),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                appsPanel.Controls.Add(emptyLabel);
            }
            else
            {
                double maxSec = Math.Max(1, _topApps[0].Duration.TotalSeconds);
                int yOffset = 4;

                for (int i = 0; i < _topApps.Count; i++)
                {
                    var app = _topApps[i];
                    double pct = Math.Min(1.0, app.Duration.TotalSeconds / maxSec);
                    string durStr = $"{(int)app.Duration.TotalHours}h {app.Duration.Minutes}m";
                    if (app.Duration.TotalHours < 1) durStr = $"{app.Duration.Minutes}m";
                    if (app.Duration.TotalSeconds < 60) durStr = $"{app.Duration.Seconds}s";

                    var rowPanel = new Panel
                    {
                        Location = new Point(0, yOffset),
                        Size = new Size(278, 28),
                        BackColor = Color.Transparent
                    };

                    var nameLabel = new Label
                    {
                        Text = $"{i + 1}. {app.AppName}",
                        Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                        ForeColor = Color.FromArgb(220, 220, 230),
                        Location = new Point(0, 0),
                        Size = new Size(185, 16),
                        AutoEllipsis = true
                    };

                    var durLabel = new Label
                    {
                        Text = durStr,
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                        ForeColor = Color.FromArgb(170, 170, 180),
                        Location = new Point(190, 0),
                        Size = new Size(88, 16),
                        TextAlign = ContentAlignment.TopRight
                    };

                    // Progress bar indicator
                    var barBack = new Panel
                    {
                        Location = new Point(0, 18),
                        Size = new Size(278, 4),
                        BackColor = Color.FromArgb(50, 50, 56)
                    };

                    int fillWidth = Math.Max(4, (int)(278 * pct));
                    var barFill = new Panel
                    {
                        Location = new Point(0, 0),
                        Size = new Size(fillWidth, 4),
                        BackColor = i == 0 ? Color.FromArgb(80, 160, 255) : (i == 1 ? Color.FromArgb(100, 200, 140) : Color.FromArgb(250, 180, 70))
                    };
                    barBack.Controls.Add(barFill);

                    rowPanel.Controls.Add(nameLabel);
                    rowPanel.Controls.Add(durLabel);
                    rowPanel.Controls.Add(barBack);

                    appsPanel.Controls.Add(rowPanel);
                    yOffset += 32;
                }
            }

            // 3. Focus Mode Status Badge
            var focusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Margin = new Padding(0, 4, 0, 0),
                BackColor = _isFocusActive ? Color.FromArgb(45, 65, 50) : Color.FromArgb(40, 40, 46)
            };

            var focusLabel = new Label
            {
                Text = _isFocusActive ? $"Focus: {_focusName}" : "Focus Mode: Off",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = _isFocusActive ? Color.FromArgb(120, 230, 140) : Color.FromArgb(160, 160, 170),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };
            focusPanel.Controls.Add(focusLabel);

            // 4. Action Buttons Footer
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34
            };

            var btnOpen = new Button
            {
                Text = "Open App",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(60, 120, 220),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(132, 30),
                Location = new Point(0, 2),
                Cursor = Cursors.Hand
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) =>
            {
                this.Close();
                TrayManager.LaunchUI();
            };

            var btnExit = new Button
            {
                Text = "Exit Service",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                BackColor = Color.FromArgb(45, 45, 52),
                ForeColor = Color.FromArgb(200, 200, 210),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(138, 30),
                Location = new Point(140, 2),
                Cursor = Cursors.Hand
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Click += (s, e) =>
            {
                this.Close();
                TrayManager.ExitAll();
            };

            buttonPanel.Controls.Add(btnOpen);
            buttonPanel.Controls.Add(btnExit);

            mainPanel.Controls.Add(buttonPanel);
            mainPanel.Controls.Add(focusPanel);
            mainPanel.Controls.Add(appsPanel);
            mainPanel.Controls.Add(headerPanel);

            this.Controls.Add(mainPanel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw a subtle border around the entire form
            using var pen = new Pen(Color.FromArgb(70, 70, 80), 1.5f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
        }
    }
}

using System;
using System.Threading;
using System.Windows.Forms;

namespace DigitalWellbeingService.UI
{
    /// <summary>
    /// Simple enforcement popup using MessageBox.
    /// Appears over all windows and blocks until dismissed.
    /// </summary>
    public static class EnforcementPopup
    {
        /// <summary>
        /// Shows a MessageBox enforcement dialog on a new STA thread.
        /// Returns true if user clicked "Cancel" (Disable Session).
        /// </summary>
        public static bool ShowPopup(string sessionName, string targetApp, string endTime)
        {
            bool disableClicked = false;
            
            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                
                string message = $"Focus Session Active!\n\n" +
                               $"You should be using '{targetApp}' right now.\n\n" +
                               $"Session: {sessionName}\n" +
                               $"Until: {endTime}\n\n" +
                               $"Click 'Cancel' to disable this session.";
                
                var result = MessageBox.Show(
                    message,
                    "Focus Session Active",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.DefaultDesktopOnly
                );
                
                disableClicked = (result == DialogResult.Cancel);
            });
            
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            
            return disableClicked;
        }
    }
}

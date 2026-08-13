#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using UsageLogger.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace UsageLogger.Views
{
    public sealed partial class VisualReportDialog : ContentDialog
    {
        private InMemoryRandomAccessStream? _currentImageStream;
        private byte[]? _imageBytes;
        private DateTime _reportDate;

        public VisualReportDialog()
        {
            this.InitializeComponent();
            this.PrimaryButtonClick += CopyButton_Click;
            this.SecondaryButtonClick += SaveButton_Click;
        }

        public async Task InitializeReportAsync(DateTime referenceDate)
        {
            _reportDate = referenceDate;
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            ReportImagePreview.Visibility = Visibility.Collapsed;

            try
            {
                var data = await VisualReportGenerator.CompileWeeklyDataAsync(referenceDate);
                _currentImageStream = await VisualReportGenerator.GenerateReportImageStreamAsync(data);

                // Read bytes for saving / copying
                using (var reader = new DataReader(_currentImageStream.GetInputStreamAt(0)))
                {
                    await reader.LoadAsync((uint)_currentImageStream.Size);
                    _imageBytes = new byte[_currentImageStream.Size];
                    reader.ReadBytes(_imageBytes);
                }

                // Render in preview
                _currentImageStream.Seek(0);
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(_currentImageStream);
                ReportImagePreview.Source = bitmap;

                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
                ReportImagePreview.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
                ReportInfoBar.Severity = InfoBarSeverity.Error;
                ReportInfoBar.Message = $"Failed to generate report: {ex.Message}";
                ReportInfoBar.IsOpen = true;
            }
        }

        private void CopyButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // Keep dialog open

            if (_currentImageStream == null) return;

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.RequestedOperation = DataPackageOperation.Copy;
                
                _currentImageStream.Seek(0);
                var streamRef = RandomAccessStreamReference.CreateFromStream(_currentImageStream);
                dataPackage.SetBitmap(streamRef);
                Clipboard.SetContent(dataPackage);

                ReportInfoBar.Severity = InfoBarSeverity.Success;
                ReportInfoBar.Message = "Infographic copied to clipboard! You can now paste it into Discord, Slack, or any document.";
                ReportInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                ReportInfoBar.Severity = InfoBarSeverity.Error;
                ReportInfoBar.Message = $"Copy failed: {ex.Message}";
                ReportInfoBar.IsOpen = true;
            }
        }

        private async void SaveButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // Keep dialog open

            if (_imageBytes == null || _imageBytes.Length == 0) return;

            try
            {
                var picker = new FileSavePicker();
                
                if (App.Current is App app && app.m_window is MainWindow window)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    if (hwnd != IntPtr.Zero)
                    {
                        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                    }
                }

                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeChoices.Add("PNG Image", new string[] { ".png" });
                picker.SuggestedFileName = $"UsageLogger_WeeklyReport_{_reportDate:yyyy-MM-dd}.png";

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await Windows.Storage.FileIO.WriteBytesAsync(file, _imageBytes);
                    ReportInfoBar.Severity = InfoBarSeverity.Success;
                    ReportInfoBar.Message = $"Report saved to: {file.Path}";
                    ReportInfoBar.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                ReportInfoBar.Severity = InfoBarSeverity.Error;
                ReportInfoBar.Message = $"Save failed: {ex.Message}";
                ReportInfoBar.IsOpen = true;
            }
        }
    }
}

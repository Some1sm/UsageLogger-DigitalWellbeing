using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.Models;
using DigitalWellbeingWinUI3.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;


namespace DigitalWellbeingWinUI3.ViewModels
{
    public class DayTimelineViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        public DateTime Date { get; private set; }
        public string DateString => Date.ToString("D"); // Full date format

        private double _timelineWidth;
        public double TimelineWidth
        {
            get => _timelineWidth;
            set
            {
                if (_timelineWidth != value)
                {
                    _timelineWidth = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ContentWidth)); // Notify derived property
                    UpdateLayoutWidths();
                }
            }
        }

        public double ContentWidth => TimelineWidth - 30.0 > 0 ? TimelineWidth - 30.0 : 0;

        private double _canvasHeight = 1440; // Default 24h
        public double CanvasHeight
        {
            get => _canvasHeight;
            set { _canvasHeight = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TimeGridLine> GridLines { get; set; } = new ObservableCollection<TimeGridLine>();
        public ObservableCollection<SessionBlock> SessionBlocks { get; set; } = new ObservableCollection<SessionBlock>();

        // Current Time Indicator (Red Line) - Only relevant if Today
        private bool _isToday;
        public bool IsToday
        {
            get => _isToday;
            set { _isToday = value; OnPropertyChanged(); UpdateCurrentTimeVisibility(); }
        }

        private Visibility _currentTimeVisibility = Visibility.Collapsed;
        public Visibility CurrentTimeVisibility
        {
            get => _currentTimeVisibility;
            set { _currentTimeVisibility = value; OnPropertyChanged(); }
        }

        private double _currentTimeTop;
        public double CurrentTimeTop
        {
            get => _currentTimeTop;
            set { _currentTimeTop = value; OnPropertyChanged(); }
        }

        // View Mode: "App", "SubApp", or "Category"
        private string _viewMode = "SubApp";
        public string ViewMode
        {
            get => _viewMode;
            set { _viewMode = value; OnPropertyChanged(); }
        }

        public DayTimelineViewModel(DateTime date)
        {
            Date = date;
            IsToday = Date.Date == DateTime.Now.Date;
            // Initialize with default zoom
            RefreshGridLines(60.0);
        }

        public void SetZoom(double pixelsPerHour)
        {
            CanvasHeight = pixelsPerHour * 24;
            RefreshGridLines(pixelsPerHour);
            
            if (_cachedSessions != null)
            {
                RefreshSessionLayout(pixelsPerHour);
            }
            UpdateCurrentTime(pixelsPerHour);
        }

        private List<AppSession> _cachedSessions;

        public void LoadSessions(List<AppSession> sessions, double pixelsPerHour)
        {
            // RETROACTIVE RULE APPLICATION:
            if (UserPreferences.CustomTitleRules != null && UserPreferences.CustomTitleRules.Count > 0)
            {
                foreach (var s in sessions)
                {
                    s.ProgramName = DigitalWellbeing.Core.Helpers.WindowTitleParser.Parse(
                        s.ProcessName, 
                        s.ProgramName, 
                        UserPreferences.CustomTitleRules
                    );
                }
            }

            _cachedSessions = sessions;
            RefreshSessionLayout(pixelsPerHour);
            UpdateCurrentTime(pixelsPerHour);
        }

        public void RefreshSessionLayout(double pixelsPerHour)
        {
            if (_cachedSessions == null) return;

            var newBlocks = new ObservableCollection<SessionBlock>();
            // Always group by ProcessName to allow merging across different window titles (e.g., song changes)
            // This fixes fragmentation for apps like YouTube Music where each song creates a new session
            // NEW LINEAR PROCESSING LOGIC (Fixes Ghosting/Overlaps)
            // 1. Flatten all sessions into a single list
            var allSessions = _cachedSessions.OrderBy(s => s.StartTime).ToList();
            
            DateTime? globalLastEnd = null;
            var mergedBlocks = new List<SessionBlock>();
            SessionBlock pendingBlock = null;
            DateTime? lastEnd = null;

            foreach (var s in allSessions)
            {
                DateTime dayStart = Date.Date;
                DateTime dayEnd = dayStart.AddDays(1);
                
                DateTime validStart = s.StartTime < dayStart ? dayStart : s.StartTime;
                DateTime validEnd = s.EndTime > dayEnd ? dayEnd : s.EndTime;
                
                // DE-OVERLAP LOGIC:
                // If this session starts before the last one ended, we have a conflict.
                // WINNER-TAKES-ALL: The new session (s) takes priority.
                // We must TRIM the previous block to end at validStart.
                
                if (globalLastEnd.HasValue && validStart < globalLastEnd.Value)
                {
                    // Overlap detected!
                    // 1. Find the last block(s) that overlap
                    // Since specific previous blocks might have ended earlier, we check the tail of our list
                    // But actually, simpler: Ensure current validStart is strictly >= globalLastEnd?
                    // No, that would trim the CURRENT session. We want to trim the PREVIOUS session.
                   
                    // However, we've already finalized pendingBlock into mergedBlocks or it's currently pending.
                    // The easiest way to handle this in a single pass is to strictly ENFORCE
                    // that validStart >= globalLastEnd by shifting validStart (clipping current), 
                    // OR by going back and modifying the previous block.
                    
                    // User preference: "It's not possible for active apps to overlap".
                    // Usually the Log says: App A 13:00-13:05. App B 13:04-13:06.
                    // Real truth is probably App A ended at 13:04.
                    
                    // So: TRIM PREVIOUS.
                    
                    // Check Pending Block
                    if (pendingBlock != null)
                    {
                        // Calculate its end time in minutes from TOP/HEIGHT
                        // Top = startMin * PPH / 60
                        // Height = durationMin * PPH / 60
                        // EndMin = Top + Height * (60/PPH)
                        // This is expensive to reverse calc.
                        // Better to rely on 'lastEnd' variable but we need to update pendingBlock.Height
                        
                        if (lastEnd.HasValue && lastEnd.Value > validStart)
                        {
                            // Reduce pending block
                            double newDurationMin = (validStart - lastEnd.Value.AddMinutes(-(lastEnd.Value - dayStart).TotalMinutes + pendingBlock.Top * 60 / pixelsPerHour)).TotalMinutes; 
                            // Wait, simpler:
                            // We know pending block ends at 'lastEnd'.
                            // We want it to end at 'validStart'.
                             
                            double reductionSeconds = (lastEnd.Value - validStart).TotalSeconds;
                            if (reductionSeconds > 0)
                            {
                                // Recalculate Height
                                double oldDurationMin = (lastEnd.Value - dayStart).TotalMinutes - (pendingBlock.Top * 60.0 / pixelsPerHour);
                                // The math above is getting complex because of referencing Top/DayStart.
                                // Let's track StartTime in the block model for easier calc? 
                                // Or references.
                                
                                // Simplified approach: 
                                // If we assume the timestamps in the logs are truthy for "START", then previous APPS must end when new ones START.
                                // Force 'validStart' to be the hard boundary.
                                
                                // If pendingBlock exists, update its height.
                                double pendingBlockStartMinutes = pendingBlock.Top * 60.0 / pixelsPerHour;
                                double pendingBlockEndMinutes = (validStart - dayStart).TotalMinutes;
                                double newHeight = (pendingBlockEndMinutes - pendingBlockStartMinutes) * pixelsPerHour / 60.0;
                                
                                if (newHeight < 1) 
                                {
                                    // It became too small or negative. Kill it.
                                    pendingBlock = null; 
                                    // Remove from list if it was already added? 
                                    // In this loop structure, pendingBlock hasn't been added to mergedBlocks yet.
                                    // BUT, what if the overlap defeats a block that was ALREADY finalized in mergedBlocks?
                                    // (e.g. huge block from A, then small block B inside it)
                                    // This linear pass assumes strictly sequential processing.
                                    
                                    // To do this robustly:
                                    // We should perform the TRIM logic *before* layout creation?
                                    // OR, just clip the CURRENT start to the global max?
                                    // "active apps overlap... impossible" -> This usually means the logger didn't catch the stop event until later.
                                    // So the START event of the NEW app is the true interrupt.
                                }
                                else
                                {
                                    pendingBlock.Height = newHeight;
                                    // Update duration text
                                    double dMin = pendingBlockEndMinutes - pendingBlockStartMinutes;
                                    pendingBlock.DurationText = DigitalWellbeing.Core.Helpers.StringHelper.TimeSpanToString(TimeSpan.FromMinutes(dMin));
                                    pendingBlock.ShowDetails = newHeight > 20;
                                }
                                lastEnd = validStart;
                                globalLastEnd = validStart;
                            }
                        }
                    }
                    
                    // We also need to check 'mergedBlocks' tail? 
                    // If we just finalized a block, it is in 'mergedBlocks', not 'pendingBlock'.
                    // In a linear loop, pendingBlock is only for "Same App Merging".
                    // If we switched from Chrome -> Word, Chrome is in mergedBlocks.
                    // Word starts. We need to check if Word overlaps Chrome.
                    
                    if (mergedBlocks.Count > 0)
                    {
                         var lastFinal = mergedBlocks[mergedBlocks.Count - 1];
                         // Reverse calc end time
                         double lastStartMin = lastFinal.Top * 60.0 / pixelsPerHour;
                         double lastHeightMin = lastFinal.Height * 60.0 / pixelsPerHour;
                         double lastEndMin = lastStartMin + lastHeightMin;
                         DateTime lastFinalEnd = dayStart.AddMinutes(lastEndMin);
                         
                         if (lastFinalEnd > validStart)
                         {
                             // Trim it
                             double newH = ((validStart - dayStart).TotalMinutes - lastStartMin) * pixelsPerHour / 60.0;
                             if (newH < 1)
                             {
                                 mergedBlocks.RemoveAt(mergedBlocks.Count - 1);
                             }
                             else
                             {
                                 lastFinal.Height = newH;
                                 double dMin = (validStart - dayStart).TotalMinutes - lastStartMin;
                                 lastFinal.DurationText = DigitalWellbeing.Core.Helpers.StringHelper.TimeSpanToString(TimeSpan.FromMinutes(dMin));
                                 lastFinal.ShowDetails = newH > 20;
                             }
                             // Update global tracker
                             globalLastEnd = validStart;
                         }
                    }
                }

                if (validEnd <= validStart) continue;

                // PREPARE DATA & VIEW MODE LOGIC
                
                // Apply retroactive hide filter: if HideSubAppsRetroactively is ON and ProgramName matches
                // an ignored keyword, treat this session as the parent process
                string effectiveProgramName = s.ProgramName;
                if (UserPreferences.ShouldHideSubApp(s.ProgramName))
                {
                    effectiveProgramName = s.ProcessName; // Hide sub-app, merge into parent
                }
                
                // 1. Calculate Tag (needed for Category mode and Coloring)
                AppTag tag;
                if (!string.IsNullOrEmpty(effectiveProgramName) && 
                    !effectiveProgramName.Equals(s.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    // Priority: Title-specific tag > ProgramName tag > Process tag
                    int? titleTagId = DigitalWellbeingWinUI3.Helpers.SettingsManager.GetTitleTagId(s.ProcessName, effectiveProgramName);
                    if (titleTagId.HasValue)
                    {
                        tag = (AppTag)titleTagId.Value;
                    }
                    else
                    {
                        // Try to get tag for ProgramName (e.g., "YouTube" might have its own tag)
                        var programTag = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetAppTag(effectiveProgramName);
                        if (programTag != AppTag.Untagged)
                        {
                            tag = programTag; // Use ProgramName's tag
                        }
                        else
                        {
                            tag = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetAppTag(s.ProcessName); // Fallback to process
                        }
                    }
                }
                else
                {
                    tag = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetAppTag(s.ProcessName);
                }

                // 2. Determine Merge Key and Display Title based on ViewMode
                string mergeKey = s.ProcessName;
                string displayName = DigitalWellbeingWinUI3.Helpers.UserPreferences.GetDisplayName(s.ProcessName);
                if (string.IsNullOrEmpty(displayName)) displayName = s.ProcessName;

                string title = displayName;
                
                if (ViewMode == "Category")
                {
                    // Use the calculated tag (respects sub-app tags like YouTube → Entertainment)
                    string tagName = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetTagDisplayName(tag);
                    if (string.IsNullOrEmpty(tagName)) tagName = "Untagged";
                    
                    title = tagName;
                    mergeKey = tagName; // For Category, we merge by the visible Title (Tag Name)
                }
                else if (ViewMode == "App")
                {
                    // Merge by ProcessName, Title is Display Name
                    // In App mode, always use ProcessName's tag (ignore sub-app tags)
                    mergeKey = s.ProcessName;
                    title = displayName;
                    tag = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetAppTag(s.ProcessName);
                }
                else // SubApp (Default)
                {
                    mergeKey = s.ProcessName;
                    
                    // Show effectiveProgramName if it differs from ProcessName (respects retroactive hide)
                    if (!string.IsNullOrEmpty(effectiveProgramName) && !effectiveProgramName.Equals(s.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        title = effectiveProgramName;
                    }
                    else
                    {
                        title = displayName;
                    }
                }
                
                if (title != null && title.Length > 30) title = title.Substring(0, 30) + "...";
                
                // 3. Color
                var baseBrush = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetTagColor(tag) as SolidColorBrush;
                Brush color = baseBrush;
                if (baseBrush != null)
                {
                    var c = baseBrush.Color;
                    var tintedColor = Windows.UI.Color.FromArgb(204, c.R, c.G, c.B);
                    color = new SolidColorBrush(tintedColor);
                }

                // MERGE CHECK
                bool isCompatible = false;
                if (pendingBlock != null)
                {
                    bool keysMatch = false;
                    if (ViewMode == "Category")
                    {
                        // In Category mode, mergeKey is the Tag Name (Title)
                        keysMatch = pendingBlock.Title == mergeKey;
                    }
                    else
                    {
                        // In App/SubApp mode, mergeKey is ProcessName
                        keysMatch = pendingBlock.ProcessName == mergeKey;
                    }

                    if (keysMatch)
                    {
                        int threshold = 30;
                        
                        if (lastEnd.HasValue && (validStart - lastEnd.Value).TotalSeconds < threshold)
                        {
                            if (pendingBlock.IsAfk == s.IsAfk)
                            {
                                isCompatible = true;
                            }
                        }
                    }
                }

                if (isCompatible)
                {
                    // Update Title Logic for SubApp mode
                    if (ViewMode == "SubApp" || ViewMode == null)
                    {
                        // IncognitoMode only affects logging, not display - always check for specific titles
                        bool newSessionHasSpecificTitle = 
                            !string.IsNullOrEmpty(s.ProgramName) && 
                            !s.ProgramName.Equals(s.ProcessName, StringComparison.OrdinalIgnoreCase);
                        
                        bool currentTitleIsGeneric = pendingBlock.Title != null && 
                            pendingBlock.Title.Equals(s.ProcessName, StringComparison.OrdinalIgnoreCase);
                        
                        if (newSessionHasSpecificTitle || (currentTitleIsGeneric && !string.IsNullOrEmpty(title) && title != s.ProcessName))
                        {
                            if (newSessionHasSpecificTitle)
                            {
                                pendingBlock.Title = title;
                                pendingBlock.OriginalSession = s;
                                // Also update color to match the new specific session's tag
                                pendingBlock.BackgroundColor = color;
                        }
                    }
                    }

                    // EXTEND
                    double newEndMinutes = (validEnd - dayStart).TotalMinutes;
                    double newHeight = (newEndMinutes * pixelsPerHour / 60.0) - pendingBlock.Top;
                    if (newHeight < 1) newHeight = 1;
                    pendingBlock.Height = newHeight;
                    
                    // Update Time Range
                    pendingBlock.EndTime = validEnd;
                    
                    // Update duration text
                    double totalMin = newHeight * 60.0 / pixelsPerHour;
                    pendingBlock.DurationText = DigitalWellbeing.Core.Helpers.StringHelper.TimeSpanToString(TimeSpan.FromMinutes(totalMin));
                    pendingBlock.ShowDetails = newHeight > 20;

                    lastEnd = validEnd;
                    globalLastEnd = validEnd;
                }
                else
                {
                    // FINALIZE Previous
                    if (pendingBlock != null) mergedBlocks.Add(pendingBlock);

                    // NEW BLOCK
                    double startMinutes = (validStart - dayStart).TotalMinutes;
                    double top = startMinutes * pixelsPerHour / 60.0;
                    double durationMinutes = (validEnd - validStart).TotalMinutes;
                    double height = durationMinutes * pixelsPerHour / 60.0;
                    if (height < 1) height = 1;

                    pendingBlock = new SessionBlock
                    {
                        Top = top,
                        Height = height,
                        Left = 0,
                        Width = ContentWidth,
                        Title = title,
                        ProcessName = s.ProcessName,
                        BackgroundColor = color,
                        OriginalSession = s,
                        IsAfk = s.IsAfk,
                        ShowDetails = height > 20,
                        DurationText = DigitalWellbeing.Core.Helpers.StringHelper.TimeSpanToString(TimeSpan.FromMinutes(durationMinutes)),
                        AudioSources = s.AudioSources != null ? new List<string>(s.AudioSources) : new List<string>(),
                        StartTime = validStart,
                        EndTime = validEnd
                    };
                    lastEnd = validEnd;
                    globalLastEnd = validEnd;
                }
            } // End Foreach
            
            if (pendingBlock != null) mergedBlocks.Add(pendingBlock);
            
            // Add all merged blocks to the observable collection
            // Sort by Top to maintain approximate chronological visual order (though overlaps will happen)
            newBlocks.Clear(); // Clear any existing blocks from previous logic
            foreach (var block in mergedBlocks.OrderBy(b => b.Top))
            {
               // 2-minute threshold rule for AFK visualization
               // Height = Minutes * (PPH / 60)  =>  Minutes = Height * 60 / PPH
               double durationMin = block.Height * 60.0 / pixelsPerHour;
               if (block.IsAfk && durationMin < 2.0) 
               {
                   block.IsAfk = false;
               }

               newBlocks.Add(block);
            }
            
            // Post-process: Remove generic blocks if a specific sub-app block exists for the same process
            // This prevents "msedge" from showing when "Netflix" exists for that process
            // SKIP this when HideSubAppsRetroactively is ON - hidden sessions intentionally become generic blocks
            if (!UserPreferences.HideSubAppsRetroactively)
            {
                var blocksToRemove = new List<SessionBlock>();
                foreach (var block in newBlocks)
                {
                    bool isGeneric = (block.Title ?? "").Equals(block.ProcessName ?? "", StringComparison.OrdinalIgnoreCase);
                    if (isGeneric)
                    {
                        // Check if any specific block exists for this process
                        bool hasSpecificBlock = newBlocks.Any(other =>
                            other != block &&
                            (other.ProcessName ?? "").Equals(block.ProcessName ?? "", StringComparison.OrdinalIgnoreCase) &&
                            !(other.Title ?? "").Equals(other.ProcessName ?? "", StringComparison.OrdinalIgnoreCase));
                        
                        if (hasSpecificBlock)
                        {
                            blocksToRemove.Add(block);
                        }
                    }
                }
                
                foreach (var block in blocksToRemove)
                {
                    newBlocks.Remove(block);
                }
            }
            
            SessionBlocks = newBlocks;
            OnPropertyChanged(nameof(SessionBlocks));
            UpdateLayoutWidths(); 
        }

        private void RefreshGridLines(double pixelsPerHour)
        {
            var newLines = new ObservableCollection<TimeGridLine>();

            int stepMinutes = 60;
            if (pixelsPerHour > 1500) stepMinutes = 1;
            else if (pixelsPerHour > 720) stepMinutes = 5;
            else if (pixelsPerHour > 240) stepMinutes = 15;
            else if (pixelsPerHour > 120) stepMinutes = 30;

            int totalMinutes = 24 * 60;
            double pixelsPerMinute = pixelsPerHour / 60.0;
            double rowHeight = stepMinutes * pixelsPerMinute;

            for (int i = 0; i < totalMinutes; i += stepMinutes)
            {
                TimeSpan ts = TimeSpan.FromMinutes(i);
                string text = "";
                bool isHour = (i % 60) == 0;
                
                if (isHour) text = ts.ToString(@"hh\:mm");
                else if (rowHeight > 20) text = ts.ToString(@"mm");

                newLines.Add(new TimeGridLine
                {
                    TimeText = text,
                    Height = rowHeight,
                    Top = (i / 60.0) * pixelsPerHour,
                    Opacity = i == 0 ? 0.0 : (isHour ? 0.3 : 0.1),
                    FontSize = isHour ? 12 : 10,
                    Width = ContentWidth
                });
            }
            GridLines = newLines;
            OnPropertyChanged(nameof(GridLines));
        }
        
        public void UpdateCurrentTime(double pixelsPerHour)
        {
            if (IsToday)
            {
                var now = DateTime.Now;
                double totalMinutes = now.TimeOfDay.TotalMinutes;
                CurrentTimeTop = (totalMinutes / 60.0) * pixelsPerHour;
                CurrentTimeVisibility = Visibility.Visible;
            }
            else
            {
                CurrentTimeVisibility = Visibility.Collapsed;
            }
        }

        private void UpdateLayoutWidths()
        {
            foreach (var line in GridLines)
            {
                line.Width = ContentWidth;
            }

            foreach (var block in SessionBlocks)
            {
                block.Width = ContentWidth;
            }
        }

        private void UpdateCurrentTimeVisibility()
        {
            CurrentTimeVisibility = IsToday ? Visibility.Visible : Visibility.Collapsed;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

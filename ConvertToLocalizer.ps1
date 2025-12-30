# Convert x:Uid to l:Uids.Uid for WinUI3Localizer
# This script updates all XAML files in the Views folder

$viewsPath = "h:\Coding\DigitalWellbeing\DigitalWellbeing_myworkGemini2\DigitalWellbeingWinUI3\Views"
$mainWindowPath = "h:\Coding\DigitalWellbeing\DigitalWellbeing_myworkGemini2\DigitalWellbeingWinUI3\MainWindow.xaml"

# Process each XAML file in Views
Get-ChildItem -Path $viewsPath -Filter "*.xaml" | ForEach-Object {
    $file = $_.FullName
    Write-Host "Processing: $file"
    
    $content = Get-Content -Path $file -Raw -Encoding UTF8
    
    # Add the WinUI3Localizer namespace if not present
    if ($content -notmatch 'xmlns:l="using:WinUI3Localizer"') {
        # Add namespace after mc:Ignorable="d"
        $content = $content -replace '(mc:Ignorable="d")', '$1`n    xmlns:l="using:WinUI3Localizer"'
    }
    
    # Replace x:Uid with l:Uids.Uid
    $content = $content -replace 'x:Uid="', 'l:Uids.Uid="'
    
    # Write back
    Set-Content -Path $file -Value $content -Encoding UTF8 -NoNewline
    Write-Host "Updated: $file"
}

# Process MainWindow.xaml
Write-Host "Processing: $mainWindowPath"
$content = Get-Content -Path $mainWindowPath -Raw -Encoding UTF8

if ($content -notmatch 'xmlns:l="using:WinUI3Localizer"') {
    $content = $content -replace '(mc:Ignorable="d")', '$1`n    xmlns:l="using:WinUI3Localizer"'
}

$content = $content -replace 'x:Uid="', 'l:Uids.Uid="'
Set-Content -Path $mainWindowPath -Value $content -Encoding UTF8 -NoNewline
Write-Host "Updated: $mainWindowPath"

Write-Host "Done! All x:Uid attributes converted to l:Uids.Uid"

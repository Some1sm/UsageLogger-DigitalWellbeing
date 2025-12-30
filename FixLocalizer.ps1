# Fix the broken xmlns:l declarations in all XAML files
# The previous script broke them by using `n which became literal text

$viewsPath = "h:\Coding\DigitalWellbeing\DigitalWellbeing_myworkGemini2\DigitalWellbeingWinUI3\Views"
$mainWindowPath = "h:\Coding\DigitalWellbeing\DigitalWellbeing_myworkGemini2\DigitalWellbeingWinUI3\MainWindow.xaml"

# Process each XAML file in Views
Get-ChildItem -Path $viewsPath -Filter "*.xaml" | ForEach-Object {
    $file = $_.FullName
    Write-Host "Fixing: $file"
    
    $content = Get-Content -Path $file -Raw -Encoding UTF8
    
    # Fix the broken namespace declaration - remove the `n and put on proper line
    # Pattern: mc:Ignorable="d"`n    xmlns:l="using:WinUI3Localizer"
    # Should be: mc:Ignorable="d"NEWLINE    xmlns:l="using:WinUI3Localizer"
    $content = $content -replace 'mc:Ignorable="d"``n    xmlns:l="using:WinUI3Localizer"', "mc:Ignorable=`"d`"`r`n    xmlns:l=`"using:WinUI3Localizer`""
    
    # Write back
    Set-Content -Path $file -Value $content -Encoding UTF8 -NoNewline
    Write-Host "Fixed: $file"
}

# Process MainWindow.xaml
Write-Host "Fixing: $mainWindowPath"
$content = Get-Content -Path $mainWindowPath -Raw -Encoding UTF8
$content = $content -replace 'mc:Ignorable="d"``n    xmlns:l="using:WinUI3Localizer"', "mc:Ignorable=`"d`"`r`n    xmlns:l=`"using:WinUI3Localizer`""
Set-Content -Path $mainWindowPath -Value $content -Encoding UTF8 -NoNewline
Write-Host "Fixed: $mainWindowPath"

Write-Host "Done! Namespace declarations fixed."

# FixEncodingAndAddFocusKey.ps1
# Fixes UTF-8 encoding in all Resources.resw files and adds Nav_Focus.Content key

$stringsFolder = "h:\Coding\DigitalWellbeing\DigitalWellbeing_myworkGemini2\DigitalWellbeingWinUI3\Strings"
$newDataEntry = @"
  <data name="Nav_Focus.Content" xml:space="preserve">
    <value>Focus Schedule</value>
  </data>
"@

# Get all Resources.resw files except en-US (we already updated that)
$files = Get-ChildItem -Path $stringsFolder -Recurse -Filter "Resources.resw" | Where-Object { $_.DirectoryName -notmatch "en-US" }

Write-Host "Found $($files.Count) non-English resource files"

foreach ($file in $files) {
    Write-Host "Processing: $($file.FullName)"
    
    # Read content with proper encoding detection
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    
    # Check if Nav_Focus.Content already exists
    if ($content -notmatch 'name="Nav_Focus.Content"') {
        # Insert before </root>
        $content = $content -replace '</root>', "$newDataEntry`r`n</root>"
        Write-Host "  Added Nav_Focus.Content key"
    }
    else {
        Write-Host "  Nav_Focus.Content already exists"
    }
    
    # Write back with explicit UTF-8 BOM encoding
    $utf8WithBom = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllText($file.FullName, $content, $utf8WithBom)
    Write-Host "  Re-saved with UTF-8 BOM encoding"
}

Write-Host "`nDone! Processed $($files.Count) files"

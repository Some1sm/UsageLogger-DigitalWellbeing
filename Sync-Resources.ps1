param (
    [string]$SourcePath = "DigitalWellbeingWinUI3\Strings\en-US\Resources.resw",
    [string]$StringsDir = "DigitalWellbeingWinUI3\Strings"
)

$ErrorActionPreference = "Stop"

Write-Host "Reading Source: $SourcePath"
$sourceXml = [xml](Get-Content $SourcePath)
$sourceData = $sourceXml.root.data

$files = Get-ChildItem -Path $StringsDir -Recurse -Filter "Resources.resw"
Write-Host "Found $($files.Count) resource files."

foreach ($file in $files) {
    # Skip source file
    if ($file.FullName -eq (Convert-Path $SourcePath)) { continue }

    Write-Host "Processing $($file.Directory.Name)..."
    try {
        $targetXml = [xml](Get-Content $file.FullName)
        $targetRoot = $targetXml.root
        
        $modified = $false
        
        foreach ($node in $sourceData) {
            $key = $node.name
            # Check if key exists (Case sensitive check? XPath is usually case sensitive)
            # Use dot notation for simpler check if mostly unique
            $existing = $targetRoot.SelectSingleNode("data[@name='$key']")
            
            if ($null -eq $existing) {
                # Write-Host "  + Adding: $key"
                $newNode = $targetXml.ImportNode($node, $true)
                $targetRoot.AppendChild($newNode) | Out-Null
                $modified = $true
            }
        }
        
        if ($modified) {
            $targetXml.Save($file.FullName)
            Write-Host "  Saved changes."
        }
    }
    catch {
        Write-Host "  ERROR processing $($file.Name): $_"
    }
}

Write-Host "Sync Complete."

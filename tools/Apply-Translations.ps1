param (
    [string]$StringsDir = "UsageLogger\Strings"
)

$ErrorActionPreference = "Stop"

$mapFile = Get-Item -Path "TranslationMap.json"

if (-not (Test-Path $mapFile.FullName)) {
    Write-Error "TranslationMap.json not found."
}

Write-Host "Found Translation Map file: $($mapFile.Name)"

$folders = Get-ChildItem -Path $StringsDir -Directory
Write-Host "Found $($folders.Count) language folders."

Write-Host "Processing $($mapFile.Name)..."
try {
    $mapJson = Get-Content -Raw $mapFile.FullName -Encoding UTF8 | ConvertFrom-Json
}
catch {
    Write-Error "Failed to parse $($mapFile.Name)."
}

foreach ($folder in $folders) {
    if ($folder.Name -eq "en-US") { continue }
    
    $langCode = $folder.Name
    $baseCode = $langCode.Split('-')[0]
    
    $translations = $null
    
    # Try Exact Match
    if ($mapJson.PSObject.Properties.Name -contains $langCode) {
        $translations = $mapJson.$langCode
        Write-Host "  [$langCode] Applying specific translations..."
    } 
    # Try Base Match
    elseif ($mapJson.PSObject.Properties.Name -contains $baseCode) {
        $translations = $mapJson.$baseCode
        Write-Host "  [$langCode] Applying base '$baseCode' translations..."
    }

    if ($translations) {
        $reswPath = Join-Path $folder.FullName "Resources.resw"
        if (Test-Path $reswPath) {
            try {
                $xml = [xml](Get-Content $reswPath)
                $root = $xml.root
                $modified = $false
                
                foreach ($prop in $translations.PSObject.Properties) {
                    $key = $prop.Name
                    $val = $prop.Value
                    
                    # Find existing node
                    $node = $root.SelectSingleNode("data[@name='$key']")
                    
                    if ($node) {
                        $valueNode = $node.SelectSingleNode("value")
                        if ($valueNode.InnerText -ne $val) {
                            $valueNode.InnerText = $val
                            $modified = $true
                        }
                    }
                    else {
                        $newNode = $xml.CreateElement("data")
                        $newNode.SetAttribute("name", $key)
                        $newNode.SetAttribute("xml:space", "preserve")
                        $valNode = $xml.CreateElement("value")
                        $valNode.InnerText = $val
                        $newNode.AppendChild($valNode) | Out-Null
                        $root.AppendChild($newNode) | Out-Null
                        $modified = $true
                    }
                }
                
                if ($modified) {
                    $writerSettings = New-Object System.Xml.XmlWriterSettings
                    $writerSettings.Indent = $true
                    $writerSettings.Encoding = [System.Text.Encoding]::UTF8
                    $writer = [System.Xml.XmlWriter]::Create($reswPath, $writerSettings)
                    $xml.Save($writer)
                    $writer.Close()
                }
            }
            catch {
                Write-Host "    Error updating $reswPath : $_"
            }
        }
    }
}

Write-Host "All Translations Applied."

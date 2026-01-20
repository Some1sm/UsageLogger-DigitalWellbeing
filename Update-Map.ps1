
$mapPath = "TranslationMap.json"
$json = Get-Content -Raw $mapPath -Encoding UTF8 | ConvertFrom-Json

$newKeys = @{
    "Dialog_ExcludeApp_Title" = "Exclude App?"
    "Dialog_ExcludeApp_Content" = "Are you sure you want to hide '{0}' from the dashboard? You can manage excluded apps in Settings."
    "Dialog_ExcludeSubApp_Title" = "Exclude Sub-App?"
    "Dialog_ExcludeSubApp_Content" = "Are you sure you want to hide '{0}' from the dashboard? You can manage excluded items in Settings."
    "Dialog_Exclude" = "Exclude"
    "Dialog_SetTimeLimit_Placeholder" = "Minutes (e.g., 60)"
}

# Iterate over each language property in the PSCustomObject
foreach ($langProp in $json.PSObject.Properties) {
    $langCode = $langProp.Name
    $langObj = $langProp.Value
    
    Write-Host "Updating $langCode..."
    
    foreach ($key in $newKeys.Keys) {
        # Check if key exists using PSObject properties
        if (-not ($langObj.PSObject.Properties.Match($key).Count -gt 0)) {
            Add-Member -InputObject $langObj -MemberType NoteProperty -Name $key -Value $newKeys[$key]
            Write-Host "  Added $key"
        }
    }
}

$json | ConvertTo-Json -Depth 10 | Set-Content $mapPath -Encoding UTF8
Write-Host "Done updating map."

$languages = @(
    "af", "am", "ar-sa", "as", "az-Latn", "be", "bg", "bn-BD", "bn-IN", "bs", 
    "ca", "ca-ES-valencia", "cs", "cy", "da", "de", "de-de", "el", "en-GB", 
    "es", "es-US", "es-MX", "et", "eu", "fa", "fi", "fil-Latn", "fr", "fr-FR", "fr-CA", 
    "ga", "gd-Latn", "gl", "gu", "ha-Latn", "he", "hi", "hr", "hu", "hy", 
    "id", "ig-Latn", "is", "it", "it-it", "ja", "ka", "kk", "km", "kn", "ko", "kok", 
    "ku-Arab", "ky-Cyrl", "lb", "lt", "lv", "mi-Latn", "mk", "ml", "mn-Cyrl", "mr", 
    "ms", "mt", "nb", "ne", "nl", "nl-BE", "nn", "nso", "or", "pa", "pa-Arab", "pl", 
    "prs-Arab", "pt-BR", "pt-PT", "qut-Latn", "quz", "ro", "ru", "rw", "sd-Arab", "si", 
    "sk", "sl", "sq", "sr-Cyrl-BA", "sr-Cyrl-RS", "sr-Latn-RS", "sv", "sw", "ta", "te", 
    "tg-Cyrl", "th", "ti", "tk-Latn", "tn", "tr", "tt-Cyrl", "ug-Arab", "uk", "ur", 
    "uz-Latn", "vi", "wo", "xh", "yo-Latn", "zh-Hans", "zh-Hant", "zu"
)

# Define paths
$sourceFile = "h:\Coding\DigitalWellbeing\DigitalWellbeing_myworkGemini2\DigitalWellbeingWinUI3\Strings\en-US\Resources.resw"
$destinationRoot = "h:\Coding\DigitalWellbeing\DigitalWellbeing_myworkGemini2\DigitalWellbeingWinUI3\Strings"

# Check if source exists
if (-not (Test-Path $sourceFile)) {
    Write-Error "Source file not found at: $sourceFile"
    exit 1
}

foreach ($lang in $languages) {
    $langPath = Join-Path $destinationRoot $lang
    $destFile = Join-Path $langPath "Resources.resw"

    if (-not (Test-Path $langPath)) {
        New-Item -ItemType Directory -Path $langPath | Out-Null
        Write-Host "Created directory: $lang"
    }

    if (-not (Test-Path $destFile)) {
        Copy-Item -Path $sourceFile -Destination $destFile
        Write-Host "Created resource file for: $lang"
    }
    else {
        Write-Host "Skipped existing: $lang"
    }
}

Write-Host "Bulk resource creation complete."


$mapPath = "TranslationMap.json"
$json = Get-Content -Raw $mapPath -Encoding UTF8 | ConvertFrom-Json

# Helper to safely set property
function Set-Val ($langObj, $key, $val) {
    if ($langObj.PSObject.Properties.Match($key).Count -gt 0) {
        $langObj.$key = $val
    }
    else {
        Add-Member -InputObject $langObj -MemberType NoteProperty -Name $key -Value $val
    }
}

# --- Italian ---
Write-Host "Updating it..."
Set-Val $json.it "Dialog_ExcludeApp_Title" "Escludi App?"
Set-Val $json.it "Dialog_ExcludeApp_Content" "Sei sicuro di voler nascondere '{0}' dalla dashboard? Puoi gestire le app escluse nelle Impostazioni."
Set-Val $json.it "Dialog_ExcludeSubApp_Title" "Escludi Sotto-App?"
Set-Val $json.it "Dialog_ExcludeSubApp_Content" "Sei sicuro di voler nascondere '{0}' dalla dashboard? Puoi gestire gli elementi esclusi nelle Impostazioni."
Set-Val $json.it "Dialog_Exclude" "Escludi"
Set-Val $json.it "Dialog_SetTimeLimit_Placeholder" "Minuti (es. 60)"

# --- Spanish ---
Write-Host "Updating es..."
Set-Val $json.es "Dialog_ExcludeApp_Title" "¿Excluir App?"
Set-Val $json.es "Dialog_ExcludeApp_Content" "¿Seguro que quieres ocultar '{0}' del panel? Puedes gestionar las apps excluidas en Configuración."
Set-Val $json.es "Dialog_ExcludeSubApp_Title" "¿Excluir Sub-App?"
Set-Val $json.es "Dialog_ExcludeSubApp_Content" "¿Seguro que quieres ocultar '{0}' del panel? Puedes gestionar los elementos excluidos en Configuración."
Set-Val $json.es "Dialog_Exclude" "Excluir"
Set-Val $json.es "Dialog_SetTimeLimit_Placeholder" "Minutos (ej. 60)"

# --- French ---
Write-Host "Updating fr..."
Set-Val $json.fr "Dialog_ExcludeApp_Title" "Exclure l'application ?"
Set-Val $json.fr "Dialog_ExcludeApp_Content" "Voulez-vous vraiment masquer '{0}' du tableau de bord ? Vous pouvez gérer les applications exclues dans les Paramètres."
Set-Val $json.fr "Dialog_ExcludeSubApp_Title" "Exclure la sous-application ?"
Set-Val $json.fr "Dialog_ExcludeSubApp_Content" "Voulez-vous vraiment masquer '{0}' du tableau de bord ? Vous pouvez gérer les éléments exclus dans les Paramètres."
Set-Val $json.fr "Dialog_Exclude" "Exclure"
Set-Val $json.fr "Dialog_SetTimeLimit_Placeholder" "Minutes (ex. 60)"

# --- German ---
Write-Host "Updating de..."
Set-Val $json.de "Dialog_ExcludeApp_Title" "App ausschließen?"
Set-Val $json.de "Dialog_ExcludeApp_Content" "Möchten Sie '{0}' wirklich aus dem Dashboard ausblenden? Sie können ausgeschlossene Apps in den Einstellungen verwalten."
Set-Val $json.de "Dialog_ExcludeSubApp_Title" "Unter-App ausschließen?"
Set-Val $json.de "Dialog_ExcludeSubApp_Content" "Möchten Sie '{0}' wirklich aus dem Dashboard ausblenden? Sie können ausgeschlossene Elemente in den Einstellungen verwalten."
Set-Val $json.de "Dialog_Exclude" "Ausschließen"
Set-Val $json.de "Dialog_SetTimeLimit_Placeholder" "Minuten (z.B. 60)"

# --- Chinese (Simplified) ---
Write-Host "Updating zh-Hans..."
Set-Val $json.'zh-Hans' "Dialog_ExcludeApp_Title" "排除应用？"
Set-Val $json.'zh-Hans' "Dialog_ExcludeApp_Content" "您确定要从仪表板中隐藏“ { 0 }”吗？您可以在设置中管理排除的应用。"
Set-Val $json.'zh-Hans' "Dialog_ExcludeSubApp_Title" "排除子应用？"
Set-Val $json.'zh-Hans' "Dialog_ExcludeSubApp_Content" "您确定要从仪表板中隐藏“ { 0 }”吗？您可以在设置中管理排除的项目。"
Set-Val $json.'zh-Hans' "Dialog_Exclude" "排除"
Set-Val $json.'zh-Hans' "Dialog_SetTimeLimit_Placeholder" "分钟 (例如 60)"

# --- Korean ---
Write-Host "Updating ko..."
Set-Val $json.ko "Dialog_ExcludeApp_Title" "앱 제외?"
Set-Val $json.ko "Dialog_ExcludeApp_Content" "대시보드에서 '{0}'을(를) 숨기시겠습니까? 설정에서 제외된 앱을 관리할 수 있습니다."
Set-Val $json.ko "Dialog_ExcludeSubApp_Title" "하위 앱 제외?"
Set-Val $json.ko "Dialog_ExcludeSubApp_Content" "대시보드에서 '{0}'을(를) 숨기시겠습니까? 설정에서 제외된 항목을 관리할 수 있습니다."
Set-Val $json.ko "Dialog_Exclude" "제외"
Set-Val $json.ko "Dialog_SetTimeLimit_Placeholder" "분 (예: 60)"

# --- Japanese ---
Write-Host "Updating ja..."
Set-Val $json.ja "Dialog_ExcludeApp_Title" "アプリを除外しますか？"
Set-Val $json.ja "Dialog_ExcludeApp_Content" "ダッシュボードから '{0}' を非表示にしますか？除外されたアプリは設定で管理できます。"
Set-Val $json.ja "Dialog_ExcludeSubApp_Title" "サブアプリを除外しますか？"
Set-Val $json.ja "Dialog_ExcludeSubApp_Content" "ダッシュボードから '{0}' を非表示にしますか？除外された項目は設定で管理できます。"
Set-Val $json.ja "Dialog_Exclude" "除外"
Set-Val $json.ja "Dialog_SetTimeLimit_Placeholder" "分 (例: 60)"

# --- Russian ---
Write-Host "Updating ru..."
Set-Val $json.ru "Dialog_ExcludeApp_Title" "Исключить приложение?"
Set-Val $json.ru "Dialog_ExcludeApp_Content" "Вы уверены, что хотите скрыть '{0}' из панели управления? Вы можете управлять исключенными приложениями в настройках."
Set-Val $json.ru "Dialog_ExcludeSubApp_Title" "Исключить под-приложение?"
Set-Val $json.ru "Dialog_ExcludeSubApp_Content" "Вы уверены, что хотите скрыть '{0}' из панели управления? Вы можете управлять исключенными элементами в настройках."
Set-Val $json.ru "Dialog_Exclude" "Исключить"
Set-Val $json.ru "Dialog_SetTimeLimit_Placeholder" "Минуты (например, 60)"

# --- Portuguese (Brazil) ---
Write-Host "Updating pt-BR..."
Set-Val $json.'pt-BR' "Dialog_ExcludeApp_Title" "Excluir App?"
Set-Val $json.'pt-BR' "Dialog_ExcludeApp_Content" "Tem certeza que deseja ocultar '{0}' do painel? Você pode gerenciar apps excluídos nas Configurações."
Set-Val $json.'pt-BR' "Dialog_ExcludeSubApp_Title" "Excluir Sub-App?"
Set-Val $json.'pt-BR' "Dialog_ExcludeSubApp_Content" "Tem certeza que deseja ocultar '{0}' do painel? Você pode gerenciar itens excluídos nas Configurações."
Set-Val $json.'pt-BR' "Dialog_Exclude" "Excluir"
Set-Val $json.'pt-BR' "Dialog_SetTimeLimit_Placeholder" "Minutos (ex. 60)"

# --- Catalan ---
Write-Host "Updating ca..."
Set-Val $json.ca "Dialog_ExcludeApp_Title" "Excloure aplicació?"
Set-Val $json.ca "Dialog_ExcludeApp_Content" "Segur que vols amagar '{0}' del tauler? Pots gestionar les aplicacions excloses a la Configuració."
Set-Val $json.ca "Dialog_ExcludeSubApp_Title" "Excloure subaplicació?"
Set-Val $json.ca "Dialog_ExcludeSubApp_Content" "Segur que vols amagar '{0}' del tauler? Pots gestionar els elements exclosos a la Configuració."
Set-Val $json.ca "Dialog_Exclude" "Excloure"
Set-Val $json.ca "Dialog_SetTimeLimit_Placeholder" "Minuts (ex. 60)"


$json | ConvertTo-Json -Depth 10 | Set-Content $mapPath -Encoding UTF8
Write-Host "Done updating map with real translations."

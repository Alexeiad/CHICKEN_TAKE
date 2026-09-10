$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false)
function Write-Utf8($path, $value) { [IO.File]::WriteAllText((Join-Path (Get-Location) $path), $value, $utf8) }
$soGuid = 'dd3457a683da4f8c863b1e1dac925a71'
$scriptGuid = 'd0e1b7c6a9234d7f8cf1e266db130245'
$assetGuid = 'be4e90e8ae7f49b389c7ac65fd6d5ab2'
foreach ($pair in @(@('Assets/_Scripts/UI/MenuTranslationsSO.cs', $soGuid), @('Assets/_Scripts/UI/MenuLocalization.cs', $scriptGuid))) {
    Write-Utf8 ($pair[0] + '.meta') "fileFormatVersion: 2`nguid: $($pair[1])`n"
}
$rows = @(
    @('playText','Играть','Play','Spielen'),
    @('settingsText','Настройки','Settings','Einstellungen'),
    @('exitText','Выйти','Quit','Beenden'),
    @('authorsText','Авторы','Credits','Mitwirkende'),
    @('savesText','Сохранения','Saved games','Spielstände'),
    @('loadText','Загрузить','Load','Laden'),
    @('slot1Text','Слот 1','Slot 1','Spielstand 1'),
    @('slot2Text','Слот 2','Slot 2','Spielstand 2'),
    @('slot3Text','Слот 3','Slot 3','Spielstand 3'),
    @('createSaveText','Нажмите, чтобы создать новое сохранение','Click to create a new save','Klicken, um einen neuen Spielstand anzulegen'),
    @('volumeText','Громкость','Volume','Lautstärke'),
    @('musicVolumeText','Громкость музыки','Music volume','Musiklautstärke'),
    @('soundVolumeText','Громкость звуков','Sound volume','Effektlautstärke'),
    @('brightnessText','Настройки яркости экрана','Screen brightness','Bildschirmhelligkeit'),
    @('resolutionText','Настройки разрешения экрана','Screen resolution','Bildschirmauflösung'),
    @('qualityText','Качество графики','Graphics quality','Grafikqualität'),
    @('graphicsText','ГРАФИКА','GRAPHICS','GRAFIK'),
    @('backText','Вернуться','Back','Zurück'),
    @('cameraDistanceText','Расстояние камеры','Camera distance','Kameraabstand'),
    @('cameraSettingsText','Настройки камеры','Camera settings','Kameraeinstellungen'),
    @('viewKeyText','Клавиша вида','View key','Perspektivtaste'),
    @('sensitivityText','Чувствительность','Sensitivity','Empfindlichkeit'),
    @('firstPersonText','От первого лица','First person','Ego-Perspektive'),
    @('thirdPersonText','От третьего лица','Third person','Dritte Person'),
    @('languageText','Язык: Русский','Language: English','Sprache: Deutsch')
)
$byRussian = @{}
$asset = "%YAML 1.1`n%TAG !u! tag:unity3d.com,2011:`n--- !u!114 &11400000`nMonoBehaviour:`n  m_ObjectHideFlags: 0`n  m_CorrespondingSourceObject: {fileID: 0}`n  m_PrefabInstance: {fileID: 0}`n  m_PrefabAsset: {fileID: 0}`n  m_GameObject: {fileID: 0}`n  m_Enabled: 1`n  m_EditorHideFlags: 0`n  m_Script: {fileID: 11500000, guid: $soGuid, type: 3}`n  m_Name: MenuTranslations`n  m_EditorClassIdentifier: `n  entries:`n"
foreach ($row in $rows) {
    $byRussian[$row[1]] = $row[0]
    $asset += "  - key: $($row[0])`n    russian: $($row[1] | ConvertTo-Json -Compress)`n    english: $($row[2] | ConvertTo-Json -Compress)`n    german: $($row[3] | ConvertTo-Json -Compress)`n"
}
Write-Utf8 'Assets/_Data/MenuTranslations.asset' $asset
Write-Utf8 'Assets/_Data/MenuTranslations.asset.meta' "fileFormatVersion: 2`nguid: $assetGuid`nNativeFormatImporter:`n  externalObjects: {}`n  mainObjectFileID: 11400000`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
$path = 'Assets/_Prefabs/MENU_INSTANCE.prefab'
$prefab = Get-Content $path -Raw
if ($prefab.Contains($scriptGuid)) { throw 'Already configured' }
$bindings = ''
$count = 0
foreach ($block in [regex]::Matches($prefab, '(?ms)^--- !u!114 &(\d+)\r?\n(.*?)(?=^---|\z)')) {
    $m = [regex]::Match($block.Groups[2].Value, '(?m)^  (m_[tT]ext): ("(?:[^"\\]|\\.)*"|[^\r\n]*)')
    if (!$m.Success) { continue }
    $value = $m.Groups[2].Value
    if ($value.StartsWith('"')) { $value = ($value -replace '\r?\n\s+', ' ') | ConvertFrom-Json }
    if (!$byRussian.ContainsKey($value)) { throw "No translation: $value" }
    $id = $block.Groups[1].Value
    $tmp = '0'; $legacy = '0'
    if ($m.Groups[1].Value -ceq 'm_text') { $tmp = $id } else { $legacy = $id }
    $bindings += "  - key: $($byRussian[$value])`n    tmpText: {fileID: $tmp}`n    legacyText: {fileID: $legacy}`n"
    $count++
}
# Explicit references to text components in the nested camera settings prefab.
$nested = @(
    @('1087133551239861809','9100000000000000101','cameraDistanceText'),
    @('3186171840298968574','9100000000000000102','cameraSettingsText'),
    @('1647927406223445752','9100000000000000103','viewKeyText'),
    @('6185619457533958288','9100000000000000104','sensitivityText'),
    @('3781229934587422462','9100000000000000105','')
)
foreach ($item in $nested) {
    $guid = if ($item[2]) {'f4688fdb7df04437aeb418b961361dc5'} else {'7b743370ac3e4ec2a1668f5455a8ef8a'}
    $prefab += "`n--- !u!114 &$($item[1]) stripped`nMonoBehaviour:`n  m_CorrespondingSourceObject: {fileID: $($item[0]), guid: f55ab861b15cff348bbb8cb860a7d618, type: 3}`n  m_PrefabInstance: {fileID: 3873498494976158815}`n  m_PrefabAsset: {fileID: 0}`n  m_GameObject: {fileID: 0}`n  m_Enabled: 1`n  m_EditorHideFlags: 0`n  m_Script: {fileID: 11500000, guid: $guid, type: 3}`n  m_Name: `n  m_EditorClassIdentifier: `n"
    if ($item[2]) {
        $bindings += "  - key: $($item[2])`n    tmpText: {fileID: $($item[1])}`n    legacyText: {fileID: 0}`n"
        $count++
    }
}
$prefab = $prefab.Replace('  - component: {fileID: 4785590452333129595}', "  - component: {fileID: 4785590452333129595}`n  - component: {fileID: 9100000000000000100}")
$prefab += "`n--- !u!114 &9100000000000000100`nMonoBehaviour:`n  m_ObjectHideFlags: 0`n  m_CorrespondingSourceObject: {fileID: 0}`n  m_PrefabInstance: {fileID: 0}`n  m_PrefabAsset: {fileID: 0}`n  m_GameObject: {fileID: 400515625921834987}`n  m_Enabled: 1`n  m_EditorHideFlags: 0`n  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}`n  m_Name: `n  m_EditorClassIdentifier: `n  translations: {fileID: 11400000, guid: $assetGuid, type: 2}`n  language: 0`n  texts:`n${bindings}  menuCanvas: {fileID: 6799600198027169879}`n  languageFont: {fileID: 11400000, guid: 020df937a954dea40a29c0ece6110d02, type: 2}`n  cameraModeDropdown: {fileID: 9100000000000000105}`n"
Write-Utf8 $path $prefab
Write-Output "Created $($rows.Count) translation entries and $count label bindings, plus camera dropdown."

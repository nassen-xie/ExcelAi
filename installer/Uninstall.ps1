<#
  Excel AI アシスタントをこの PC のあなたのアカウントから外します。
    ・Excel の起動時の読み込み（OPEN）と信頼できる場所の登録を消し、.xll を削除します
    ・設定・キャッシュ・ログ（%APPDATA%\ExcelAi）は残します。API キーも含めて全部消す場合は -RemoveData を付けてください
  使い方:  Uninstall.cmd をダブルクリック、または  powershell -ExecutionPolicy Bypass -File Uninstall.ps1 [-RemoveData]
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InstallDir = (Join-Path $env:APPDATA 'ExcelAi\addin'),
    [string]$OfficeRegistryRoot = 'HKCU:\Software\Microsoft\Office\16.0\Excel',
    [switch]$RemoveData
)

$ErrorActionPreference = 'Stop'
Write-Host 'Excel AI Assistant  (c) 2026 株式会社ナッセンシステム  https://nassen.co.jp'
$lang = switch -Regex ($PSUICulture) { '^ja' { 'ja' } '^zh' { 'zh' } default { 'en' } }
$T = @{
    ja = @{ open = '起動時の読み込みの登録を消しました: {0}'; trusted = '信頼できる場所の登録を消しました。'; file = '削除しました: {0}'; locked = '⚠ {0} を削除できません（Excel が使用中です）。Excel を閉じてからもう一度実行してください。'; data = '設定・キャッシュ・ログを削除しました: {0}'; kept = '設定・キャッシュ・ログは残しています（全部消す場合は -RemoveData）。'; done = '完了しました。' }
    zh = @{ open = '已删除启动加载项登记: {0}'; trusted = '已删除受信任位置的登记。'; file = '已删除: {0}'; locked = '⚠ 无法删除 {0}（Excel 正在使用）。请关闭 Excel 后重试。'; data = '已删除设置、缓存和日志: {0}'; kept = '已保留设置、缓存和日志（如需全部删除请加 -RemoveData）。'; done = '卸载完成。' }
    en = @{ open = 'Removed the startup registration: {0}'; trusted = 'Removed the trusted location.'; file = 'Deleted: {0}'; locked = '⚠ Cannot delete {0} (Excel is using it). Close Excel and run again.'; data = 'Deleted settings, cache and logs: {0}'; kept = 'Settings, cache and logs are kept (use -RemoveData to delete everything).'; done = 'Done.' }
}[$lang]

$target = Join-Path $InstallDir 'ExcelAi-AddIn.xll'
$options = Join-Path $OfficeRegistryRoot 'Options'
if (Test-Path $options) {
    $props = (Get-ItemProperty $options).PSObject.Properties | Where-Object { $_.Name -match '^OPEN\d*$' -and "$($_.Value)" -like "*$target*" }
    foreach ($p in $props) {
        if ($PSCmdlet.ShouldProcess($p.Name, 'remove OPEN')) {
            Remove-ItemProperty -Path $options -Name $p.Name
            Write-Host ($T.open -f $p.Name)
        }
    }
}

$trusted = Join-Path $OfficeRegistryRoot 'Security\Trusted Locations'
if (Test-Path $trusted) {
    foreach ($k in Get-ChildItem $trusted -ErrorAction SilentlyContinue) {
        if ((Get-ItemProperty $k.PSPath).Path -eq ($InstallDir + '\') -and $PSCmdlet.ShouldProcess($k.Name, 'remove trusted location')) {
            Remove-Item $k.PSPath -Recurse
            Write-Host $T.trusted
        }
    }
}

if (Test-Path $target) {
    if ($PSCmdlet.ShouldProcess($target, 'delete')) {
        try { Remove-Item $target -Force; Write-Host ($T.file -f $target) } catch { Write-Host ($T.locked -f $target) -ForegroundColor Yellow }
    }
}

$data = Split-Path -Parent $InstallDir
if ($RemoveData) {
    if ($PSCmdlet.ShouldProcess($data, 'delete data')) { Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue; Write-Host ($T.data -f $data) }
} else {
    Write-Host $T.kept
}

Write-Host $T.done -ForegroundColor Green

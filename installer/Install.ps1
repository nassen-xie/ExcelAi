<#
  Excel AI アシスタントをこの PC のあなたのアカウントに入れます（管理者の権限は要りません）。
    ・Excel の 32 ビット / 64 ビットを調べて、合う .xll を %APPDATA%\ExcelAi\addin に置きます
    ・次に Excel を起動したときに読み込まれるよう、Excel の起動時の読み込み（OPEN）に登録します
    ・信頼できる場所に追加します（「セキュリティの警告」を出さないため。-NoTrustedLocation で省略）
  使い方:  Install.cmd をダブルクリック、または  powershell -ExecutionPolicy Bypass -File Install.ps1
  オプション: -Bitness 32|64（自動判定を使わない） / -InstallDir <フォルダー> / -NoTrustedLocation / -WhatIf
  （-OfficeRegistryRoot はテスト用。ふつうは指定しません）
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('', '32', '64')][string]$Bitness = '',
    [string]$InstallDir = (Join-Path $env:APPDATA 'ExcelAi\addin'),
    [string]$OfficeRegistryRoot = 'HKCU:\Software\Microsoft\Office\16.0\Excel',
    [switch]$NoTrustedLocation
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$lang = switch -Regex ($PSUICulture) { '^ja' { 'ja' } '^zh' { 'zh' } default { 'en' } }
$T = @{
    ja = @{ detect = 'Excel は {0} ビットです。'; copy = 'コピーしました: {0}'; reg = 'Excel の起動時の読み込みに登録しました（{0}）。'; already = 'すでに登録されています。'; trusted = '信頼できる場所に追加しました: {0}'; running = '⚠ Excel が起動しています。いったん Excel を閉じて、もう一度開くと [AI] タブが出ます。'; done = '完了しました。Excel を起動して [AI] タブの「設定」で API キーを入力してください。'; missing = '{0} ビット用の .xll が見つかりません: {1}'; unknown = 'Excel のビット数を判定できません。-Bitness 32 または -Bitness 64 を指定してください。' }
    zh = @{ detect = 'Excel 是 {0} 位。'; copy = '已复制: {0}'; reg = '已登记到 Excel 的启动加载项（{0}）。'; already = '已经登记过。'; trusted = '已添加到受信任位置: {0}'; running = '⚠ Excel 正在运行。请先关闭 Excel 再重新打开，即可看到 [AI] 选项卡。'; done = '安装完成。启动 Excel，在 [AI] 选项卡的“设置”中输入 API 密钥。'; missing = '找不到 {0} 位的 .xll: {1}'; unknown = '无法判断 Excel 的位数。请指定 -Bitness 32 或 -Bitness 64。' }
    en = @{ detect = 'Excel is {0}-bit.'; copy = 'Copied: {0}'; reg = 'Registered to load when Excel starts ({0}).'; already = 'Already registered.'; trusted = 'Added to trusted locations: {0}'; running = '⚠ Excel is running. Close and reopen Excel to see the [AI] tab.'; done = 'Done. Start Excel and enter your API key in [AI] tab > Settings.'; missing = 'The {0}-bit .xll was not found: {1}'; unknown = 'Cannot detect the Excel bitness. Specify -Bitness 32 or -Bitness 64.' }
}[$lang]

# Excel の 32 / 64 ビット：Office (クイック実行) の Platform → 無ければ EXCEL.EXE の PE ヘッダー
function Get-ExcelBitness {
    $c2r = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue
    if ($c2r -and $c2r.Platform -match 'x64') { return 64 }
    if ($c2r -and $c2r.Platform -match 'x86') { return 32 }
    $path = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe' -ErrorAction SilentlyContinue).'(default)'
    if ($path -and (Test-Path $path)) {
        $bytes = [IO.File]::ReadAllBytes($path)[0..1023]
        $pe = [BitConverter]::ToInt32($bytes, 0x3C)
        $machine = [BitConverter]::ToUInt16($bytes, $pe + 4)
        if ($machine -eq 0x8664) { return 64 }
        if ($machine -eq 0x14c) { return 32 }
    }
    return 0
}

Write-Host 'Excel AI Assistant  (c) 2026 株式会社ナッセンシステム  https://nassen.co.jp  contact@nassen.co.jp  [MIT]'
$bits = if ($Bitness) { [int]$Bitness } else { Get-ExcelBitness }
if ($bits -eq 0) { throw $T.unknown }
Write-Host ($T.detect -f $bits)

$source = Join-Path $here ("ExcelAi-AddIn{0}.xll" -f $(if ($bits -eq 64) { '64' } else { '' }))
if (-not (Test-Path $source)) { throw ($T.missing -f $bits, $source) }

$target = Join-Path $InstallDir 'ExcelAi-AddIn.xll'
if ($PSCmdlet.ShouldProcess($target, 'copy xll')) {
    New-Item -ItemType Directory -Force $InstallDir | Out-Null
    # 前に入れたものが Excel に読み込まれている間は上書きできない
    try { Copy-Item $source $target -Force } catch { throw "$target を上書きできません。Excel をすべて閉じてからやり直してください。($($_.Exception.Message))" }
    Unblock-File $target -ErrorAction SilentlyContinue
    Write-Host ($T.copy -f $target)
}

# ---- Excel の起動時の読み込み（OPEN, OPEN1, OPEN2 …）に登録 ----
$options = Join-Path $OfficeRegistryRoot 'Options'
if ($PSCmdlet.ShouldProcess($options, 'register OPEN')) {
    # 注意: 既にあるキーに New-Item -Force をすると、中の値（ほかのアドインの登録）が消えるため、なければ作る
    if (-not (Test-Path $options)) { New-Item -Path $options -Force | Out-Null }
    $props = (Get-ItemProperty $options).PSObject.Properties | Where-Object { $_.Name -match '^OPEN\d*$' }
    $value = '/R "' + $target + '"'
    if ($props | Where-Object { "$($_.Value)" -like "*$target*" }) {
        Write-Host $T.already
    } else {
        $names = @($props | ForEach-Object { $_.Name })
        $name = if ($names -notcontains 'OPEN') { 'OPEN' } else { 'OPEN' + (1..99 | Where-Object { $names -notcontains "OPEN$_" } | Select-Object -First 1) }
        New-ItemProperty -Path $options -Name $name -Value $value -PropertyType String -Force | Out-Null
        Write-Host ($T.reg -f $name)
    }
}

# ---- 信頼できる場所（読み込みのたびの警告を出さない） ----
if (-not $NoTrustedLocation -and $PSCmdlet.ShouldProcess($InstallDir, 'trusted location')) {
    $trusted = Join-Path $OfficeRegistryRoot 'Security\Trusted Locations'
    if (-not (Test-Path $trusted)) { New-Item -Path $trusted -Force | Out-Null }
    $existing = Get-ChildItem $trusted -ErrorAction SilentlyContinue | Where-Object { (Get-ItemProperty $_.PSPath).Path -eq ($InstallDir + '\') }
    if (-not $existing) {
        $n = 1; while (Test-Path (Join-Path $trusted "Location$n")) { $n++ }
        $key = Join-Path $trusted "Location$n"
        New-Item -Path $key -Force | Out-Null
        New-ItemProperty -Path $key -Name 'Path' -Value ($InstallDir + '\') -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $key -Name 'AllowSubfolders' -Value 1 -PropertyType DWord -Force | Out-Null
        New-ItemProperty -Path $key -Name 'Description' -Value 'Excel AI Assistant' -PropertyType String -Force | Out-Null
        Write-Host ($T.trusted -f $InstallDir)
    }
}

if (Get-Process excel -ErrorAction SilentlyContinue) { Write-Host $T.running -ForegroundColor Yellow }
Write-Host $T.done -ForegroundColor Green

<#
  Excel の中から、実際の接続先（既定は DeepSeek）に無効なキーで =AI() を 1 回だけ呼び、結果のセルを表示する。
  「#AI! API キーが正しくない」と出れば、Excel の中からも通信できている。本物のキーは使わない。
  別の Excel を新しく起動し、設定は一時フォルダー。開いている Excel・本物の設定には触らない。
  使い方:  powershell -File tools\excel-probe.ps1 -Xll <packed.xll> [-Provider deepseek] [-BaseUrl <接続先>]
#>
param(
    [Parameter(Mandatory = $true)][string]$Xll,
    [string]$Provider = 'deepseek',
    [string]$BaseUrl = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
. (Join-Path $PSScriptRoot 'TestExcel.ps1')
$aiHome = Join-Path $env:TEMP ('excelai-xlprobe-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $aiHome | Out-Null
$protected = [Convert]::ToBase64String([Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes('sk-invalid-probe'), [Text.Encoding]::UTF8.GetBytes('ExcelAi.v1'), 'CurrentUser'))
@{ Provider = $Provider; Language = 'ja'; BaseUrl = $BaseUrl; ConsentAccepted = $true; UseCache = $false; ApiKeysProtected = @{ $Provider = $protected }; TimeoutSeconds = 30 } | ConvertTo-Json | Set-Content -Path (Join-Path $aiHome 'settings.json') -Encoding UTF8
$existing = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$xl = $null
$xlPid = 0
try {
    $started = Start-TestExcel -DataHome $aiHome
    $xl = $started.Application
    $xlPid = $started.Pid
    if ($existing -contains $xlPid) { throw '既存の Excel を操作しようとしているため中止しました。' }
    [void]$xl.RegisterXLL((Resolve-Path $Xll).Path)
    $ws = $xl.Workbooks.Add().Worksheets.Item(1)
    $ws.Range('A1').Formula = '=AI("hi")'
    $deadline = (Get-Date).AddSeconds(60)
    do { Start-Sleep -Milliseconds 500 } while ("$($ws.Range('A1').Text)" -match '^(#N/A|.*処理中|.*working)' -and (Get-Date) -lt $deadline)
    "Excel の中での結果: $($ws.Range('A1').Text)"
    "ログ:"
    Get-Content (Join-Path $aiHome 'logs\*.log') -Encoding UTF8 -ErrorAction SilentlyContinue | Select-Object -Last 12
}
finally {
    if ($xl) {
        try { $xl.DisplayAlerts = $false; $xl.Quit() } catch { }
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
        Start-Sleep -Seconds 2
        if ($xlPid -gt 0 -and ($existing -notcontains $xlPid)) { Get-Process -Id $xlPid -ErrorAction SilentlyContinue | Stop-Process -Force }
    }
    Remove-Item $aiHome -Recurse -Force -ErrorAction SilentlyContinue
}

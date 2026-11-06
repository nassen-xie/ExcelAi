<#
  ビルドして、配るためのフォルダー・zip（dist\ExcelAI-<版>）を作る。
    powershell -File build.ps1                 ビルド + テスト + 配布物の作成
    powershell -File build.ps1 -SkipTests      テストを省く
    powershell -File build.ps1 -Smoke          さらに、本物の Excel（新しいプロセス）で動作確認とインストーラーの確認をする
                                               （設定は一時フォルダー。本物の設定・開いている Excel には触りません）
  できるもの:  dist\ExcelAI-<版>\ （xll 32/64、Install/Uninstall、README.txt、THIRD-PARTY-NOTICES.txt、SHA256SUMS.txt）と同名の .zip
#>
param(
    [switch]$SkipTests,
    [switch]$Smoke
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

function Step($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }
function Run($exe, $arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "失敗しました（終了コード $LASTEXITCODE）: $exe $($arguments -join ' ')" }
}

Step 'ビルド（Release）'
Run 'dotnet' @('build', 'src/ExcelAi.Addin', '-c', 'Release', '--nologo', '-v', 'q')

if (-not $SkipTests) {
    Step 'テスト（ロジック: net10 / 通信の形: net48 = アドインと同じ SDK）'
    Run 'dotnet' @('test', 'tests/ExcelAi.Core.Tests', '--nologo', '-v', 'q')
    Run 'dotnet' @('test', 'tests/ExcelAi.Wire.Tests', '--nologo', '-v', 'q')
}

$publish = Join-Path $root 'src\ExcelAi.Addin\bin\Release\net48\publish'
$xll32 = Join-Path $publish 'ExcelAi.Addin-AddIn-packed.xll'
$xll64 = Join-Path $publish 'ExcelAi.Addin-AddIn64-packed.xll'
foreach ($x in $xll32, $xll64) { if (-not (Test-Path $x)) { throw "見つかりません: $x" } }

if ($Smoke) {
    Step '本物の Excel での動作確認（DeepSeek 形式: 数式・一括処理）'
    Run 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\smoke-test.ps1', '-Xll', $xll32, '-Provider', 'deepseek')
    Step '本物の Excel での動作確認（Claude 形式）'
    Run 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\smoke-test.ps1', '-Xll', $xll32, '-Provider', 'anthropic', '-Port', '18778')
    Step 'セルの右クリックメニュー「AI で処理…」'
    Run 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\ctxmenu-shot.ps1', '-Xll', $xll32, '-Out', (Join-Path $root 'dist\ui\ctxmenu.png'))
    Step '[AI] タブの画面（設定・ヘルプ・使用量・チャット）'
    Run 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\ui-e2e.ps1', '-Xll', $xll32, '-OutDir', (Join-Path $root 'dist\ui'))
    Step 'インストーラー'
    Run 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools\installer-test.ps1', '-Xll32', $xll32, '-Xll64', $xll64)
}

Step '配布物の作成'
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$out = Join-Path $root "dist\ExcelAI-$version"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item $xll32 (Join-Path $out 'ExcelAi-AddIn.xll')
Copy-Item $xll64 (Join-Path $out 'ExcelAi-AddIn64.xll')
Copy-Item (Join-Path $root 'installer\*') $out
(Get-Content (Join-Path $root 'docs\README-dist.txt') -Raw -Encoding UTF8).Replace('{VERSION}', $version) | Set-Content (Join-Path $out 'README.txt') -Encoding UTF8
Copy-Item (Join-Path $root 'docs\THIRD-PARTY-NOTICES.txt') $out
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $out 'LICENSE.txt')
Get-ChildItem $out -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name } | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ASCII

$zip = "$out.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $out -DestinationPath $zip
# 紹介ページ（dist\site）: 画面写真・ダウンロードの zip・版番号と SHA-256 を差し込んだ index.html
Step '紹介ページの作成'
$site = Join-Path $root 'dist\site'
Remove-Item $site -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $site 'downloads') | Out-Null
Copy-Item (Join-Path $root 'docs\site\img') (Join-Path $site 'img') -Recurse
Copy-Item $zip (Join-Path $site 'downloads')
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
# 支援（PayPay・微信支付）の設定。docs\site\sponsor.json に QR 画像・リンクを書いたものだけを表示する（空なら、支援の欄は出ない）
$sponsorFile = Join-Path $root 'docs\site\sponsor.json'
$sponsorJson = '{}'
if (Test-Path $sponsorFile) {
    $cfg = Get-Content $sponsorFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $sponsorDir = Join-Path $root 'docs\site\sponsor'
    if (Test-Path $sponsorDir) { Copy-Item $sponsorDir (Join-Path $site 'sponsor') -Recurse }
    $sponsorJson = ($cfg | ConvertTo-Json -Compress)
}
(Get-Content (Join-Path $root 'docs\site\index.html') -Raw -Encoding UTF8).Replace('{{VERSION}}', $version).Replace('{{SHA256}}', $sha).Replace('{{SPONSOR_JSON}}', $sponsorJson) | Set-Content (Join-Path $site 'index.html') -Encoding UTF8
Write-Host "紹介ページ: $site\index.html" -ForegroundColor Green
Write-Host "`n完成: $zip" -ForegroundColor Green
Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize

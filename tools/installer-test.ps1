<#
  インストーラー（Install.ps1 / Uninstall.ps1）の確認。本物の Excel の登録には触らず、テスト用のレジストリ キー
  HKCU:\Software\ExcelAiInstallerTest と一時フォルダーだけを使い、終わったら消します。
  使い方:  powershell -File tools\installer-test.ps1 -Xll32 <32 ビットの packed.xll> -Xll64 <64 ビットの packed.xll>
#>
param(
    [Parameter(Mandatory = $true)][string]$Xll32,
    [Parameter(Mandatory = $true)][string]$Xll64
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $env:TEMP ("excelai-install-test-" + [guid]::NewGuid().ToString('N'))
$installDir = Join-Path $stage 'installed\addin'
$reg = 'HKCU:\Software\ExcelAiInstallerTest\Excel'
$failures = 0
function Check($name, $ok) { if ($ok) { "PASS  $name" } else { "FAIL  $name"; $script:failures++ } }

try {
    New-Item -ItemType Directory -Force $stage | Out-Null
    Copy-Item (Join-Path $root 'installer\*') $stage
    Copy-Item $Xll32 (Join-Path $stage 'ExcelAi-AddIn.xll')
    Copy-Item $Xll64 (Join-Path $stage 'ExcelAi-AddIn64.xll')
    $target = Join-Path $installDir 'ExcelAi-AddIn.xll'
    $args32 = @{ Bitness = '32'; InstallDir = $installDir; OfficeRegistryRoot = $reg }

    # --- WhatIf は何も変えない ---
    & (Join-Path $stage 'Install.ps1') @args32 -WhatIf | Out-Null
    Check 'WhatIf changes nothing' (-not (Test-Path $target) -and -not (Test-Path $reg))

    # --- 32 ビット ---
    & (Join-Path $stage 'Install.ps1') @args32 | Out-Null
    Check 'xll copied (32-bit)' ((Get-FileHash $target).Hash -eq (Get-FileHash $Xll32).Hash)
    $open = (Get-ItemProperty "$reg\Options").OPEN
    Check 'OPEN registered with /R and quoted path' ($open -eq ('/R "' + $target + '"'))
    $loc = Get-ChildItem "$reg\Security\Trusted Locations" | ForEach-Object { Get-ItemProperty $_.PSPath }
    Check 'trusted location added' (@($loc | Where-Object { $_.Path -eq ($installDir + '\') -and $_.AllowSubfolders -eq 1 }).Count -eq 1)

    # --- 2 回目は増えない ---
    & (Join-Path $stage 'Install.ps1') @args32 | Out-Null
    $opens = @((Get-ItemProperty "$reg\Options").PSObject.Properties | Where-Object { $_.Name -match '^OPEN\d*$' })
    Check 'second install is idempotent (1 OPEN entry)' ($opens.Count -eq 1)
    Check 'second install keeps 1 trusted location' (@(Get-ChildItem "$reg\Security\Trusted Locations").Count -eq 1)

    # --- ほかのアドインの OPEN は残して、次の番号に追加 ---
    New-ItemProperty -Path "$reg\Options" -Name 'OPEN1' -Value '/R "C:\other\other.xll"' -PropertyType String -Force | Out-Null
    Remove-ItemProperty -Path "$reg\Options" -Name 'OPEN'
    & (Join-Path $stage 'Install.ps1') @args32 | Out-Null
    $props = (Get-ItemProperty "$reg\Options").PSObject.Properties | Where-Object { $_.Name -match '^OPEN\d*$' }
    Check 'other add-in entry kept' (@($props | Where-Object { $_.Value -like '*other.xll*' }).Count -eq 1)
    Check 'ours re-registered in the free slot OPEN' (@($props | Where-Object { $_.Name -eq 'OPEN' -and $_.Value -like '*ExcelAi-AddIn.xll*' }).Count -eq 1)

    # --- 64 ビットでは 64 ビット版をコピー ---
    & (Join-Path $stage 'Install.ps1') -Bitness 64 -InstallDir $installDir -OfficeRegistryRoot $reg -NoTrustedLocation | Out-Null
    Check 'xll copied (64-bit)' ((Get-FileHash $target).Hash -eq (Get-FileHash $Xll64).Hash)

    # --- 足りないファイル ---
    Remove-Item (Join-Path $stage 'ExcelAi-AddIn64.xll')
    $threw = $false
    try { & (Join-Path $stage 'Install.ps1') -Bitness 64 -InstallDir $installDir -OfficeRegistryRoot $reg | Out-Null } catch { $threw = $true }
    Check 'missing 64-bit xll is reported' $threw

    # --- 外す ---
    & (Join-Path $stage 'Uninstall.ps1') -InstallDir $installDir -OfficeRegistryRoot $reg | Out-Null
    Check 'xll deleted' (-not (Test-Path $target))
    $after = (Get-ItemProperty "$reg\Options").PSObject.Properties | Where-Object { $_.Name -match '^OPEN\d*$' }
    Check 'our OPEN removed, other kept' (@($after).Count -eq 1 -and $after[0].Value -like '*other.xll*')
    Check 'trusted location removed' (@(Get-ChildItem "$reg\Security\Trusted Locations" -ErrorAction SilentlyContinue).Count -eq 0)
    Check 'data folder kept without -RemoveData' (Test-Path (Split-Path -Parent $installDir))
    & (Join-Path $stage 'Uninstall.ps1') -InstallDir $installDir -OfficeRegistryRoot $reg -RemoveData | Out-Null
    Check '-RemoveData deletes the data folder' (-not (Test-Path (Split-Path -Parent $installDir)))
}
finally {
    Remove-Item 'HKCU:\Software\ExcelAiInstallerTest' -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}

"不合格: $failures"
if ($failures -gt 0) { exit 1 }

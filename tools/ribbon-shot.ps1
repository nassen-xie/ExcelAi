<#
  [AI] タブ（リボン）の画面を撮る。別の Excel を新しく起動し、設定は一時フォルダー（EXCELAI_HOME）を使う。
  開いている Excel・本物の設定には触らない。
  使い方:  powershell -File tools\ribbon-shot.ps1 -Xll <packed.xll> [-Out ribbon.png] [-Language ja]
#>
param(
    [Parameter(Mandatory = $true)][string]$Xll,
    [string]$Out = (Join-Path (Get-Location) 'ribbon.png'),
    [string]$Language = 'ja'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'TestExcel.ps1')
Add-Type -ErrorAction SilentlyContinue -Namespace Win -Name Ribbon -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[void][Win.Ribbon]::SetProcessDPIAware()

$aiHome = Join-Path $env:TEMP ('excelai-ribbon-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $aiHome | Out-Null
@{ Language = $Language } | ConvertTo-Json | Set-Content -Path (Join-Path $aiHome 'settings.json') -Encoding UTF8
$existing = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$xl = $null
$xlPid = 0
try {
    $started = Start-TestExcel -DataHome $aiHome
    $xl = $started.Application
    $xlPid = $started.Pid
    if ($existing -contains $xlPid) { throw '既存の Excel を操作しようとしているため中止しました。' }
    [void]$xl.RegisterXLL((Resolve-Path $Xll).Path)
    $xl.Visible = $true
    $xl.WindowState = -4137
    [void][Win.Ribbon]::SetWindowPos([IntPtr]$xl.Hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003)   # 最前面（キー送信はしない）
    Start-Sleep -Seconds 4
    $root = [Windows.Automation.AutomationElement]::FromHandle([IntPtr]$xl.Hwnd)
    $cond = New-Object Windows.Automation.AndCondition((New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'AI')), (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)))
    $tab = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    if ($tab) { $tab.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Seconds 2 } else { '[AI] タブが見つかりません' }
    $rect = New-Object Win.Ribbon+RECT
    [void][Win.Ribbon]::GetWindowRect([IntPtr]$xl.Hwnd, [ref]$rect)
    $width = [Math]::Min($rect.Right - $rect.Left, 1500)
    $bmp = New-Object Drawing.Bitmap $width, 200
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $bmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png)
    "保存しました: $Out"
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

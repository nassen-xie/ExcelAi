<#
  セルの右クリックメニューに「AI で処理…」（DeepSeek のアイコン付き）が入っているか、画面で確かめる。
  別の Excel を新しく起動し、設定は一時フォルダー（EXCELAI_HOME）を使う。開いている Excel・本物の設定には触らない。
  使い方:  powershell -File tools\ctxmenu-shot.ps1 -Xll <packed.xll> [-Out ctxmenu.png] [-Language en]
#>
param(
    [Parameter(Mandatory = $true)][string]$Xll,
    [string]$Out = (Join-Path (Get-Location) 'ctxmenu.png'),
    [string]$Language = 'en'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'TestExcel.ps1')
Add-Type -ErrorAction SilentlyContinue -Namespace Win -Name Shot -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
[DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
'@
[void][Win.Shot]::SetProcessDPIAware()

$aiHome = Join-Path $env:TEMP ('excelai-ctx-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $aiHome | Out-Null
New-Item -ItemType Directory -Force (Split-Path -Parent $Out) | Out-Null
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
    # 前面に出すのは「最前面」指定で行う（キー送信はしない。ほかのアプリにキーが届かないように）
    [void][Win.Shot]::SetWindowPos([IntPtr]$xl.Hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003)
    Start-Sleep -Seconds 3
    $ws = $xl.ActiveWorkbook.Worksheets.Item(1)
    $ws.Range('B3').Select() | Out-Null
    $win = $xl.ActiveWindow
    $px = [int]$win.PointsToScreenPixelsX($ws.Range('B3').Left + 10)
    $py = [int]$win.PointsToScreenPixelsY($ws.Range('B3').Top + 6)
    $main = [TestExcelNative]::FindMainWindow($xlPid)
    $sheet = [TestExcelNative]::FindChild($main, 'EXCEL7')
    # 右クリックのメニューを、そのウィンドウに直接依頼する（WM_CONTEXTMENU）
    $pt = New-Object Win.Shot+POINT; $pt.X = $px; $pt.Y = $py
    [void][Win.Shot]::ScreenToClient($sheet, [ref]$pt)
    $lp = [IntPtr](($pt.Y -shl 16) -bor ($pt.X -band 0xFFFF))
    [void][Win.Shot]::PostMessage($sheet, 0x204, [IntPtr]2, $lp)
    Start-Sleep -Milliseconds 150
    [void][Win.Shot]::PostMessage($sheet, 0x205, [IntPtr]0, $lp)
    Start-Sleep -Seconds 2

    # 画面上のほかのウィンドウが閉じた瞬間などに UI オートメーションが失敗することがあるので、数回やり直す
    $names = @(); $hit = @()
    for ($try = 1; $try -le 5; $try++) {
        try {
            $desktop = [Windows.Automation.AutomationElement]::RootElement
            $items = $desktop.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::MenuItem)))
            $names = @(); $hit = @()
            foreach ($item in $items) {
                try { $n = $item.Current.Name } catch { continue }
                $names += $n
                if ($n -match 'Process with AI|AI で処理|用 AI 处理') { $hit += $item }
            }
            break
        }
        catch { Start-Sleep -Seconds 1 }
    }
    "メニュー項目 $($names.Count) 件、AI の項目: $($hit.Count)"
    if ($hit.Count -gt 0) {
        $r = $hit[0].Current.BoundingRectangle
        "位置: $([int]$r.Left),$([int]$r.Top) $([int]$r.Width)x$([int]$r.Height)"
        # メニュー全体を写す（AI の項目を中心に、上下に余裕を持たせる）
        $x = [Math]::Max(0, [int]$r.Left - 40); $y = [Math]::Max(0, [int]$r.Top - 200)
        $bmp = New-Object Drawing.Bitmap 520, 520
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
        $bmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png)
        "保存しました: $Out"
    } else {
        "AI の項目が見つかりません。先頭の項目: " + (($names | Select-Object -First 8) -join ' | ')
        exit 1
    }

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

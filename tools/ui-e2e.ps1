<#
  本物の Excel（新しいプロセス）で、[AI] タブのボタンを実際に押して、画面が開くか・エラーにならないかを確かめ、画面を PNG にする。
  使い方:  powershell -File tools\ui-e2e.ps1 -Xll <packed.xll のパス> -OutDir <PNG の保存先> [-Language ja]
  ・開いている Excel には触りません（新しいプロセスだけを起動・終了します）。
  ・設定は一時フォルダー（EXCELAI_HOME）。本物の設定・開いている Excel には触らない。
  終了コード: 0 = すべて開いた / 1 = 開かない画面があった
#>
param(
    [Parameter(Mandatory = $true)][string]$Xll,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$Language = 'ja',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
# 設定は一時フォルダー（EXCELAI_HOME）。本物の設定・開いている Excel には触らない
. (Join-Path $PSScriptRoot 'TestExcel.ps1')
$aiHome = Join-Path $env:TEMP ('excelai-e2e-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $OutDir | Out-Null
$existing = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$failures = 0

New-Item -ItemType Directory -Force $aiHome | Out-Null
@{ Language = $Language; ConsentAccepted = $true } | ConvertTo-Json | Set-Content -Path (Join-Path $aiHome 'settings.json') -Encoding UTF8
Add-Type -Namespace Win -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[void][Win.Native]::SetProcessDPIAware()
$AE = [Windows.Automation.AutomationElement]
$TS = [Windows.Automation.TreeScope]

function Find-ByName($root, $name, $type) {
    $c1 = New-Object Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    $c2 = New-Object Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $type)
    return $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.AndCondition($c1, $c2)))
}

function Save-Window($hwnd, $path) {
    $rect = New-Object Win.Native+RECT
    [void][Win.Native]::GetWindowRect([IntPtr]$hwnd, [ref]$rect)
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    $bmp = New-Object Drawing.Bitmap $w, $h
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
}

# ボタンを押す（ダイアログが閉じるまで戻らないことがあるので、別のスレッドで押す。PowerShell のスクリプトではなく C# の部品で）
Add-Type -ReferencedAssemblies UIAutomationClient, UIAutomationTypes, WindowsBase -TypeDefinition @'
using System.Threading.Tasks;
using System.Windows.Automation;
public static class UiaPress
{
    public static void Async(InvokePattern pattern)
    {
        Task.Run(() => { try { pattern.Invoke(); } catch { } });
    }
}
'@
function Press-Async($button) {
    [UiaPress]::Async($button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern))
}

# このプロセスのトップレベル ウィンドウ（EnumWindows。UI オートメーションでは持ち主つきのダイアログが見えないことがある）
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class WinList
{
    private delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    /// <summary>見えていて、題名のあるウィンドウ（ハンドル, 題名）。</summary>
    public static List<KeyValuePair<long, string>> For(int pid)
    {
        var result = new List<KeyValuePair<long, string>>();
        EnumWindows((h, l) =>
        {
            int p;
            GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h))
            {
                var title = new StringBuilder(256);
                GetWindowText(h, title, 256);
                if (title.Length > 0)
                {
                    result.Add(new KeyValuePair<long, string>(h.ToInt64(), title.ToString()));
                }
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static void Close(long handle)
    {
        PostMessage(new IntPtr(handle), 0x0010, IntPtr.Zero, IntPtr.Zero);   // WM_CLOSE
    }
}
'@

function Get-Windows($pid0) { return @([WinList]::For($pid0) | ForEach-Object { $_.Key }) }

# ボタンを押す前にはなかった新しいウィンドウ（ダイアログ）を待つ。戻り値は @{ Handle; Title }
function Wait-NewWindow($pid0, $before, $seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300
        foreach ($w in [WinList]::For($pid0)) {
            if ($before -notcontains $w.Key) { return @{ Handle = $w.Key; Title = $w.Value } }
        }
    }
    return $null
}

$xl = $null
$xlPid = 0
try {
    $started = Start-TestExcel -DataHome $aiHome
    $xl = $started.Application
    $xlPid = $started.Pid
    if ($existing -contains $xlPid) { throw '既存の Excel を操作しようとしているため中止しました。' }
    $xl.DisplayAlerts = $false
    $wb = $xl.Workbooks.Add()
    $ws = $wb.Worksheets.Item(1)
    $ws.Range('A1').Value2 = 'こんにちは'
    $ws.Range('A2').Value2 = '注文番号 1234'
    [void]$xl.RegisterXLL((Resolve-Path $Xll).Path)
    $xl.WindowState = -4137
    $xl.Visible = $true
    [void][Win.Native]::SetWindowPos([IntPtr]$xl.Hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003)   # 最前面（画面を撮るため。キー送信はしない）
    Start-Sleep -Seconds 4
    $main = $AE::FromHandle([IntPtr]$xl.Hwnd)
    $tab = Find-ByName $main 'AI' ([Windows.Automation.ControlType]::TabItem)
    if (-not $tab) { throw '[AI] タブが見つかりません' }
    $tab.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 2

    $labels = @{ ja = @{ settings = '設定'; help = 'ヘルプ'; usage = '使用量'; process = '選択範囲を処理' }; en = @{ settings = 'Settings'; help = 'Help'; usage = 'Usage'; process = 'Process selection' }; zh = @{ settings = '设置'; help = '帮助'; usage = '用量'; process = '处理选中区域' } }[$Language]
    foreach ($name in 'settings', 'help', 'usage', 'process') {
        $button = Find-ByName $main $labels[$name] ([Windows.Automation.ControlType]::Button)
        if (-not $button) { "FAIL  button '$($labels[$name])' not found"; $failures++; continue }
        $xl.ScreenUpdating = $true
        $before = Get-Windows $xlPid
        Press-Async $button
        $dialog = Wait-NewWindow $xlPid $before 10
        if ($dialog) {
            Start-Sleep -Milliseconds 600
            Save-Window $dialog.Handle (Join-Path $OutDir "$name-$Language.png")
            "PASS  $name opened: '$($dialog.Title)'"
            [WinList]::Close($dialog.Handle)
            Start-Sleep -Milliseconds 1500
            if ((Get-Windows $xlPid) -contains $dialog.Handle) { "FAIL  $name did not close"; $failures++ }
        } else {
            "FAIL  $name did not open a dialog"; $failures++
        }
    }

    # チャット（作業ウィンドウ）：押すと右側に表示される
    $chat = Find-ByName $main (@{ ja = 'チャット'; en = 'Chat'; zh = '聊天' }[$Language]) ([Windows.Automation.ControlType]::Button)
    if ($chat) {
        Press-Async $chat
        Start-Sleep -Seconds 3
        Save-Window $xl.Hwnd (Join-Path $OutDir "chat-$Language.png")
        "PASS  chat pane toggled"
    } else { "FAIL  chat button not found"; $failures++ }
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
"不合格: $failures"
if ($failures -gt 0) { exit 1 }

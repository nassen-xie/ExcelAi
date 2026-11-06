<#
  テスト用に、別の Excel プロセスを新しく起動して、その Application を返す（dot-source して使う）。
  ・環境変数 EXCELAI_HOME を付けて起動するので、アドインの設定・キャッシュ・ログはその一時フォルダーに入る
    （本物の %APPDATA%\ExcelAi と、開いている Excel には触らない）。
  ・COM の New-Object ではなく excel.exe を直接起動する（COM 経由だと環境変数が渡らないため）。
    起動したプロセスの Application は、そのウィンドウから取り出す（ほかの Excel と取り違えない）。
#>
Add-Type -ErrorAction SilentlyContinue -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class TestExcelNative
{
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("oleacc.dll")] public static extern int AccessibleObjectFromWindow(IntPtr hWnd, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object ptr);

    public static IntPtr FindMainWindow(int pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            int p; GetWindowThreadProcessId(h, out p);
            var name = new StringBuilder(64); GetClassName(h, name, 64);
            if (p == pid && name.ToString() == "XLMAIN") { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static IntPtr FindChild(IntPtr parent, string className)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, (h, l) =>
        {
            var name = new StringBuilder(64); GetClassName(h, name, 64);
            if (name.ToString() == className) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // EXCEL7 ウィンドウの OLE オブジェクト（Excel のウィンドウ）を取り出す
    public static object WindowObject(IntPtr excel7)
    {
        Guid iid = new Guid("00020400-0000-0000-C000-000000000046");
        object o;
        int hr = AccessibleObjectFromWindow(excel7, 0xFFFFFFF0, ref iid, out o);
        return hr == 0 ? o : null;
    }
}
'@

function Start-TestExcel {
    param([Parameter(Mandatory = $true)][string]$DataHome, [int]$TimeoutSeconds = 60)
    New-Item -ItemType Directory -Force $DataHome | Out-Null
    # ブックを 1 つ開いて起動する（ブックがないと、ウィンドウから Application を取り出せない）
    $seed = Join-Path $DataHome "seed.csv"
    Set-Content -Path $seed -Value "x" -Encoding ASCII
    $excelArgs = @('/x', '/e', ('"' + $seed + '"'))
    $before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $previous = $env:EXCELAI_HOME
    $env:EXCELAI_HOME = $DataHome
    try {
        # /x = 新しいインスタンス、/e = スプラッシュ・新規ブックなし
        $proc = Start-Process -FilePath 'excel.exe' -ArgumentList $excelArgs -PassThru -WindowStyle Minimized
    }
    finally {
        $env:EXCELAI_HOME = $previous
    }

    if ($before -contains $proc.Id) { throw '既存の Excel と同じプロセスです。中止します。' }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $app = $null
    while ((Get-Date) -lt $deadline -and -not $app) {
        Start-Sleep -Milliseconds 500
        # 「/x」で渡した起動プロセスが別の PID に変わることがあるため、起動後に増えた Excel のうち、自分のものを探す
        $candidates = @(Get-Process excel -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id } | ForEach-Object { $_.Id })
        foreach ($candidate in $candidates) {
            $main = [TestExcelNative]::FindMainWindow($candidate)
            if ($main -eq [IntPtr]::Zero) { continue }
            $child = [TestExcelNative]::FindChild($main, 'EXCEL7')
            if ($child -eq [IntPtr]::Zero) { continue }
            $window = [TestExcelNative]::WindowObject($child)
            if ($window) { $app = $window.Application; $pidFound = $candidate; break }
        }
    }

    if (-not $app) { Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force; throw 'テスト用の Excel に接続できませんでした。' }
    $app.DisplayAlerts = $false
    "テスト用 Excel: PID $pidFound（EXCELAI_HOME=$DataHome）" | Out-Host
    return [pscustomobject]@{ Application = $app; Pid = $pidFound }
}

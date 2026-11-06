<#
  実際の Excel でアドインを読み込んで確かめる（本物の API には接続しない。Messages API のふりをする小さなサーバーを使う）。
  ・Excel は新しい別のプロセスで起動して、終わったらそのプロセスだけを終了します（開いている Excel・ブックには触りません）。
  ・設定は %APPDATA%\ExcelAi を使うため、すでに settings.json がある場合は中止します（-Force で上書き。テスト後に消します）。
  使い方:  powershell -File tools\smoke-test.ps1 -Xll <packed.xll のパス> [-Port 18777]
  終了コード: 0 = すべて合格 / 1 = 不合格あり
#>
param(
    [Parameter(Mandatory = $true)][string]$Xll,
    [int]$Port = 18777,
    [ValidateSet('deepseek', 'anthropic')][string]$Provider = 'deepseek',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
# 設定・キャッシュは一時フォルダー（EXCELAI_HOME）を使う。本物の %APPDATA%\ExcelAi には触らない
. (Join-Path $PSScriptRoot 'TestExcel.ps1')
$aiHome = Join-Path $env:TEMP ('excelai-smoke-' + [guid]::NewGuid().ToString('N'))

$existing = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
"既存の Excel: $($existing -join ', ')"

$script:failures = 0
function Check($name, $actual, $pattern) {
    if ("$actual" -match $pattern) { "PASS  $name" } else { "FAIL  $name : [$actual] !~ [$pattern]"; $script:failures++ }
}

# ---- Messages API のふりをするサーバー（別のジョブ） ----
$serverLog = Join-Path $env:TEMP 'excelai-smoke-server.log'
Remove-Item $serverLog -ErrorAction SilentlyContinue
$job = Start-Job -ArgumentList $Port, $serverLog -ScriptBlock {
    param($port, $log)
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://127.0.0.1:$port/")
    $listener.Start()
    while ($true) {
        $ctx = $listener.GetContext()
        $reader = New-Object System.IO.StreamReader($ctx.Request.InputStream, [Text.Encoding]::UTF8)
        $body = $reader.ReadToEnd()
        Add-Content -Path $log -Value ($ctx.Request.Url.AbsolutePath + ' ' + $body) -Encoding UTF8
        $json = $body | ConvertFrom-Json
        $openAi = $ctx.Request.Url.AbsolutePath -like '*chat/completions'
        $last = $json.messages[-1].content
        if ($last -isnot [string]) { $last = ($last | ForEach-Object { $_.text }) -join '' }
        $reply = if ($last -match 'FAILME') { $null } else { 'ECHO[' + ($last.Substring(0, [Math]::Min(60, $last.Length)) -replace "[\r\n\t]", ' ') + ']' }
        if ($null -eq $reply) {
            $payload = '{"type":"error","error":{"type":"invalid_request_error","message":"simulated failure"}}'
            $ctx.Response.StatusCode = 400
        } elseif ($openAi) {
            $payload = (@{ id = 'c_smoke'; object = 'chat.completion'; model = 'deepseek-flash'; choices = @(@{ index = 0; message = @{ role = 'assistant'; content = $reply }; finish_reason = 'stop' }); usage = @{ prompt_tokens = 20; completion_tokens = 7; prompt_cache_hit_tokens = 0 } } | ConvertTo-Json -Depth 6 -Compress)
        } else {
            $payload = (@{ id = 'msg_smoke'; type = 'message'; role = 'assistant'; model = 'claude-opus-5-5'; content = @(@{ type = 'text'; text = $reply }); stop_reason = 'end_turn'; stop_sequence = $null; usage = @{ input_tokens = 20; output_tokens = 7 } } | ConvertTo-Json -Depth 6 -Compress)
        }
        $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
        $ctx.Response.ContentType = 'application/json'
        $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        $ctx.Response.Close()
    }
}
Start-Sleep -Seconds 1

# ---- テスト用の設定（API キーは DPAPI で暗号化） ----
New-Item -ItemType Directory -Force $aiHome | Out-Null
$protected = [Convert]::ToBase64String([Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes('sk-ant-api03-smoketest'), [Text.Encoding]::UTF8.GetBytes('ExcelAi.v1'), 'CurrentUser'))
$model = if ($Provider -eq 'deepseek') { 'deepseek-flash' } else { 'claude-opus-5-5' }
$settings = @{ Provider = $Provider; Model = $model; Language = 'en'; BaseUrl = "http://127.0.0.1:$Port"; ConsentAccepted = $true; UseCache = $true; ApiKeysProtected = @{ $Provider = $protected }; TimeoutSeconds = 30; MaxConcurrency = 3 }
$settings | ConvertTo-Json | Set-Content -Path (Join-Path $aiHome 'settings.json') -Encoding UTF8

$results = [ordered]@{}
$xl = $null
$xlPid = 0
try {
    $started = Start-TestExcel -DataHome $aiHome
    $xl = $started.Application
    $xlPid = $started.Pid
    "このテストの Excel: PID $xlPid（既存: $($existing -join ', ')）"
    if ($existing -contains $xlPid) { throw '既存の Excel を操作しようとしているため中止しました。' }

    $results['RegisterXLL'] = $xl.RegisterXLL((Resolve-Path $Xll).Path)
    $wb = $xl.Workbooks.Add()
    $ws = $wb.Worksheets.Item(1)

    $ws.Range('A1').Value2 = 'こんにちは世界'
    $ws.Range('A2').Value2 = 'order 1234'
    $ws.Range('A3').Value2 = 'FAILME please'
    $ws.Range('C1').Formula = '=AI_VERSION()'
    $ws.Range('B1').Formula = '=AI("Translate to English:",A1)'
    $ws.Range('B2').Formula = '=AI_TRANSLATE(A2,"French")'
    $ws.Range('B3').Formula = '=AI("hello",A3)'
    $ws.Range('B4').Formula = '=AI_CLASSIFY(A2,"alpha,beta")'
    $ws.Range('B5').Formula = '=AI("same",A1)'
    $ws.Range('B6').Formula = '=AI("same",A1)'

    $deadline = (Get-Date).AddSeconds(40)
    do {
        Start-Sleep -Milliseconds 500
        $pending = @('B1', 'B2', 'B3', 'B4', 'B5', 'B6' | Where-Object { "$($ws.Range($_).Text)" -match '^(#N/A|.*処理中|.*working)' })
    } while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline)

    foreach ($cell in 'C1', 'B1', 'B2', 'B3', 'B4', 'B5', 'B6') { $results[$cell] = "$($ws.Range($cell).Text)" }
    $results['requests'] = @(Get-Content $serverLog -ErrorAction SilentlyContinue).Count
    Check 'register' $results['RegisterXLL'] 'True'
    Check 'AI_VERSION' $results['C1'] '^Excel AI Assistant 1\.2\.1$'
    Check 'AI async result' $results['B1'] '^ECHO\[Translate to English:'
    Check 'AI_TRANSLATE' $results['B2'] '^ECHO\[Translate the following text into French'
    Check 'AI error (400)' $results['B3'] '^#AI! The request was rejected'
    Check 'AI_CLASSIFY (full prompt reached the server)' ((Get-Content $serverLog -Raw -Encoding UTF8)) 'categories: alpha, beta'
    Check 'two identical formulas: 1 request' $results['requests'] '^5$'

    # ---- 選択範囲の処理（画面なし） ----
    $ws.Range('E1').Value2 = 'apple'
    $ws.Range('E3').Value2 = 'cherry'
    $ws.Range('E4').Value2 = 'FAILME banana'
    $ws.Range('E5').Value2 = 'melon'
    $ws.Activate()
    [void]$ws.Range('E1:E5').Select()
    $o = '{"Template":"Fix {{value}} (row {{row}})","RowMode":false,"Output":0,"SkipEmpty":true,"SkipDone":true}'
    $r1 = $xl.Run('AI_SELFTEST_PROCESS', $o)
    Check 'process: items (skip empty)' $r1 'items=4 written=4 failed=1'
    Check 'process: right of selection F1' "$($ws.Range('F1').Text)" '^ECHO\[Fix apple \(row 1\)\]'
    Check 'process: empty cell skipped' "$($ws.Range('F2').Text)" '^$'
    Check 'process: row placeholder' "$($ws.Range('F3').Text)" 'Fix cherry \(row 3\)'
    Check 'process: failure marked' "$($ws.Range('F4').Text)" '^#AI! '
    $r2 = $xl.Run('AI_SELFTEST_PROCESS', $o)
    Check 'process: resume reruns only failed' $r2 'items=1 written=1 failed=1'
    $ws.Range('E4').Value2 = 'banana'
    $r3 = $xl.Run('AI_SELFTEST_PROCESS', $o)
    Check 'process: failed row fixed on rerun' $r3 'items=1 written=1 failed=0'
    Check 'process: fixed text' "$($ws.Range('F4').Text)" 'Fix banana'
    $r4 = $xl.Run('AI_SELFTEST_PROCESS', $o)
    Check 'process: nothing left' $r4 'items=0 written=0 failed=0'

    # 行ごと（{{H}} {{I}} で列を使う）・指定した列
    $ws.Range('H1').Value2 = 'Tokyo'; $ws.Range('I1').Value2 = 'Japan'
    $ws.Range('H2').Value2 = 'Paris'; $ws.Range('I2').Value2 = 'France'
    [void]$ws.Range('H1:I2').Select()
    $row = '{"Template":"{{H}} is in {{I}} / {{row}}","RowMode":true,"Output":1,"ColumnLetter":"Z"}'
    Check 'row mode' ($xl.Run('AI_SELFTEST_PROCESS', $row)) 'items=2 written=2 failed=0'
    Check 'row mode column Z' "$($ws.Range('Z2').Text)" 'Paris is in France / 2'

    # 元のセルを置き換える
    $ws.Range('K1').Value2 = 'alpha'; $ws.Range('K2').Value2 = 'beta'
    [void]$ws.Range('K1:K2').Select()
    $rep = '{"Template":"Up {{value}}","RowMode":false,"Output":2}'
    Check 'replace' ($xl.Run('AI_SELFTEST_PROCESS', $rep)) 'items=2 written=2 failed=0'
    Check 'replace text' "$($ws.Range('K1').Text)" '^ECHO\[Up alpha\]'

    # 使用済みの範囲の外は処理しない（AB10:AB11 は範囲の外）
    [void]$ws.Range('AB10:AB11').Select()
    Check 'empty selection' ($xl.Run('AI_SELFTEST_PROCESS', $o)) '^ERROR'
    $results['requests-final'] = @(Get-Content $serverLog -ErrorAction SilentlyContinue).Count
}
finally {
    if ($xl) {
        try { $xl.DisplayAlerts = $false; $xl.Quit() } catch { }
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
        Start-Sleep -Seconds 2
        # このテストで起動した Excel だけを、まだ残っていれば終了する
        if ($xlPid -gt 0 -and ($existing -notcontains $xlPid)) { Get-Process -Id $xlPid -ErrorAction SilentlyContinue | Stop-Process -Force }
    }
    Stop-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    foreach ($id in (Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })) { if ($existing -contains $id) { "既存の Excel $id は残っています" } }
}

$results.GetEnumerator() | ForEach-Object { '{0,-14} {1}' -f $_.Key, $_.Value }
"不合格: $script:failures"

# ---- 後片付け（テストで作ったものだけ） ----
foreach ($f in 'settings.json', 'cache.jsonl', 'usage.json') { Remove-Item (Join-Path $aiHome $f) -ErrorAction SilentlyContinue }
Remove-Item $aiHome -Recurse -Force -ErrorAction SilentlyContinue
if ($script:failures -gt 0) { exit 1 }

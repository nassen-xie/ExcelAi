<#
  接続の診断。アドインと同じ部品（ExcelAi.Core の OpenAiLlm）で、無効なキーを使って DeepSeek（などの接続先）に 1 回だけ要求を送り、
  「通信できる / 認証で断られる（= 通信は正常）」を調べる。本物のキーは使わない。
  使い方:  powershell -File tools\net-probe.ps1 [-Provider deepseek] [-BaseUrl <接続先>]
#>
param(
    [string]$Provider = 'deepseek',
    [string]$BaseUrl = '',
    [string]$Bin = (Join-Path $PSScriptRoot '..\src\ExcelAi.Addin\bin\Release\net48')
)

$ErrorActionPreference = 'Stop'
$env:EXCELAI_HOME = Join-Path $env:TEMP ('excelai-probe-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $env:EXCELAI_HOME | Out-Null
$script:probeBin = (Resolve-Path $Bin).Path
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($sender, $args2)
    $name = (New-Object Reflection.AssemblyName($args2.Name)).Name
    $file = Join-Path $script:probeBin ($name + '.dll')
    if (Test-Path $file) { return [Reflection.Assembly]::LoadFrom($file) }
    return $null
})
foreach ($dll in 'System.Memory', 'System.Buffers', 'System.Numerics.Vectors', 'System.Runtime.CompilerServices.Unsafe', 'System.Threading.Tasks.Extensions', 'Microsoft.Bcl.AsyncInterfaces', 'System.Text.Encodings.Web', 'System.Text.Json', 'ExcelAi.Core') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $Bin "$dll.dll"))
}

$settings = New-Object ExcelAi.AiSettings
$settings.Provider = $Provider
$settings.ApiKey = 'sk-invalid-probe'
$settings.BaseUrl = $BaseUrl
$settings.TimeoutSeconds = 30
Write-Host ("OS: {0} / .NET: {1} / TLS 設定: {2}" -f [Environment]::OSVersion.Version, [Environment]::Version, [Net.ServicePointManager]::SecurityProtocol)
Write-Host ("接続先: {0}" -f $settings.ResolvedBaseUrl)
$proxy = [Net.WebRequest]::GetSystemWebProxy().GetProxy([Uri]($settings.ResolvedBaseUrl))
Write-Host ("システムのプロキシ: {0}" -f $proxy)

$llm = New-Object ExcelAi.RoutingLlm ([Func[ExcelAi.AiSettings]]{ $settings })
$request = New-Object ExcelAi.LlmRequest
$request.Model = $settings.Model
$request.MaxTokens = 16
$request.Messages = [ExcelAi.ChatMessage[]]@(New-Object ExcelAi.ChatMessage($true, 'ping'))
try {
    $result = $llm.CompleteAsync($request, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Write-Host "成功（想定外）: $($result.Text)"
}
catch {
    $e = $_.Exception
    while ($e.InnerException) { $e = $e.InnerException }
    Write-Host ("結果: {0}: {1}" -f $e.GetType().Name, $e.Message)
    if ($e -is [ExcelAi.AiException]) { Write-Host ("種類: {0} / HTTP: {1} / 詳細: {2}" -f $e.Kind, $e.Status, $e.Detail) }
    $inner = $_.Exception
    while ($inner) { Write-Host ("  - {0}: {1}" -f $inner.GetType().FullName, $inner.Message); $inner = $inner.InnerException }
}
Remove-Item $env:EXCELAI_HOME -Recurse -Force -ErrorAction SilentlyContinue

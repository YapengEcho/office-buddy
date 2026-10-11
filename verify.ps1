param([switch]$Overlay)
$ErrorActionPreference = 'Stop'
$taskApp = Join-Path $PSScriptRoot 'OfficeBuddyStudio.exe'
if (-not (Test-Path -LiteralPath $taskApp)) { throw '请先运行 build.ps1。' }
$taskResults = Join-Path $PSScriptRoot 'work\verification'
New-Item -ItemType Directory -Force -Path $taskResults | Out-Null
function Invoke-BuddyCheck([string]$flag,[string]$output,[string]$theme = '') {
    $taskArgs = @($flag,('"' + $output + '"'))
    if ($theme) { $taskArgs += $theme }
    $taskProcess = Start-Process -FilePath $taskApp -ArgumentList $taskArgs -WindowStyle Hidden -PassThru -Wait
    if ($taskProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw ('验证失败：' + $flag + ' ' + $theme) }
    $taskReport = Get-Content -LiteralPath $output -Raw -Encoding UTF8
    Write-Output $taskReport
    if ($taskReport -match 'FAIL:') { throw ('检查未通过：' + $theme) }
}
Invoke-BuddyCheck '--self-test' (Join-Path $taskResults 'core.txt')
if ($Overlay) {
    if (Get-Process -Name OfficeBuddyStudio -ErrorAction SilentlyContinue) { throw '请先从托盘正常退出搭子，再运行独立窗口检查。' }
    foreach ($taskTheme in @('pixel','cream','midnight','nailong')) {
        Invoke-BuddyCheck '--overlay-test' (Join-Path $taskResults ('overlay-' + $taskTheme + '.txt')) $taskTheme
    }
}

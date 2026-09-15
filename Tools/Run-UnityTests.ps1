param(
    [Parameter(Mandatory = $true)]
    [string]$EditorPath,

    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Suite = 'EditMode'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = Join-Path $repositoryRoot 'Builds\TestResults'
$logsDirectory = Join-Path $repositoryRoot 'Builds\Logs'
New-Item -ItemType Directory -Force -Path $resultsDirectory, $logsDirectory | Out-Null

$resultPath = Join-Path $resultsDirectory ($Suite.ToLowerInvariant() + '.xml')
$logPath = Join-Path $logsDirectory ($Suite.ToLowerInvariant() + '-tests.log')
$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $repositoryRoot,
    '-runTests',
    '-testPlatform', $Suite,
    '-testResults', $resultPath,
    '-logFile', $logPath
)

$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(600000)) {
    Stop-Process -Id $process.Id -Force
    throw "Unity $Suite tests timed out after 10 minutes. See $logPath."
}
$process.Refresh()
if ($process.ExitCode -ne 0) {
    Write-Error "Unity $Suite tests failed with exit code $($process.ExitCode). See $logPath and $resultPath."
}

Write-Host "Unity $Suite tests passed. Results: $resultPath"
exit $process.ExitCode

param(
    [Parameter(Mandatory = $true)]
    [string]$EditorPath,
    [ValidateSet('Development', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Archive
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$method = if ($Configuration -eq 'Release') {
    'CockroachFantasia.Editor.ProjectSetup.BuildWindowsRelease'
} else {
    'CockroachFantasia.Editor.ProjectSetup.BuildWindowsDevelopment'
}
$folder = Join-Path $repository "Builds\Windows$Configuration"
$log = Join-Path $repository "Builds\Logs\windows-$($Configuration.ToLowerInvariant()).log"
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
$arguments = @('-batchmode', '-nographics', '-projectPath', $repository, '-executeMethod', $method, '-logFile', $log)
$process = Start-Process -FilePath (Resolve-Path $EditorPath).Path -ArgumentList $arguments `
    -WindowStyle Hidden -PassThru
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Unity $Configuration build failed. Inspect $log." }

$executable = Join-Path $folder 'CockroachFantasia.exe'
if (-not (Test-Path $executable)) { throw "Build did not produce $executable." }
if ($Configuration -eq 'Release') {
    $doNotShip = Join-Path $folder 'Cockroach Fantasia_BurstDebugInformation_DoNotShip'
    if (Test-Path $doNotShip) { Remove-Item -LiteralPath $doNotShip -Recurse -Force }
}
$commit = (git -C $repository rev-parse HEAD).Trim()
$manifest = [ordered]@{
    product = 'Cockroach Fantasia'
    version = '0.1.0-rc.1'
    platform = 'Windows x86-64'
    configuration = $Configuration
    unity = '6000.3.21f1'
    commit = $commit
}
$manifest | ConvertTo-Json | Set-Content -Path (Join-Path $folder 'release-manifest.json') -Encoding utf8

if ($Archive) {
    $artifactDirectory = Join-Path $repository 'Builds\Artifacts'
    New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
    $archivePath = Join-Path $artifactDirectory "CockroachFantasia-0.1.0-rc.1-Windows-$Configuration.zip"
    Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $archivePath -Force
    $hash = (Get-FileHash -Algorithm SHA256 -Path $archivePath).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $archivePath -Leaf)" | Set-Content -Path "$archivePath.sha256" -Encoding ascii
    Write-Host "Artifact: $archivePath"
    Write-Host "SHA-256: $hash"
}

Write-Host "$Configuration build succeeded: $executable"

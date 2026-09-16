param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Host', 'Client')]
    [string]$Role,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string]$ParticipantId,

    [Parameter(Mandatory = $true)]
    [string]$NetworkLabel,

    [string]$PlayerPath = (Join-Path $PSScriptRoot '..\Builds\WindowsDevelopment\CockroachFantasia.exe'),
    [string]$RoomCode,
    [ValidateRange(60, 3600)][int]$DurationSeconds = 600,
    [switch]$Rendered,
    [ValidateRange(640, 7680)][int]$Width = 1920,
    [ValidateRange(480, 4320)][int]$Height = 1080,
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\Builds\Diagnostics\Physical')
)

$ErrorActionPreference = 'Stop'
$resolvedPlayer = (Resolve-Path -LiteralPath $PlayerPath).Path
$timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$safeComputer = $env:COMPUTERNAME -replace '[^A-Za-z0-9_-]', '-'
$outputDirectory = Join-Path $OutputRoot "$timestamp-$ParticipantId-$safeComputer"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$roomCodePath = Join-Path $outputDirectory 'room-code.txt'
$logPath = Join-Path $outputDirectory 'player.log'
$evidencePath = Join-Path $outputDirectory 'evidence.json'

function Get-SafeCimValue {
    param([string]$ClassName, [scriptblock]$Projection)
    try {
        return @(Get-CimInstance $ClassName | ForEach-Object $Projection)
    } catch {
        return @("Unavailable: $($_.Exception.Message)")
    }
}

$manifestPath = Join-Path (Split-Path -Parent $resolvedPlayer) 'release-manifest.json'
$manifest = if (Test-Path -LiteralPath $manifestPath) {
    Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} else {
    $null
}
if ($manifest -and $manifest.configuration -ne 'Development') {
    throw "Physical Relay diagnostics require a Development build; manifest reports $($manifest.configuration)."
}
$evidence = [ordered]@{
    capturedUtc = (Get-Date).ToUniversalTime().ToString('o')
    participantId = $ParticipantId
    role = $Role
    networkLabel = $NetworkLabel
    computerName = $env:COMPUTERNAME
    operatingSystem = Get-SafeCimValue Win32_OperatingSystem { "$($_.Caption) $($_.Version)" }
    cpu = Get-SafeCimValue Win32_Processor { $_.Name }
    gpu = Get-SafeCimValue Win32_VideoController { "$($_.Name) | driver $($_.DriverVersion)" }
    memoryBytes = Get-SafeCimValue Win32_ComputerSystem { [long]$_.TotalPhysicalMemory }
    playerPath = $resolvedPlayer
    playerSha256 = (Get-FileHash -LiteralPath $resolvedPlayer -Algorithm SHA256).Hash.ToLowerInvariant()
    buildManifest = $manifest
    durationSeconds = $DurationSeconds
    rendered = $Rendered.IsPresent
    requestedResolution = if ($Rendered) { "${Width}x${Height}" } else { 'Headless' }
    roomCode = $null
    exitCode = $null
    connectedFourPlayers = $false
    success = $false
    suspiciousLogLines = @()
}
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8

if ($Role -eq 'Client') {
    $normalizedCode = ($RoomCode -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
    if ($normalizedCode.Length -ne 6) {
        throw 'Client -RoomCode must contain exactly six letters or digits.'
    }
    Set-Content -LiteralPath $roomCodePath -Value $normalizedCode -NoNewline
}

$profile = "phys-$ParticipantId-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$profile = $profile.Substring(0, [Math]::Min(30, $profile.Length))
$switch = if ($Role -eq 'Host') { '-relayHostSmoke' } else { '-relayJoinSmoke' }
$holdSeconds = if ($Role -eq 'Host') { 30 } else { 0 }
$arguments = "-batchmode -playerProfile $profile $switch -roomCodeFile `"$roomCodePath`" " +
             "-relayDurationSeconds $DurationSeconds -relayPostSuccessHoldSeconds $holdSeconds " +
             "-logFile `"$logPath`""
if ($Rendered) {
    $arguments += " -screen-fullscreen 0 -screen-width $Width -screen-height $Height"
    $player = Start-Process -FilePath $resolvedPlayer -ArgumentList $arguments -PassThru
} else {
    $arguments += ' -nographics'
    $player = Start-Process -FilePath $resolvedPlayer -ArgumentList $arguments -WindowStyle Hidden -PassThru
}

if ($Role -eq 'Host') {
    $codeDeadline = (Get-Date).AddSeconds(90)
    while (!(Test-Path -LiteralPath $roomCodePath) -and (Get-Date) -lt $codeDeadline -and !$player.HasExited) {
        Start-Sleep -Milliseconds 500
        $player.Refresh()
    }
    if (!(Test-Path -LiteralPath $roomCodePath)) {
        if (!$player.HasExited) { Stop-Process -Id $player.Id -Force }
        throw "Host did not publish a room code. Inspect $logPath"
    }
    $RoomCode = (Get-Content -LiteralPath $roomCodePath -Raw).Trim()
    Write-Host "ROOM CODE: $RoomCode"
    Write-Host 'Send this code to the three client testers immediately.'
}

$evidence.roomCode = if ($RoomCode) { $RoomCode.ToUpperInvariant() } else { $null }
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8
$timeoutMilliseconds = ($DurationSeconds + 240) * 1000
if (!$player.WaitForExit($timeoutMilliseconds)) {
    Stop-Process -Id $player.Id -Force
    throw "Physical Relay diagnostic timed out. Inspect $logPath"
}
$player.Refresh()

$successRole = $Role.ToLowerInvariant()
$connected = Select-String -LiteralPath $logPath -Pattern 'RELAY_DIAGNOSTIC_FOUR_PLAYERS_CONNECTED' -Quiet
$success = Select-String -LiteralPath $logPath -Pattern "RELAY_DIAGNOSTIC_SUCCESS role=$successRole" -Quiet
$suspicious = @(Select-String -LiteralPath $logPath -Pattern @(
    'RELAY_DIAGNOSTIC_FAILED',
    'NetworkVariable is written',
    'Exception:',
    '\[Netcode\].*(Error|Warning)'
) | ForEach-Object Line | Select-Object -Unique)

$evidence.exitCode = $player.ExitCode
$evidence.connectedFourPlayers = $connected
$evidence.success = $success -and $player.ExitCode -eq 0 -and $suspicious.Count -eq 0
$evidence.suspiciousLogLines = $suspicious
$evidence.completedUtc = (Get-Date).ToUniversalTime().ToString('o')
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8

Write-Host "Evidence: $evidencePath"
Write-Host "Player log: $logPath"
if (!$evidence.success) {
    throw "Physical Relay diagnostic failed validation (exit=$($player.ExitCode), connected=$connected, marker=$success, suspicious=$($suspicious.Count))."
}
Write-Host "PHYSICAL_RELAY_SUCCESS participant=$ParticipantId role=$Role duration=$DurationSeconds"

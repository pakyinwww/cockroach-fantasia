param(
    [string]$PlayerPath = (Join-Path $PSScriptRoot '..\Builds\WindowsDevelopment\CockroachFantasia.exe'),
    [string]$OutputName = 'manual',
    [ValidateRange(0, 1000)][int]$OneWayDelayMs = 0,
    [ValidateRange(0, 100)][int]$PacketLossPercent = 0,
    [switch]$TeleportViolation,
    [switch]$MatchState,
    [switch]$SkipMovement
)

$ErrorActionPreference = 'Stop'
$resolvedPlayer = (Resolve-Path $PlayerPath).Path
$outputDirectory = Join-Path $PSScriptRoot "..\Builds\Diagnostics\Movement\$OutputName"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$roomCodePath = Join-Path $outputDirectory 'room-code.txt'
Remove-Item -LiteralPath $roomCodePath -Force -ErrorAction SilentlyContinue

$common = "-batchmode -nographics -rosterExpectedPlayers 4 -requireValidDistribution " +
          "-rosterReady -rosterExpectKitchen -simulateDelayMs $OneWayDelayMs " +
          "-simulateLossPercent $PacketLossPercent"
if (-not $SkipMovement) { $common += ' -movementSmoke' }
if ($MatchState) { $common += ' -matchStateSmoke' }
$players = @(
    @{ Key = 'host'; Name = 'Human'; Seat = 'Human'; Extra = '-rosterHostSmoke -rosterStartMatch -rosterDurationSeconds 8' },
    @{ Key = 'c1'; Name = 'RoachA'; Seat = 'CockroachOne'; Extra = '-rosterJoinSmoke -rosterDurationSeconds 1' },
    @{ Key = 'c2'; Name = 'RoachB'; Seat = 'CockroachTwo'; Extra = '-rosterJoinSmoke -rosterDurationSeconds 1' },
    @{ Key = 'c3'; Name = 'RoachC'; Seat = 'CockroachThree'; Extra = '-rosterJoinSmoke -rosterDurationSeconds 1' }
)

if ($TeleportViolation) {
    $players[1].Extra += ' -movementTeleportViolation'
}

$processes = @()
for ($index = 0; $index -lt $players.Count; $index++) {
    $player = $players[$index]
    $logPath = Join-Path $outputDirectory ($player.Key + '.log')
    $safeOutputName = $OutputName -replace '[^a-zA-Z0-9_-]', '-'
    $profilePrefix = "mv-$safeOutputName"
    $profilePrefix = $profilePrefix.Substring(0, [Math]::Min(24, $profilePrefix.Length))
    $profile = "$profilePrefix-$($player.Key)"
    $arguments = "$common $($player.Extra) -playerProfile $profile -roomCodeFile $roomCodePath " +
                 "-rosterName $($player.Name) -rosterSeat $($player.Seat) -logFile $logPath"
    $processes += Start-Process -FilePath $resolvedPlayer -ArgumentList $arguments -WindowStyle Hidden -PassThru

    if ($index -eq 0) {
        $roomDeadline = (Get-Date).AddSeconds(45)
        while (-not (Test-Path $roomCodePath) -and (Get-Date) -lt $roomDeadline) {
            Start-Sleep -Milliseconds 250
        }
        if (-not (Test-Path $roomCodePath)) { throw 'Host did not publish a room code.' }
    } else {
        Start-Sleep -Seconds 1
    }
}

$deadline = (Get-Date).AddMinutes(3)
foreach ($process in $processes) {
    $remainingMs = [Math]::Max(1, [int]($deadline - (Get-Date)).TotalMilliseconds)
    if (-not $process.WaitForExit($remainingMs)) {
        Stop-Process -Id $process.Id -Force
        throw "Movement diagnostic process $($process.Id) timed out."
    }
}

$results = for ($index = 0; $index -lt $players.Count; $index++) {
    $logPath = Join-Path $outputDirectory ($players[$index].Key + '.log')
    [pscustomobject]@{
        Role = $players[$index].Seat
        ExitCode = $processes[$index].ExitCode
        Movement = (Select-String -Path $logPath -Pattern 'MOVEMENT_DIAGNOSTIC_SUCCESS' | ForEach-Object Line) -join ''
        Correction = (Select-String -Path $logPath -Pattern 'MOVEMENT_CORRECTION_SUCCESS' | ForEach-Object Line) -join ''
        MatchState = (Select-String -Path $logPath -Pattern 'MATCH_STATE_DIAGNOSTIC' | ForEach-Object Line) -join ''
    }
}

$results | Format-Table -AutoSize
if ($processes.Where({ $_.ExitCode -ne 0 }).Count -gt 0 -or
    (-not $SkipMovement -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Movement) }).Count -gt 0) -or
    ($TeleportViolation -and [string]::IsNullOrWhiteSpace($results[1].Correction)) -or
    ($MatchState -and $results.Where({ [string]::IsNullOrWhiteSpace($_.MatchState) }).Count -gt 0)) {
    throw "Movement diagnostic failed. Inspect $outputDirectory."
}

if ($MatchState) {
    $deadlines = @($results.MatchState | ForEach-Object {
        if ($_ -match 'deadline=(?<deadline>[0-9.]+)') { $Matches.deadline }
    } | Select-Object -Unique)
    if ($deadlines.Count -ne 1) {
        throw "Clients did not receive one shared match deadline. Inspect $outputDirectory."
    }
    Write-Host "Shared authoritative deadline: $($deadlines[0])"
}

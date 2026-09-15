param(
    [string]$PlayerPath = (Join-Path $PSScriptRoot '..\Builds\WindowsDevelopment\CockroachFantasia.exe'),
    [string]$OutputName = 'manual',
    [ValidateRange(0, 1000)][int]$OneWayDelayMs = 0,
    [ValidateRange(0, 100)][int]$PacketLossPercent = 0,
    [switch]$TeleportViolation,
    [switch]$MatchState,
    [switch]$SkipMovement,
    [switch]$FoodSpawn,
    [switch]$FoodCarry,
    [switch]$FoodDeposit,
    [switch]$Swatter,
    [switch]$Respawn,
    [switch]$Hud,
    [switch]$ResultsRematch,
    [switch]$Art,
    [switch]$Audio,
    [switch]$HostAsCockroach,
    [switch]$BriefInterruption,
    [switch]$Performance,
    [switch]$RenderedPrimary,
    [ValidateRange(640, 7680)][int]$RenderedWidth = 1920,
    [ValidateRange(480, 4320)][int]$RenderedHeight = 1080
)

$ErrorActionPreference = 'Stop'
$resolvedPlayer = (Resolve-Path $PlayerPath).Path
$outputDirectory = Join-Path $PSScriptRoot "..\Builds\Diagnostics\Movement\$OutputName"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$roomCodePath = Join-Path $outputDirectory 'room-code.txt'
Remove-Item -LiteralPath $roomCodePath -Force -ErrorAction SilentlyContinue

$common = "-batchmode -rosterExpectedPlayers 4 -requireValidDistribution " +
          "-rosterReady -rosterExpectKitchen -simulateDelayMs $OneWayDelayMs " +
          "-simulateLossPercent $PacketLossPercent"
if (-not $RenderedPrimary) { $common += ' -nographics' }
if (-not $SkipMovement) { $common += ' -movementSmoke' }
if ($MatchState) { $common += ' -matchStateSmoke' }
if ($FoodSpawn) { $common += ' -foodSpawnSmoke' }
if ($FoodCarry) { $common += ' -foodCarrySmoke' }
if ($FoodDeposit) { $common += ' -foodCarrySmoke -foodDepositSmoke' }
if ($Swatter) { $common += ' -swatterSmoke' }
if ($Respawn) { $common += ' -swatterSmoke -respawnSmoke' }
if ($Hud) { $common += ' -hudSmoke' }
if ($ResultsRematch) { $common += ' -resultsRematchSmoke' }
if ($Art) { $common += ' -artSmoke' }
if ($Audio) { $common += ' -audioSmoke' }
if ($BriefInterruption) { $common += ' -interruptionSmoke' }
if ($Performance) { $common += ' -performanceSmoke' }
$players = @(
    @{ Key = 'host'; Name = 'Human'; Seat = 'Human'; Extra = '-rosterHostSmoke -rosterStartMatch -rosterDurationSeconds 8' },
    @{ Key = 'c1'; Name = 'RoachA'; Seat = 'CockroachOne'; Extra = '-rosterJoinSmoke -rosterDurationSeconds 1' },
    @{ Key = 'c2'; Name = 'RoachB'; Seat = 'CockroachTwo'; Extra = '-rosterJoinSmoke -rosterDurationSeconds 1' },
    @{ Key = 'c3'; Name = 'RoachC'; Seat = 'CockroachThree'; Extra = '-rosterJoinSmoke -rosterDurationSeconds 1' }
)

if ($HostAsCockroach) {
    $players[0].Seat = 'CockroachOne'
    $players[1].Seat = 'Human'
}

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
    if ($RenderedPrimary -and $index -eq 0) {
        $arguments += " -screen-fullscreen 0 -screen-width $RenderedWidth -screen-height $RenderedHeight"
        $processes += Start-Process -FilePath $resolvedPlayer -ArgumentList $arguments -PassThru
    } else {
        if ($RenderedPrimary) { $arguments += ' -nographics' }
        $processes += Start-Process -FilePath $resolvedPlayer -ArgumentList $arguments -WindowStyle Hidden -PassThru
    }

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
        FoodSpawn = (Select-String -Path $logPath -Pattern 'FOOD_SPAWN_DIAGNOSTIC' | ForEach-Object Line) -join ''
        FoodCarry = (Select-String -Path $logPath -Pattern 'FOOD_CARRY_DIAGNOSTIC phase=dropped' | ForEach-Object Line) -join ''
        FoodDeposit = (Select-String -Path $logPath -Pattern 'FOOD_DEPOSIT_DIAGNOSTIC' | ForEach-Object Line) -join ''
        Swatter = (Select-String -Path $logPath -Pattern 'SWATTER_DIAGNOSTIC' | ForEach-Object Line) -join ''
        Respawn = (Select-String -Path $logPath -Pattern 'RESPAWN_DIAGNOSTIC phase=restored' | ForEach-Object Line) -join ''
        Hud = (Select-String -Path $logPath -Pattern 'HUD_DIAGNOSTIC' | ForEach-Object Line) -join ''
        ResultsRematch = (Select-String -Path $logPath -Pattern 'RESULTS_REMATCH_DIAGNOSTIC_SUCCESS' | ForEach-Object Line) -join ''
        Art = (Select-String -Path $logPath -Pattern 'ART_DIAGNOSTIC_SUCCESS' | ForEach-Object Line) -join ''
        Audio = (Select-String -Path $logPath -Pattern 'AUDIO_DIAGNOSTIC_SUCCESS' | ForEach-Object Line) -join ''
        Interruption = (Select-String -Path $logPath -Pattern 'INTERRUPTION_DIAGNOSTIC_SUCCESS' | ForEach-Object Line) -join ''
        Performance = (Select-String -Path $logPath -Pattern 'PERFORMANCE_DIAGNOSTIC_SUCCESS' | ForEach-Object Line) -join ''
        RematchMemory = (Select-String -Path $logPath -Pattern 'REMATCH_MEMORY_DIAGNOSTIC_SUCCESS' | ForEach-Object Line) -join ''
    }
}

$results | Format-Table -AutoSize
if ($processes.Where({ $_.ExitCode -ne 0 }).Count -gt 0 -or
    (-not $SkipMovement -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Movement) }).Count -gt 0) -or
    ($TeleportViolation -and [string]::IsNullOrWhiteSpace($results[1].Correction)) -or
    ($MatchState -and $results.Where({ [string]::IsNullOrWhiteSpace($_.MatchState) }).Count -gt 0) -or
    ($FoodSpawn -and $results.Where({ [string]::IsNullOrWhiteSpace($_.FoodSpawn) }).Count -gt 0) -or
    ($FoodCarry -and $results.Where({ [string]::IsNullOrWhiteSpace($_.FoodCarry) }).Count -gt 0) -or
    ($FoodDeposit -and $results.Where({ [string]::IsNullOrWhiteSpace($_.FoodDeposit) }).Count -gt 0) -or
    ($Swatter -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Swatter) }).Count -gt 0) -or
    ($Respawn -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Respawn) }).Count -gt 0) -or
    ($Hud -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Hud) }).Count -gt 0) -or
    ($ResultsRematch -and $results.Where({ [string]::IsNullOrWhiteSpace($_.ResultsRematch) }).Count -gt 0) -or
    ($Art -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Art) }).Count -gt 0) -or
    ($Audio -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Audio) }).Count -gt 0)) {
    throw "Movement diagnostic failed. Inspect $outputDirectory."
}

if ($BriefInterruption -and $results.Where({ [string]::IsNullOrWhiteSpace($_.Interruption) }).Count -gt 0) {
    throw "Movement diagnostic failed. Inspect $outputDirectory."
}

if ($Performance -and ($results.Where({ [string]::IsNullOrWhiteSpace($_.Performance) }).Count -gt 0 -or
    $results.Where({ [string]::IsNullOrWhiteSpace($_.RematchMemory) }).Count -gt 0)) {
    throw "Performance diagnostic failed. Inspect $outputDirectory."
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

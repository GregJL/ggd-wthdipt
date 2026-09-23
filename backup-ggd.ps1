param(
    [string]$LocalBackupRoot = 'D:\ggd-wthdipt-backups',
    [string]$CloudBackupRoot = 'H:\My Drive\GGD-Backups',
    [int]$LocalRetentionDays = 14,
    [int]$CloudRetentionDays = 30
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$photoRoot = 'D:\ggd-wthdipt-data\photos'
$logDirectory = Join-Path $projectRoot 'logs'
$logFile = Join-Path $logDirectory 'backup.log'
$timestamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$workingDirectory = Join-Path $LocalBackupRoot "working-$timestamp"
$archiveName = "ggd-backup-$timestamp.zip"
$localArchive = Join-Path $LocalBackupRoot $archiveName
$cloudArchive = Join-Path $CloudBackupRoot $archiveName

function Write-BackupLog([string]$Message) {
    $line = '[{0}] {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    Add-Content -LiteralPath $logFile -Value $line
    Write-Host $line
}

function Find-PgDump {
    $command = Get-Command pg_dump.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $candidate = 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe'
    if (Test-Path -LiteralPath $candidate) { return $candidate }

    throw 'pg_dump.exe was not found. Install PostgreSQL client tools or add its bin folder to PATH.'
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $LocalBackupRoot -Force | Out-Null

try {
    Write-BackupLog "Starting backup $archiveName."

    if (-not (Test-Path -LiteralPath $CloudBackupRoot)) {
        throw "Google Drive backup folder is unavailable: $CloudBackupRoot. Make sure Google Drive for desktop is running."
    }

    $passwordFile = Join-Path $env:APPDATA 'postgresql\pgpass.conf'
    if (-not (Test-Path -LiteralPath $passwordFile)) {
        throw "PostgreSQL password file not found: $passwordFile. Run configure-backup-password.ps1 first."
    }

    $pgDump = Find-PgDump
    New-Item -ItemType Directory -Path $workingDirectory -Force | Out-Null
    $databaseDump = Join-Path $workingDirectory 'ggd_inventory.dump'

    & $pgDump `
        --host=localhost `
        --port=5432 `
        --username=postgres `
        --dbname=ggd_inventory `
        --format=custom `
        --file=$databaseDump 2>> $logFile

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $databaseDump)) {
        throw "pg_dump failed with exit code $LASTEXITCODE."
    }

    $photosIncluded = Test-Path -LiteralPath $photoRoot
    if ($photosIncluded) {
        Copy-Item -LiteralPath $photoRoot -Destination (Join-Path $workingDirectory 'photos') -Recurse -Force
    }

    @(
        'GGD-WTHDIPT backup'
        "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')"
        'Database: PostgreSQL ggd_inventory (custom pg_dump format)'
        "Photos included: $photosIncluded"
        "Source photo folder: $photoRoot"
        ''
        'Restore the database with pg_restore after creating an empty ggd_inventory database.'
        'Restore the photos by copying the photos folder back to the configured PhotoStorage path.'
        'Do not overwrite a working database or photo folder without first making another backup.'
    ) | Set-Content -LiteralPath (Join-Path $workingDirectory 'RESTORE-INSTRUCTIONS.txt') -Encoding UTF8

    Compress-Archive -Path (Join-Path $workingDirectory '*') -DestinationPath $localArchive -CompressionLevel Optimal -Force
    if (-not (Test-Path -LiteralPath $localArchive)) { throw 'The local backup archive was not created.' }

    Copy-Item -LiteralPath $localArchive -Destination $cloudArchive -Force
    if (-not (Test-Path -LiteralPath $cloudArchive)) { throw 'The Google Drive copy was not created.' }

    $localCutoff = (Get-Date).AddDays(-$LocalRetentionDays)
    Get-ChildItem -LiteralPath $LocalBackupRoot -Filter 'ggd-backup-*.zip' -File |
        Where-Object LastWriteTime -lt $localCutoff |
        Remove-Item -Force

    $cloudCutoff = (Get-Date).AddDays(-$CloudRetentionDays)
    Get-ChildItem -LiteralPath $CloudBackupRoot -Filter 'ggd-backup-*.zip' -File |
        Where-Object LastWriteTime -lt $cloudCutoff |
        Remove-Item -Force

    $sizeMb = [math]::Round((Get-Item -LiteralPath $localArchive).Length / 1MB, 2)
    Write-BackupLog "Backup completed successfully ($sizeMb MB): $cloudArchive"
}
catch {
    Write-BackupLog "BACKUP FAILED: $($_.Exception.Message)"
    throw
}
finally {
    if (Test-Path -LiteralPath $workingDirectory) {
        Remove-Item -LiteralPath $workingDirectory -Recurse -Force
    }
}

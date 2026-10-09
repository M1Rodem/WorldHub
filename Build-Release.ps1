$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================" -ForegroundColor Cyan
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host $Description -ForegroundColor Yellow

    & $Command

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed. Exit code: $LASTEXITCODE"
    }
}

$root = $PSScriptRoot
$versionFile = Join-Path $root "Version.props"
$solutionFile = Join-Path $root "WorldHub.slnx"
$appProject = Join-Path $root "WorldHub.App\WorldHub.App.csproj"
$updaterProject = Join-Path $root "WorldHub.Updater\WorldHub.Updater.csproj"
$installerScript = Join-Path $root "Installer\WorldHub.iss"

$artifactsPath = Join-Path $root "artifacts"
$releasePath = Join-Path $root "release"
$updaterPath = Join-Path $root "updater-publish"

$backup = "$versionFile.backup"
$versionChanged = $false

try {
    Write-Step "WORLDHUB RELEASE BUILDER"

    foreach ($path in @(
        $versionFile,
        $solutionFile,
        $appProject,
        $updaterProject,
        $installerScript
    )) {
        if (-not (Test-Path $path)) {
            throw "Required file not found: $path"
        }
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI not found in PATH."
    }

    $isccCandidates = @()

    $isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($null -ne $isccCommand) {
        $isccCandidates += $isccCommand.Source
    }

    $isccCandidates += @(
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "E:\Inno Setup 7\ISCC.exe"
    )

    $iscc = $isccCandidates |
        Where-Object { $_ -and (Test-Path $_) } |
        Select-Object -First 1

    if (-not $iscc) {
        throw "Inno Setup compiler ISCC.exe not found."
    }

    $versionContent = Get-Content $versionFile -Raw
    $versionMatch = [regex]::Match(
        $versionContent,
        '<WorldHubVersion>(.*?)</WorldHubVersion>'
    )

    if (-not $versionMatch.Success) {
        throw "WorldHubVersion was not found in Version.props."
    }

    $currentVersion = $versionMatch.Groups[1].Value

    Write-Host ""
    Write-Host "Current version: $currentVersion" -ForegroundColor Green

    $newVersion = Read-Host "Enter new version (X.Y.Z)"

    if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version must use the X.Y.Z format."
    }

    Write-Host "Old version: $currentVersion"
    Write-Host "New version: $newVersion"

    $confirm = Read-Host "Continue? (y/n)"

    if ($confirm -ne "y") {
        Write-Host "Release cancelled."
        return
    }

    Copy-Item $versionFile $backup -Force

    try {
        # VERSION
        Write-Step "[1/9] Updating version"

        $updatedContent = [regex]::Replace(
            $versionContent,
            '<WorldHubVersion>.*?</WorldHubVersion>',
            "<WorldHubVersion>$newVersion</WorldHubVersion>"
        )

        Set-Content -Path $versionFile -Value $updatedContent -Encoding UTF8
        $versionChanged = $true

        # CLEAN
        Write-Step "[2/9] Cleaning solution"

        Push-Location $root
        try {
            Invoke-Checked "Cleaning solution" {
                dotnet clean $solutionFile -c Release
            }

            Invoke-Checked "Restoring application packages" {
                dotnet restore $appProject -r win-x64
            }
        }
        finally {
            Pop-Location
        }

        # FOLDERS
        Write-Step "[3/9] Preparing output folders"

        foreach ($folder in @($releasePath, $updaterPath, $artifactsPath)) {
            if (Test-Path $folder) {
                Remove-Item $folder -Recurse -Force
            }
        }

        New-Item -ItemType Directory -Path $artifactsPath -Force |
            Out-Null

        # APP
        Write-Step "[4/9] Publishing WorldHub.App"

        Invoke-Checked "Publishing WorldHub.App" {
            dotnet publish $appProject -c Release -r win-x64 --self-contained true -o $releasePath
        }

        $appExe = Join-Path $releasePath "WorldHub.App.exe"

        if (-not (Test-Path $appExe)) {
            throw "WorldHub.App.exe was not produced."
        }

        # UPDATER
        Write-Step "[5/9] Publishing WorldHub.Updater"

        Invoke-Checked "Publishing WorldHub.Updater" {
            dotnet publish $updaterProject -c Release -r win-x64 --self-contained true -o $updaterPath
        }

        $updaterExe = Join-Path $updaterPath "WorldHub.Updater.exe"

        if (-not (Test-Path $updaterExe)) {
            throw "WorldHub.Updater.exe was not produced."
        }

        Copy-Item $updaterExe $releasePath -Force

        # VERSION CHECK
        Write-Step "[6/9] Checking application version"

        $actualVersion = (Get-Item $appExe).VersionInfo.ProductVersion

        if ([string]::IsNullOrWhiteSpace($actualVersion) -or
            -not $actualVersion.StartsWith(
                $newVersion,
                [System.StringComparison]::Ordinal
            )) {
            throw "Version mismatch. Expected $newVersion, got '$actualVersion'."
        }

        Write-Host "Version OK: $actualVersion" -ForegroundColor Green

        # SMOKE TEST
        Write-Step "[7/9] Testing application startup"

        $process = Start-Process `
            -FilePath $appExe `
            -WorkingDirectory $releasePath `
            -PassThru

        Start-Sleep -Seconds 5
        $process.Refresh()

        if ($process.HasExited) {
            throw "WorldHub exited during startup. Exit code: $($process.ExitCode)"
        }

        Stop-Process -Id $process.Id -Force

        # PORTABLE
        Write-Step "[8/9] Creating portable archive"

        $portable = Join-Path $artifactsPath "WorldHub-portable-v$newVersion.zip"

        Compress-Archive `
            -Path (Join-Path $releasePath "*") `
            -DestinationPath $portable `
            -CompressionLevel Optimal

        # INSTALLER
        Write-Step "[9/9] Creating installer"

        Invoke-Checked "Compiling installer" {
            & $iscc "/dAppVersion=$newVersion" $installerScript
        }

        $installer = Join-Path $artifactsPath "WorldHub-Setup-v$newVersion.exe"

        if (-not (Test-Path $installer)) {
            throw "Installer was not found at '$installer'. Check OutputDir in WorldHub.iss."
        }

        Write-Step "RELEASE COMPLETE"

        Write-Host "Version: $newVersion" -ForegroundColor Green
        Write-Host ""
        Write-Host "Artifacts:"

        Get-ChildItem $artifactsPath |
            ForEach-Object {
                Write-Host $_.FullName -ForegroundColor Green
            }

        Remove-Item $backup -Force -ErrorAction SilentlyContinue
        $versionChanged = $false
    }
    catch {
        if (Test-Path $backup) {
            Copy-Item $backup $versionFile -Force
            Remove-Item $backup -Force
        }

        $versionChanged = $false
        throw
    }
}
catch {
    Write-Host ""
    Write-Host "================================" -ForegroundColor Red
    Write-Host "RELEASE FAILED" -ForegroundColor Red
    Write-Host "================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally {
    if ($versionChanged -and (Test-Path $backup)) {
        Copy-Item $backup $versionFile -Force
        Remove-Item $backup -Force
    }
}
$ErrorActionPreference = "Stop"


function Write-Step(
    [string]$message
)
{
    Write-Host ""
    Write-Host "================================" -ForegroundColor Cyan
    Write-Host $message -ForegroundColor Cyan
    Write-Host "================================" -ForegroundColor Cyan
}


function Invoke-Dotnet(
    [string[]]$Arguments,
    [string]$Description
)
{
    Write-Host ""
    Write-Host $Description -ForegroundColor Yellow

    dotnet @Arguments

    if ($LASTEXITCODE -ne 0)
    {
        throw "$Description failed. Exit code: $LASTEXITCODE"
    }
}



try
{
    $root =
        Split-Path `
            -Parent `
            $MyInvocation.MyCommand.Path


    $versionFile =
        Join-Path `
            $root `
            "Version.props"


    $artifactsPath =
        Join-Path `
            $root `
            "artifacts"


    $releasePath =
        Join-Path `
            $root `
            "release"


    $updaterPath =
        Join-Path `
            $root `
            "updater-publish"



    Write-Step "WORLDHUB RELEASE BUILDER"



    if (-not (Test-Path $versionFile))
    {
        throw "Version.props not found."
    }



    $currentVersion =
        Select-String `
            -Path $versionFile `
            -Pattern '<WorldHubVersion>(.*?)</WorldHubVersion>' |
        ForEach-Object {
            $_.Matches.Groups[1].Value
        }



    if ([string]::IsNullOrWhiteSpace($currentVersion))
    {
        throw "Current version not found."
    }



    Write-Host ""
    Write-Host "Current version: $currentVersion" `
        -ForegroundColor Green



    $newVersion =
        Read-Host `
            "Enter new version"



    if ($newVersion -notmatch '^\d+\.\d+\.\d+$')
    {
        throw "Version must be X.Y.Z"
    }



    Write-Host ""

    Write-Host "Old version:"
    Write-Host $currentVersion `
        -ForegroundColor DarkGray

    Write-Host "New version:"
    Write-Host $newVersion `
        -ForegroundColor Green



    $confirm =
        Read-Host `
            "Continue? (y/n)"



    if ($confirm -ne "y")
    {
        exit 0
    }



    #
    # BACKUP VERSION
    #

    $backup =
        "$versionFile.backup"



    Copy-Item `
        $versionFile `
        $backup `
        -Force



    try
    {


        #
        # VERSION
        #

        Write-Step "[1/9] Updating version"



        $content =
            Get-Content `
                $versionFile `
                -Raw



        $content =
            $content -replace `
            '<WorldHubVersion>.*?</WorldHubVersion>',
            "<WorldHubVersion>$newVersion</WorldHubVersion>"



        Set-Content `
            -Path $versionFile `
            -Value $content `
            -Encoding UTF8





        #
        # CLEAN
        #

        Write-Step "[2/9] Cleaning"



        Invoke-Dotnet `
            @(
                "clean"
            ) `
            "dotnet clean"





        #
        # PREPARE
        #

        Write-Step "[3/9] Preparing folders"



        foreach($folder in @(
            $releasePath,
            $updaterPath
        ))
        {
            if(Test-Path $folder)
            {
                Remove-Item `
                    $folder `
                    -Recurse `
                    -Force
            }
        }



        New-Item `
            -ItemType Directory `
            -Path $releasePath |
        Out-Null





        #
        # APP PUBLISH
        #

        Write-Step "[4/9] Publishing WorldHub.App"



        Invoke-Dotnet `
            @(
                "publish",
                "$root\WorldHub.App\WorldHub.App.csproj",
                "-c",
                "Release",
                "-r",
                "win-x64",
                "--self-contained",
                "true",
                "-o",
                $releasePath
            ) `
            "WorldHub.App publish"





        #
        # APP CHECK
        #

        Write-Step "[5/9] Checking application"



        $appExe =
            Join-Path `
                $releasePath `
                "WorldHub.App.exe"



        if(-not(Test-Path $appExe))
        {
            throw "WorldHub.App.exe missing."
        }





        #
        # UPDATER
        #

        Write-Step "[6/9] Publishing updater"



        Invoke-Dotnet `
            @(
                "publish",
                "$root\WorldHub.Updater\WorldHub.Updater.csproj",
                "-c",
                "Release",
                "-r",
                "win-x64",
                "--self-contained",
                "true",
                "-o",
                $updaterPath
            ) `
            "WorldHub.Updater publish"





        $updaterExe =
            Join-Path `
                $updaterPath `
                "WorldHub.Updater.exe"



        if(-not(Test-Path $updaterExe))
        {
            throw "WorldHub.Updater.exe missing after publish."
        }



        Copy-Item `
            $updaterExe `
            $releasePath `
            -Force





        #
        # VERSION CHECK
        #

        Write-Step "[7/9] Checking application version"



        $fileInfo =
            Get-Item $appExe


        $appVersion =
            $fileInfo.VersionInfo.ProductVersion



        if([string]::IsNullOrWhiteSpace($appVersion))
        {
            throw "Application version not found."
        }



        if(-not $appVersion.StartsWith(
            $newVersion,
            [System.StringComparison]::OrdinalIgnoreCase))
        {
            throw (
                "Application version mismatch. " +
                "Expected: $newVersion. " +
                "Actual: $appVersion"
            )
        }



        Write-Host (
            "Application version OK: {0}" -f $appVersion
        ) -ForegroundColor Green





        #
        # SMOKE TEST
        #

        Write-Step "[8/9] Testing application"


        $process =
            Start-Process `
                -FilePath $appExe `
                -WorkingDirectory $releasePath `
                -PassThru


        Start-Sleep -Seconds 5


        if($process.HasExited)
        {
            throw (
                "WorldHub.App exited immediately. " +
                "ExitCode: $($process.ExitCode)"
            )
        }


        Write-Host "Application started successfully." `
            -ForegroundColor Green


        Stop-Process `
            -Id $process.Id `
            -Force


        #
        # ZIP
        #

        Write-Step "[9/9] Creating archive"



        if(-not(Test-Path $artifactsPath))
        {
            New-Item `
                -ItemType Directory `
                -Path $artifactsPath |
            Out-Null
        }



        $zip =
            Join-Path `
                $artifactsPath `
                "WorldHub-v$newVersion-win-x64.zip"



        if(Test-Path $zip)
        {
            Remove-Item `
                $zip `
                -Force
        }



        Compress-Archive `
            -Path "$releasePath\*" `
            -DestinationPath $zip





        Remove-Item `
            $backup `
            -Force `
            -ErrorAction SilentlyContinue





        $size =
            (Get-Item $zip).Length / 1MB



        Write-Step "RELEASE COMPLETE"



        Write-Host ""
        Write-Host "Version:"
        Write-Host $newVersion `
            -ForegroundColor Green

        Write-Host ""

        Write-Host "Archive:"
        Write-Host $zip `
            -ForegroundColor Green

        Write-Host ""

        Write-Host (
            "Size: {0:N2} MB" -f $size
        )

    }
    catch
    {
        throw
    }

}
catch
{
    Write-Step "RELEASE FAILED"


    Write-Host ""
    Write-Host $_.Exception.Message `
        -ForegroundColor Red



    if(Test-Path "$versionFile.backup")
    {
        Copy-Item `
            "$versionFile.backup" `
            $versionFile `
            -Force


        Remove-Item `
            "$versionFile.backup" `
            -Force


        Write-Host ""
        Write-Host "Version restored."
    }


    exit 1
}
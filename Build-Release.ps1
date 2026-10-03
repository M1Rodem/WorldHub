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


function Invoke-CommandChecked(
    [scriptblock]$Command,
    [string]$Description
)
{
    Write-Host """WorldHub-portable-v$newVersion.zip"
    Write-Host $Description -ForegroundColor Yellow

    & $Command

    if($LASTEXITCODE -ne 0)
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


    $minecraftPath =
        Join-Path `
            $root `
            "WorldHub.Minecraft"


    $installerScript =
        Join-Path `
            $root `
            ".\Installer\WorldHub.iss"



    Write-Step "WORLDHUB RELEASE BUILDER"



    if(-not(Test-Path $versionFile))
    {
        throw "Version.props not found."
    }


    if(-not(Test-Path $installerScript))
    {
        throw "WorldHub.iss not found."
    }



    $currentVersion =
        Select-String `
            -Path $versionFile `
            -Pattern '<WorldHubVersion>(.*?)</WorldHubVersion>' |
        ForEach-Object {
            $_.Matches.Groups[1].Value
        }



    Write-Host ""
    Write-Host "Current version: $currentVersion" -ForegroundColor Green



    $newVersion =
        Read-Host "Enter new version"



    if($newVersion -notmatch '^\d+\.\d+\.\d+$')
    {
        throw "Version must be X.Y.Z"
    }



    Write-Host ""
    Write-Host "Old version: $currentVersion"
    Write-Host "New version: $newVersion"



    $confirm =
        Read-Host "Continue? (y/n)"



    if($confirm -ne "y")
    {
        exit 0
    }



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

        Write-Step "[1/11] Updating version"


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

        Write-Step "[2/11] Cleaning"


        dotnet clean


        Write-Host ""
        Write-Host "Restoring packages for win-x64..." -ForegroundColor Yellow


        dotnet restore `
            "$root\WorldHub.App\WorldHub.App.csproj" `
            -r win-x64


        #
        # FOLDERS
        #

        Write-Step "[3/11] Preparing folders"


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


        if(Test-Path $artifactsPath)
        {
            Remove-Item `
                $artifactsPath `
                -Recurse `
                -Force
        }


        New-Item `
            -ItemType Directory `
            -Path $artifactsPath |
        Out-Null



        #
        # APP
        #

        Write-Step "[4/11] Publishing WorldHub.App"


        dotnet publish `
            "$root\WorldHub.App\WorldHub.App.csproj" `
            -c Release `
            -r win-x64 `
            --self-contained true `
            -o $releasePath



        $appExe =
            Join-Path `
                $releasePath `
                "WorldHub.App.exe"


        if(-not(Test-Path $appExe))
        {
            throw "WorldHub.App.exe not found."
        }



        #
        # UPDATER
        #

        Write-Step "[5/11] Publishing updater"


        dotnet publish `
            "$root\WorldHub.Updater\WorldHub.Updater.csproj" `
            -c Release `
            -r win-x64 `
            --self-contained true `
            -o $updaterPath



        Copy-Item `
            "$updaterPath\WorldHub.Updater.exe" `
            $releasePath `
            -Force



        #
        # VERSION CHECK
        #

        Write-Step "[6/11] Checking version"


        $version =
            (Get-Item $appExe).VersionInfo.ProductVersion


        if(-not $version.StartsWith($newVersion))
        {
            throw "Version mismatch. Expected $newVersion, got $version"
        }


        Write-Host "Version OK: $version" -ForegroundColor Green



        #
        # SMOKE TEST
        #

        Write-Step "[7/11] Testing application"


        $process =
            Start-Process `
                $appExe `
                -WorkingDirectory $releasePath `
                -PassThru


        Start-Sleep 5


        if($process.HasExited)
        {
            throw "Application exited."
        }


        Stop-Process `
            $process.Id `
            -Force



        #
        # PORTABLE
        #

        Write-Step "[8/11] Creating portable"


        $portable =
            Join-Path `
                $artifactsPath `
                 "WorldHub-portable-v$newVersion.zip"


        Compress-Archive `
            "$releasePath\*" `
            $portable



        #
        # INSTALLER
        #

        Write-Step "[9/11] Creating installer"


        $iscc =
            "E:\Inno Setup 7\ISCC.exe"


        if(-not(Test-Path $iscc))
        {
            throw "Inno Setup compiler not found."
        }


        & $iscc `
            "/dAppVersion=$newVersion" `
            $installerScript



        $installer =
            Join-Path `
                $artifactsPath `
                "WorldHub-Setup-v$newVersion.exe"



        if(-not(Test-Path $installer))
        {
            throw "Installer was not created."
        }



        #
        # MOD
        #

        Write-Step "[10/11] Building Minecraft mod"


        Push-Location $minecraftPath

        .\gradlew.bat clean build

        Pop-Location



        $jar =
            Get-ChildItem `
                "$minecraftPath\build\libs" `
                -Filter "*.jar" |
            Where-Object {
                $_.Name -notlike "*sources*" -and
                $_.Name -notlike "*dev*"
            } |
            Select-Object -First 1



        if($null -eq $jar)
        {
            throw "Minecraft mod jar not found."
        }



        Copy-Item `
            $jar.FullName `
            $artifactsPath `
            -Force



        #
        # DONE
        #

        Write-Step "[11/11] RELEASE COMPLETE"


        Write-Host ""
        Write-Host "Version: $newVersion" -ForegroundColor Green

        Write-Host ""
        Write-Host "Artifacts:"
        Get-ChildItem `
            $artifactsPath |
        ForEach-Object {
            Write-Host $_.FullName -ForegroundColor Green
        }


        Remove-Item `
            $backup `
            -Force `
            -ErrorAction SilentlyContinue

    }
    catch
    {
        if(Test-Path $backup)
        {
            Copy-Item `
                $backup `
                $versionFile `
                -Force

            Remove-Item `
                $backup `
                -Force
        }

        throw
    }

}
catch
{
    Write-Host ""
    Write-Host "================================" -ForegroundColor Red
    Write-Host "RELEASE FAILED" -ForegroundColor Red
    Write-Host "================================" -ForegroundColor Red

    Write-Host $_.Exception.Message -ForegroundColor Red

    exit 1
}
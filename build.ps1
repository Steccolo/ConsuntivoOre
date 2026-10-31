# Compila l'app (exe singolo, .NET incluso) e genera l'installer in dist\.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path "Assets\app.ico")) { & "$PSScriptRoot\tools\MakeIcon.ps1" }

Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue
dotnet publish ConsuntivoOre.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish non riuscito" }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 non trovato (winget install JRSoftware.InnoSetup)" }

$version = ([xml](Get-Content "Directory.Build.props")).Project.PropertyGroup.Version
& $iscc /Qp "/DMyAppVersion=$version" "installer\ConsuntivoOre.iss"
if ($LASTEXITCODE -ne 0) { throw "Compilazione installer non riuscita" }

Get-ChildItem dist\*.exe | ForEach-Object { Write-Host "Installer pronto: $($_.FullName)" }

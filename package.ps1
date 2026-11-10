# Builds the plugin and makes the release zip: dist\SkipTraining-<version>.zip
# The zip holds BepInEx\plugins\SkipTraining\SkipTraining.dll, so it can be extracted straight into the game folder.
# Close the game first: it locks the plugin DLL.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }

$version = ([xml](Get-Content SkipTraining.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dll = Join-Path $PSScriptRoot 'bin\Release\net6.0\SkipTraining.dll'
New-Item -ItemType Directory -Force dist | Out-Null
$zipPath = Join-Path $PSScriptRoot "dist\SkipTraining-$version.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, 'BepInEx/plugins/SkipTraining/SkipTraining.dll')
$zip.Dispose()
$stream.Dispose()
"Made $zipPath"

# This script builds Celestia tools. To use, just run this script with `pwsh` (Powershell 7).

# Save pwd for later recover
$PrevPath = Get-Location

Write-Host "Publish for packaging build."
Set-Location $PSScriptRoot

# Pick an OS tag…
$os = if ($IsWindows) { 'Win' }
    elseif ($IsLinux)  { 'Linux' }
    elseif ($IsMacOS)  { 'OSX' }
    else              { 'Unknown' }

# Pick an architecture
$arch = if ([Environment]::Is64BitProcess) { '64' } else { '86' }
$runtimeFolder = "${os}${arch}" # Runtime name, e.g. "Win64"

$PublishFolder = "$PSScriptRoot\Publish\Celestia Tools\${runtimeFolder}"
# Delete current data
if (Test-Path -Path $PublishFolder) {
    Remove-Item $PublishFolder -Recurse -Force
}

# Publish Executable
$PublishExecutables = @(
    "CelestiaCMODConverter\CelestiaCMODConverter.csproj"
    "CelestiaStarDatabaseConverter\CelestiaStarDatabaseConverter.csproj"
    "VirtualTextureReferenceSet\VirtualTextureReferenceSet.csproj"
)
foreach ($Item in $PublishExecutables) {
    dotnet publish $PSScriptRoot\..\$Item --use-current-runtime --self-contained --output $PublishFolder
}

# Delete all PDB files from the $PublishFolder
Write-Host "Deleting all PDB files from $PublishFolder..."
$pdbFiles = Get-ChildItem -Path $PublishFolder -Filter *.pdb -Recurse -ErrorAction SilentlyContinue
if ($pdbFiles) {
    foreach ($file in $pdbFiles) {
        Remove-Item $file.FullName -Force -Verbose
    }
    Write-Host "Deleted $($pdbFiles.Count) PDB file(s)."
} else {
    Write-Host "No PDB files found in $PublishFolder."
}

# Validation
$Executables = @(
    "CelestiaCMODConverter.exe"
    "CelestiaStarDatabaseConverter.exe"
    "VirtualTextureReferenceSet.exe"
)
foreach ($item in $Executables) {
    $exePath = Join-Path $PublishFolder $item
    if (-Not (Test-Path $exePath)) {
        Write-Host "Build failed."
        Exit
    }   
}

# Pick RID
$rid = "${os}-${arch}" # Runtime name, e.g. "Win-x64"
$version = "v0.0.1"
# Create archive
$Date = Get-Date -Format yyyyMMdd
$ArchiveFolder = "$PublishFolder\Packages"
$ArchivePath = "$ArchiveFolder\Celestia_Tools_${version}_${rid}_B$Date.zip"
New-Item -ItemType Directory -Force -Path $ArchiveFolder
Compress-Archive -Path $PublishFolder\* -DestinationPath $ArchivePath -Force

# Recover pwd
Set-Location $PrevPath
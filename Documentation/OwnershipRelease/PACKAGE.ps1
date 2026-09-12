# Run BUILD-VERIFICATION.ps1 successfully first. Only creates distributable ZIP files.
param([string]$Version='2026-09-12')
$ErrorActionPreference='Stop'
if ($Version -notmatch '^\d{4}-\d{2}-\d{2}$') { throw 'Use a YYYY-MM-DD version.' }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageRoot=$PSScriptRoot
$sourceRoot=Join-Path $packageRoot 'verification'

function Add-ArchiveFile($Archive,[string]$Path,[string]$Name) {
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $Archive,$Path,$Name.Replace('\','/'),[System.IO.Compression.CompressionLevel]::Optimal)
}
function Add-GuideFiles($Archive) {
    foreach($name in @('START-HERE.md','README.md','FILE-MAP.md','VERIFICATION.md')) {
        Add-ArchiveFile $Archive (Join-Path $packageRoot $name) $name
    }
    foreach($folder in @('database','settings')) {
        foreach($item in Get-ChildItem -LiteralPath (Join-Path $packageRoot $folder) -Recurse -File) {
            Add-ArchiveFile $Archive $item.FullName $item.FullName.Substring($packageRoot.Length+1)
        }
    }
}

$updatePath=Join-Path $packageRoot "OwnershipRelease-$Version.zip"
$stream=[System.IO.File]::Open($updatePath,[System.IO.FileMode]::Create)
$archive=[System.IO.Compression.ZipArchive]::new($stream,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    Add-GuideFiles $archive
    foreach($name in @('BUILD-VERIFICATION.ps1','PACKAGE.ps1')) { Add-ArchiveFile $archive (Join-Path $packageRoot $name) $name }
    foreach($folder in @('portal','api','tests')) {
        foreach($item in Get-ChildItem -LiteralPath (Join-Path $packageRoot $folder) -Recurse -File) {
            if($item.FullName -match '[\\/](bin|obj)[\\/]') { continue }
            Add-ArchiveFile $archive $item.FullName $item.FullName.Substring($packageRoot.Length+1)
        }
    }
} finally { $archive.Dispose();$stream.Dispose() }

$fullPath=Join-Path $packageRoot "PortalAndApi-FullSource-$Version.zip"
$stream=[System.IO.File]::Open($fullPath,[System.IO.FileMode]::Create)
$archive=[System.IO.Compression.ZipArchive]::new($stream,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    Add-GuideFiles $archive
    foreach($item in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
        $relative=$item.FullName.Substring($sourceRoot.Length+1)
        if($relative -match '[\\/](bin|obj|uploads|DataProtection|\.git)[\\/]' -or
           $item.Name -like 'appsettings*.json' -or $item.Name -eq 'launchSettings.json') { continue }
        # Allow source/static assets and licenses only. Exclude user files, temporary backups,
        # request files, key material, settings, database dumps and generated binaries.
        if($item.Extension -notin @('.cs','.csproj','.cshtml','.js','.css','.map','.ico','.woff','.woff2','.ttf','.svg','.png','.jpg','.jpeg') -and
           $item.Name -notlike 'LICENSE*') { continue }
        Add-ArchiveFile $archive $item.FullName $relative
    }
} finally { $archive.Dispose();$stream.Dispose() }
Get-Item -LiteralPath $updatePath,$fullPath | Select-Object Name,Length

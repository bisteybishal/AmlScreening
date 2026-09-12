# Copies code to an isolated directory INSIDE this package, then builds/tests it.
# Does not run either web server, connect to a database, or change the source projects.
param(
 [string]$PortalRoot='C:\Projects\AmlScreening',
 [string]$ApiRoot='C:\Projects\ScreeningApi'
)
$ErrorActionPreference='Stop'
$verificationRoot=Join-Path $PSScriptRoot 'verification'
New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
foreach($entry in @(@{Root=$PortalRoot;Side='portal';Prefix='AmlScreening'},@{Root=$ApiRoot;Side='api';Prefix='ScreeningApi'})) {
 foreach($project in Get-ChildItem -LiteralPath $entry.Root -Directory | Where-Object { $_.Name -like ($entry.Prefix+'*') -and (Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File) }) {
  $destination=Join-Path $verificationRoot ($entry.Side+'\'+$project.Name)
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  Get-ChildItem -LiteralPath $project.FullName -Force | Where-Object { $_.Name -notin @('bin','obj','.git') -and $_.Name -notlike 'appsettings*.json' } |
   Copy-Item -Destination $destination -Recurse -Force
 }
 Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $entry.Side) -Directory |
  Copy-Item -Destination (Join-Path $verificationRoot $entry.Side) -Recurse -Force
}
dotnet build (Join-Path $verificationRoot 'portal\AmlScreening\AmlScreening.Portal.csproj') --nologo
if($LASTEXITCODE -ne 0) {throw 'Portal build failed.'}
dotnet build (Join-Path $verificationRoot 'api\ScreeningApi\ScreeningApi.csproj') --nologo
if($LASTEXITCODE -ne 0) {throw 'API build failed.'}
dotnet run --project (Join-Path $PSScriptRoot 'tests\OwnershipChecks.csproj') --no-launch-profile
if($LASTEXITCODE -ne 0) {throw 'Ownership/calculation checks failed.'}

# Build script for LethalMic
$ErrorActionPreference = "Stop"

# Find Lethal Company installation
$lethalCompanyPath = "E:\SteamLibrary\steamapps\common\Lethal Company"
if (!(Test-Path $lethalCompanyPath)) {
    Write-Error "Lethal Company not found at: $lethalCompanyPath"
    exit 1
}
Write-Host "Found Lethal Company at: $lethalCompanyPath"

# Find r2modman profile
$r2modmanPath = "$env:APPDATA\r2modmanPlus-local\LethalCompany\profiles\LethalMic Test"
if (!(Test-Path $r2modmanPath)) {
    Write-Error "r2modman profile not found at: $r2modmanPath"
    exit 1
}
Write-Host "Found r2modman profile at: $r2modmanPath"

# Clean previous builds
Write-Host "Cleaning previous builds..."
if (Test-Path "build") {
    Remove-Item -Path "build" -Recurse -Force
}

# Create build directories
Write-Host "Creating build directories..."
New-Item -Path "build" -ItemType Directory -Force | Out-Null
New-Item -Path "build\plugins" -ItemType Directory -Force | Out-Null

# Build LethalMic
Write-Host "Building LethalMic..."
dotnet build src/plugins/LethalMic/LethalMic.csproj -c Release

# Copy plugin files
Write-Host "Copying plugin files..."
Copy-Item "src/plugins/LethalMic/bin/Release/netstandard2.1/LethalMic.dll" -Destination "build/plugins/" -Force

# Copy icon.png to build output
Write-Host "Copying icon.png..."
Copy-Item "src/plugins/LethalMic/icon.png" -Destination "build/plugins/" -Force

# Copy canonical manifest.json for mod loader compatibility
Write-Host "Copying manifest.json..."
Copy-Item "src/plugins/LethalMic/manifest.json" -Destination "build/plugins/manifest.json" -Force

# Copy Resources directory if it exists
if (Test-Path "src/plugins/LethalMic/Resources") {
    Write-Host "Copying Resources directory..."
    Copy-Item "src/plugins/LethalMic/Resources" -Destination "build/plugins/" -Recurse -Force
}

# Create zip package
Write-Host "Creating zip package..."
Compress-Archive -Path "build/plugins/*" -DestinationPath "build/LethalMic.zip" -Force

Write-Host "Build completed successfully!"
Write-Host "Plugin package created at: $((Get-Item "build/LethalMic.zip").FullName)" 
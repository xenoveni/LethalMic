# Build script for LethalMic
$ErrorActionPreference = "Stop"

# Configuration
$LETHAL_COMPANY_PATH = "C:\Program Files (x86)\Steam\steamapps\common\Lethal Company"
$OUTPUT_DIR = "bin\Release\netstandard2.1"
$PLUGIN_DIR = "$LETHAL_COMPANY_PATH\BepInEx\plugins\LethalMic"

# Check if Lethal Company is installed
if (-not (Test-Path $LETHAL_COMPANY_PATH)) {
    Write-Error "Lethal Company not found at $LETHAL_COMPANY_PATH"
    Write-Host "Please set the correct path in the build script."
    exit 1
}

# Build the project
Write-Host "Building LethalMic..."
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# Create plugin directory if it doesn't exist
if (-not (Test-Path $PLUGIN_DIR)) {
    New-Item -ItemType Directory -Path $PLUGIN_DIR | Out-Null
}

# Copy files to plugin directory
Write-Host "Copying files to plugin directory..."
Copy-Item "$OUTPUT_DIR\LethalMic.dll" -Destination $PLUGIN_DIR
Copy-Item "manifest.json" -Destination $PLUGIN_DIR
Copy-Item "icon.png" -Destination $PLUGIN_DIR
Copy-Item "README.md" -Destination $PLUGIN_DIR
Copy-Item "LICENSE" -Destination $PLUGIN_DIR

Write-Host "Build completed successfully!"
Write-Host "Plugin installed at: $PLUGIN_DIR" 
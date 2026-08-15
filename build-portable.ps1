param(
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "PortableRdpManager.csproj"
$output = Join-Path $PSScriptRoot "dist\$Runtime"

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $output `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

Write-Host ""
Write-Host "Готово: $output\PortableRdpManager.exe"

param(
    [string]$DllPath = "..\CyUSB.dll"
)

$ErrorActionPreference = "Stop"
$resolved = Resolve-Path $DllPath
Write-Host "Inspecting: $resolved"

$asm = [System.Reflection.Assembly]::LoadFrom($resolved.Path)
Write-Host "FullName: $($asm.FullName)"
Write-Host "ImageRuntimeVersion: $($asm.ImageRuntimeVersion)"
Write-Host ""
Write-Host "Referenced assemblies:"
$asm.GetReferencedAssemblies() | ForEach-Object { Write-Host "  $($_.Name) $($_.Version)" }
Write-Host ""
Write-Host "Public types:"
$asm.GetExportedTypes() | ForEach-Object { Write-Host "  $($_.FullName)" }

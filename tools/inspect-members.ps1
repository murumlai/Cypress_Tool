param(
    [string]$DllPath = "..\CyUSB.dll"
)

$ErrorActionPreference = "Stop"
$resolved = Resolve-Path $DllPath
$asm = [System.Reflection.Assembly]::LoadFrom($resolved.Path)

$targetTypes = @(
    "CyUSB.USBDeviceList",
    "CyUSB.USBDevice",
    "CyUSB.CyUSBDevice",
    "CyUSB.CyFX3Device",
    "CyUSB.CyControlEndPoint",
    "CyUSB.CyConst",
    "CyUSB.FX3_FWDWNLOAD_MEDIA_TYPE",
    "CyUSB.FX3_FWDWNLOAD_ERROR_CODE",
    "CyUSB.XMODE"
)

foreach ($tn in $targetTypes) {
    $t = $asm.GetType($tn)
    if (-not $t) { Write-Host "TYPE NOT FOUND: $tn"; continue }
    Write-Host "==================================================================="
    Write-Host "TYPE: $($t.FullName)  (Base: $($t.BaseType))"
    Write-Host "-- Constructors --"
    $t.GetConstructors() | ForEach-Object {
        $ps = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
        Write-Host "  ctor($ps)"
    }
    Write-Host "-- Public Fields --"
    $t.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static) | ForEach-Object {
        Write-Host "  $($_.FieldType.Name) $($_.Name)"
    }
    Write-Host "-- Public Properties --"
    $t.GetProperties() | ForEach-Object {
        Write-Host "  $($_.PropertyType.Name) $($_.Name)"
    }
    Write-Host "-- Public Methods --"
    $t.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::DeclaredOnly) | ForEach-Object {
        $ps = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
        Write-Host "  $($_.ReturnType.Name) $($_.Name)($ps)"
    }
    Write-Host ""
}

# Enum values
foreach ($en in @("CyUSB.FX3_FWDWNLOAD_MEDIA_TYPE","CyUSB.FX3_FWDWNLOAD_ERROR_CODE","CyUSB.XMODE")) {
    $t = $asm.GetType($en)
    if ($t -and $t.IsEnum) {
        Write-Host "ENUM $en :"
        [Enum]::GetNames($t) | ForEach-Object { Write-Host "  $_ = $([int][Enum]::Parse($t,$_))" }
        Write-Host ""
    }
}

# Exporta cada diapositiva de instrucciones.html a PNG (1080 x 1920) con Edge sin ventana.
# Uso:  powershell -ExecutionPolicy Bypass -File Docs\Instrucciones\exportar.ps1
param(
    [int]$Total = 19,
    [string]$Salida = "$PSScriptRoot\..\..\Assets\VR_FlightSimulato_Proy\Sprites\Instrucciones"
)

$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
$html = (Resolve-Path "$PSScriptRoot\instrucciones.html").Path -replace '\\', '/'
New-Item -ItemType Directory -Force $Salida | Out-Null
$Salida = (Resolve-Path $Salida).Path
$perfil = Join-Path $env:TEMP "edge-export-instrucciones"

for ($i = 1; $i -le $Total; $i++) {
    $png = Join-Path $Salida ("Instruccion_{0:D2}.png" -f $i)
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
        --user-data-dir="$perfil" --window-size=1080,1920 --virtual-time-budget=2000 `
        --screenshot="$png" "file:///$html#$i" | Out-Null
    Write-Host "OK $png"
}

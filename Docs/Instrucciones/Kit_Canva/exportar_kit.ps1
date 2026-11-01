# Exporta el kit para Canva: las diapositivas nuevas (1080 x 1920), la hoja de recursos y cada SVG como PNG
# transparente al doble de tamaño.
# Uso:  powershell -ExecutionPolicy Bypass -File Docs\Instrucciones\Kit_Canva\exportar_kit.ps1
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
$kit = $PSScriptRoot
$perfil = Join-Path $env:TEMP "edge-export-kit"
$html = (Join-Path $kit "diapositivas_nuevas.html") -replace '\\', '/'
New-Item -ItemType Directory -Force "$kit\png", "$kit\diapositivas" | Out-Null

function Captura($url, $png, $ancho, $alto, $escala = 1, $transparente = $false) {
    $extra = @()
    if ($transparente) { $extra += "--default-background-color=00000000" }
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=$escala @extra `
        --user-data-dir="$perfil" --window-size="$ancho,$alto" --virtual-time-budget=4000 `
        --screenshot="$png" $url 2>$null | Out-Null
    Write-Host "OK $png"
}

Captura "file:///$html#1" "$kit\diapositivas\Diapositiva_09_Tren_de_aterrizaje.png" 1080 1920
Captura "file:///$html#2" "$kit\diapositivas\Diapositiva_17_Luces_PAPI.png" 1080 1920
Captura "file:///$html#3" "$kit\hoja_de_recursos.png" 1080 3150

foreach ($svg in Get-ChildItem "$kit\svg\*.svg") {
    $xml = [xml](Get-Content $svg.FullName -Raw)
    $w = [int]$xml.svg.width; $h = [int]$xml.svg.height
    $url = "file:///" + ($svg.FullName -replace '\\', '/')
    Captura $url "$kit\png\$($svg.BaseName).png" $w $h 2 $true
}

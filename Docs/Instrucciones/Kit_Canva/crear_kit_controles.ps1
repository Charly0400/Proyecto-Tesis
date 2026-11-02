# Arma "Kit_de_piezas_controles.pptx" (mismo formato que Kit_P-47 y Kit_de_piezas_VR: 540 x 960 pt, ARMADO + PIEZAS
# SUELTAS). Todo son figuras nativas de PowerPoint (formas, líneas y trazos libres), así en Canva cada parte se
# recolorea, se mueve o se usa sola. Página 1: palanca del tren y switch con guarda. Página 2: aviso de pérdida,
# luces PAPI, iconos y botón SUBIR.
# Uso:  powershell -ExecutionPolicy Bypass -File Docs\Instrucciones\Kit_Canva\crear_kit_controles.ps1
$ErrorActionPreference = 'Stop'
$kit = $PSScriptRoot
$salida = Join-Path $kit "Kit_de_piezas_controles.pptx"
$vistas = Join-Path $kit "kit_controles_vista_previa"

# Se diseña en píxeles de 1080 x 1920; la página mide 540 x 960 pt (igual que los otros kits)
function Pt($v) { [single]($v * 0.5) }
function Rgb($hex) {
    [int]([Convert]::ToInt32($hex.Substring(0, 2), 16) + 256 * [Convert]::ToInt32($hex.Substring(2, 2), 16) + 65536 * [Convert]::ToInt32($hex.Substring(4, 2), 16))
}
$Titulo = 'Barlow'; $Cuerpo = 'Noto Sans'
$script:unico = 0

# ---------- Transformación de la pieza actual: coordenadas locales (las del SVG) → página ----------
$script:T = @{ ox = 0; oy = 0; sc = 1 }
function X($x) { Pt ($script:T.ox + $x * $script:T.sc) }
function Y($y) { Pt ($script:T.oy + $y * $script:T.sc) }
function L($v) { Pt ($v * $script:T.sc) }

function Estilo($sh, $relleno, $borde, $grosor, $transp) {
    if ($relleno) { $sh.Fill.ForeColor.RGB = Rgb $relleno; $sh.Fill.Transparency = [single]$transp } else { $sh.Fill.Visible = 0 }
    if ($borde) { $sh.Line.ForeColor.RGB = Rgb $borde; $sh.Line.Weight = [single][Math]::Max(0.25, (L $grosor)) } else { $sh.Line.Visible = 0 }
    $sh.Shadow.Visible = 0
    $sh
}

function RR($x, $y, $w, $h, $r, $relleno, $borde = $null, $grosor = 0, $transp = 0) {
    $sh = $script:S.Shapes.AddShape(5, (X $x), (Y $y), (L $w), (L $h))
    $sh.Adjustments.Item(1) = [single]([Math]::Min(0.5, $r / [Math]::Min($w, $h)))
    Estilo $sh $relleno $borde $grosor $transp
}
function Circ($cx, $cy, $r, $relleno, $borde = $null, $grosor = 0, $transp = 0) {
    $sh = $script:S.Shapes.AddShape(9, (X ($cx - $r)), (Y ($cy - $r)), (L (2 * $r)), (L (2 * $r)))
    Estilo $sh $relleno $borde $grosor $transp
}
function Linea($x1, $y1, $x2, $y2, $color, $grosor, $guiones = 0) {
    $sh = $script:S.Shapes.AddLine((X $x1), (Y $y1), (X $x2), (Y $y2))
    $sh.Line.ForeColor.RGB = Rgb $color
    $sh.Line.Weight = [single](L $grosor)
    if ($guiones) { $sh.Line.DashStyle = $guiones }
    $sh
}
# Polígono cerrado (lista plana x1,y1,x2,y2...)
function Poli($pts, $relleno, $borde = $null, $grosor = 0, $transp = 0) {
    $ff = $script:S.Shapes.BuildFreeform(1, (X $pts[0]), (Y $pts[1]))
    for ($k = 2; $k -lt $pts.Count; $k += 2) { $ff.AddNodes(0, 1, (X $pts[$k]), (Y $pts[$k + 1])) }
    $ff.AddNodes(0, 1, (X $pts[0]), (Y $pts[1]))
    Estilo $ff.ConvertToShape() $relleno $borde $grosor $transp
}
# Curva abierta cuadrática (como 'M p0 Q q p2' del SVG), solo trazo
function Curva($x0, $y0, $qx, $qy, $x2, $y2, $color, $grosor) {
    $c1x = $x0 + 2 / 3 * ($qx - $x0); $c1y = $y0 + 2 / 3 * ($qy - $y0)
    $c2x = $x2 + 2 / 3 * ($qx - $x2); $c2y = $y2 + 2 / 3 * ($qy - $y2)
    $ff = $script:S.Shapes.BuildFreeform(1, (X $x0), (Y $y0))
    $ff.AddNodes(1, 1, (X $c1x), (Y $c1y), (X $c2x), (Y $c2y), (X $x2), (Y $y2))
    $sh = $ff.ConvertToShape()
    $sh.Fill.Visible = 0
    $sh.Line.ForeColor.RGB = Rgb $color; $sh.Line.Weight = [single](L $grosor); $sh.Shadow.Visible = 0
    $sh
}

function Agrupar($formas, $nombre) {
    $formas = @($formas | Where-Object { $_ })
    if ($formas.Count -eq 1) { $formas[0].Name = $nombre; return $formas[0] }
    $nombres = @()
    foreach ($f in $formas) { $script:unico++; $f.Name = "tmp$script:unico"; $nombres += $f.Name }
    $g = $script:S.Shapes.Range([string[]]$nombres).Group()
    $g.Name = $nombre
    $g
}

function Texto($x, $y, $w, $h, $texto, $tam, $color, $fuente = $Cuerpo, [switch]$negrita, $alinear = 1, $espaciado = 0) {
    $tb = $script:S.Shapes.AddTextbox(1, (Pt $x), (Pt $y), (Pt $w), (Pt $h))
    $tf = $tb.TextFrame2
    $tf.AutoSize = 0; $tf.WordWrap = -1
    $tf.MarginLeft = 0; $tf.MarginRight = 0; $tf.MarginTop = 0; $tf.MarginBottom = 0
    $tr = $tf.TextRange
    $tr.Text = $texto
    $tr.Font.Name = $fuente; $tr.Font.Size = Pt $tam
    $tr.Font.Fill.ForeColor.RGB = Rgb $color
    $tr.Font.Bold = $(if ($negrita) { -1 } else { 0 })
    if ($espaciado) { $tr.Font.Spacing = [single]$espaciado }
    $tr.ParagraphFormat.Alignment = $alinear
    $tb
}

# ---------- Piezas (coordenadas locales iguales a los SVG del kit) ----------
# Cada pieza: caja local (x, y, ancho, alto) y un bloque que la dibuja y devuelve sus formas.
$P = [ordered]@{}

# Palanca del tren
$P['placaTren'] = @{ caja = @(40, 20, 320, 480); dibujo = {
    RR 40 20 320 480 28 '1A2C47' '4A6283' 4
    foreach ($xy in @(68, 48), @(332, 48), @(68, 472), @(332, 472)) { Circ $xy[0] $xy[1] 7 '4A6283' } } }
$P['ranuraTren'] = @{ caja = @(150, 70, 36, 330); dibujo = { RR 150 70 36 330 18 '0A1120' '2A3F60' 3 } }
$P['marcasTren'] = @{ caja = @(90, 92, 48, 291); dibujo = {
    Linea 90 95 138 95 'EEF3FB' 7; Linea 112 380 138 380 'EEF3FB' 7 } }
$P['perillaArriba'] = @{ caja = @(118, 55, 100, 100); dibujo = { Perilla 168 105 } }
$P['perillaAbajo'] = @{ caja = @(118, 330, 100, 100); dibujo = { Perilla 168 380 } }
function Perilla($cx, $cy) {
    Circ $cx $cy 50 '161A22' '0A0D12' 4
    Circ $cx $cy 42 $null '3A414D' 7 | ForEach-Object { $_.Line.DashStyle = 4; $_ }
    Circ $cx $cy 25 '9AA8BF' '6F7D95' 3
    Circ $cx $cy 9 'EEF3FB'
}
function Luz($cx, $cy, $color, $apagada, [switch]$encendida) {
    if ($encendida) { Circ $cx $cy 48 $color -transp 0.82 }
    Circ $cx $cy 34 '0A1120' '4A6283' 4
    Circ $cx $cy 23 $(if ($encendida) { $color } else { $apagada })
    if ($encendida) { Circ ($cx - 7) ($cy - 7) 7 'FFFFFF' -transp 0.25 }
}
$P['rojaOn'] = @{ caja = @(237, 82, 96, 96); dibujo = { Luz 285 130 'FF5A5A' '4A1D24' -encendida } }
$P['rojaOff'] = @{ caja = @(251, 96, 68, 68); dibujo = { Luz 285 130 'FF5A5A' '4A1D24' } }
$P['verdeOn'] = @{ caja = @(237, 202, 96, 96); dibujo = { Luz 285 250 '4ADE80' '1C4430' -encendida } }
$P['verdeOff'] = @{ caja = @(251, 216, 68, 68); dibujo = { Luz 285 250 '4ADE80' '1C4430' } }

# Switch de encendido con guarda
$P['placaSwitch'] = @{ caja = @(60, 60, 240, 340); dibujo = {
    RR 60 60 240 340 26 '1A2C47' '4A6283' 4
    foreach ($xy in @(84, 84), @(276, 84), @(84, 376), @(276, 376)) { Circ $xy[0] $xy[1] 6 '4A6283' } } }
$P['marcasSwitch'] = @{ caja = @(150, 96, 60, 272); dibujo = {
    Linea 150 100 210 100 '4ADE80' 8; Linea 164 364 196 364 'FF5A5A' 8 } }
$P['tuerca'] = @{ caja = @(144, 214, 72, 72); dibujo = { Circ 180 250 36 '0A1120' '4A6283' 5 } }
$P['palancaSwitch'] = @{ caja = @(159, 125, 42, 127); dibujo = {
    Poli @(168, 252, 162, 150, 198, 150, 192, 252) 'C9D3E3'
    Poli @(170, 250, 166, 152, 176, 152, 178, 250) 'EEF3FB' -transp 0.3
    Circ 180 146 21 'EEF3FB' '9AA8BF' 3 } }
$P['guardaCerrada'] = @{ caja = @(100, 140, 160, 224); dibujo = {
    RR 100 140 160 18 9 '8A1F1F'
    RR 112 150 136 214 24 'E03A3A' '8A1F1F' 4
    RR 128 166 22 182 11 'FF7A7A' -transp 0.5
    Linea 150 330 210 330 '8A1F1F' 6 } }
$P['guardaAbierta'] = @{ caja = @(100, 6, 160, 144); dibujo = {
    Poli @(112, 140, 124, 14, 134, 6, 226, 6, 236, 14, 248, 140) 'B52A2A' '8A1F1F' 4
    Poli @(132, 130, 140, 24, 220, 24, 228, 130) '8A1F1F' -transp 0.45
    RR 100 132 160 18 9 '8A1F1F' } }

# Aviso de pérdida
$P['avisoCarcasa'] = @{ caja = @(60, 62, 520, 76); dibujo = {
    RR 60 62 520 76 22 '1B222E' '4A6283' 4
    Circ 82 100 5 '4A6283'; Circ 558 100 5 '4A6283' } }
$P['avisoLenteOn'] = @{ caja = @(80, 76, 480, 48); dibujo = {
    RR 80 76 480 48 16 'FFB020'
    RR 96 84 448 12 6 'FFF1C9' -transp 0.3 } }
$P['avisoLenteOff'] = @{ caja = @(80, 76, 480, 48); dibujo = { RR 80 76 480 48 16 '4A3A1C' } }
$P['avisoBrillo'] = @{ caja = @(20, 24, 600, 152); dibujo = {
    RR 20 24 600 152 40 'FFB020' -transp 0.88
    RR 40 44 560 112 30 'FFB020' -transp 0.82 } }

# PAPI (una unidad; las 4 del armado son copias)
function Papi($x, $color) {
    Circ ($x + 46) 32 44 $color -transp 0.82
    RR $x 0 92 64 14 '1A2C47' '4A6283' 4
    Circ ($x + 46) 32 22 $color
}
$P['papiCaja'] = @{ caja = @(0, 0, 92, 64); dibujo = { RR 0 0 92 64 14 '1A2C47' '4A6283' 4 } }
$P['papiBlanca'] = @{ caja = @(24, 10, 44, 44); dibujo = { Circ 46 32 22 'FFF4DC' } }
$P['papiRoja'] = @{ caja = @(24, 10, 44, 44); dibujo = { Circ 46 32 22 'FF3B30' } }
$P['papiHalo'] = @{ caja = @(2, -12, 88, 88); dibujo = { Circ 46 32 44 'FF3B30' -transp 0.75 } }

# Iconos
$P['mando'] = @{ caja = @(52, 32, 96, 154); dibujo = {
    RR 78 96 44 90 22 '1A2C47' '4A6283' 5
    Circ 100 80 48 '1A2C47' '4A6283' 5
    Circ 100 76 16 'FFB020' } }
$P['ondasVibracion'] = @{ caja = @(4, 36, 192, 88); dibujo = {
    Curva 36 50 24 80 36 110 'FFB020' 7; Curva 18 36 0 80 18 124 'FFB020' 7
    Curva 164 50 176 80 164 110 'FFB020' 7; Curva 182 36 200 80 182 124 'FFB020' 7 } }
$P['bocina'] = @{ caja = @(30, 40, 78, 120); dibujo = {
    Poli @(30, 76, 64, 76, 108, 40, 108, 160, 64, 124, 30, 124) '1A2C47' '4A6283' 5 } }
$P['ondasSonido'] = @{ caja = @(130, 34, 58, 132); dibujo = {
    Curva 130 74 144 100 130 126 'FFB020' 8; Curva 150 54 176 100 150 146 'FFB020' 8
    Curva 170 34 206 100 170 166 'FFB020' 8 } }

# Botón SUBIR flotante (el texto es editable)
$P['botonSubir'] = @{ caja = @(0, 0, 300, 110); dibujo = {
    $b = RR 0 0 300 110 24 '142238' '4ADE80' 6
    $t = $b.TextFrame2
    $t.TextRange.Text = 'SUBIR'; $t.TextRange.Font.Name = $Titulo; $t.TextRange.Font.Bold = -1
    $t.TextRange.Font.Size = L 50; $t.TextRange.Font.Spacing = [single]1.5
    $t.TextRange.Font.Fill.ForeColor.RGB = Rgb '4ADE80'
    $t.TextRange.ParagraphFormat.Alignment = 2; $t.VerticalAnchor = 3
    $t.MarginLeft = 0; $t.MarginRight = 0
    $b } }

# Dibuja una pieza con su caja local colocada en (x, y) a escala 'sc'
function Poner($clave, $x, $y, $sc, $nombre) {
    $c = $P[$clave].caja
    $script:T = @{ ox = $x - $c[0] * $sc; oy = $y - $c[1] * $sc; sc = $sc }
    $formas = & $P[$clave].dibujo
    Agrupar $formas $nombre
}
# Igual, pero centrada en una celda de la cuadrícula de piezas sueltas, con su rótulo abajo
function Celda($clave, $cx, $cy, $nombre, $rotulo, $ry, $max = 1.6, $area = 190) {
    $c = $P[$clave].caja
    $sc = [Math]::Min($max, [Math]::Min($area / $c[2], $area / $c[3]))
    $g = Poner $clave ($cx - $c[2] * $sc / 2) ($cy - $c[3] * $sc / 2) $sc $nombre
    if ($rotulo) { Texto ($cx - 120) $ry 240 70 $rotulo 26 '9FB0C8' -alinear 2 | Out-Null }
    $g
}
# Dibuja varias piezas con la misma transformación (para el ARMADO)
function Armar($claves, $x, $y, $sc, $nombre) {
    $script:T = @{ ox = $x; oy = $y; sc = $sc }
    $formas = @()
    foreach ($k in $claves) { $script:T = @{ ox = $x; oy = $y; sc = $sc }; $formas += & $P[$k].dibujo }
    Agrupar $formas $nombre
}

function Pagina($n, $encabezado, $texto) {
    $script:S = $pres.Slides.Add($n, 12)
    $script:S.FollowMasterBackground = 0
    $script:S.Background.Fill.Solid()
    $script:S.Background.Fill.ForeColor.RGB = Rgb '0D1424'
    Texto 80 82 920 40 "KIT DE PIEZAS · CONTROLES · $n / 2" 30 'FFB020' $Titulo -negrita -espaciado 6 | Out-Null
    Texto 80 150 920 110 $encabezado 84 'EEF3FB' $Titulo -negrita | Out-Null
    Texto 80 262 920 90 $texto 32 '9FB0C8' | Out-Null
}
function Seccion($y, $texto) { Texto 80 $y 920 40 $texto 28 'FFB020' $Titulo -negrita -espaciado 7 | Out-Null }

# ---------- PowerPoint ----------
$app = New-Object -ComObject PowerPoint.Application
$pres = $app.Presentations.Add(0)
try {
    $pres.PageSetup.SlideWidth = 540
    $pres.PageSetup.SlideHeight = 960

    # ═══ 1 · Palanca del tren y switch ═══
    Pagina 1 'Tren y switch' 'Palanca del tren y switch con guarda. Cada parte es una figura separada: cámbiale el color o úsala sola.'
    Seccion 370 'ARMADO'
    Armar @('placaTren', 'ranuraTren', 'marcasTren', 'rojaOff', 'verdeOn', 'perillaAbajo') 70 410 0.84 'Palanca del tren (abajo, verde)' | Out-Null
    Armar @('placaSwitch', 'marcasSwitch', 'tuerca', 'palancaSwitch', 'guardaAbierta') 520 420 0.98 'Switch encendido (guarda abierta)' | Out-Null
    Texto 38 860 400 40 'Tren abajo' 26 '9FB0C8' -alinear 2 | Out-Null
    Texto 516 860 400 40 'Switch en ON' 26 '9FB0C8' -alinear 2 | Out-Null
    Seccion 930 'PIEZAS SUELTAS'
    $cols = 195, 425, 655, 885
    Celda 'placaTren' $cols[0] 1080 'Placa del tren' 'Placa del tren' 1195 | Out-Null
    Celda 'ranuraTren' $cols[1] 1080 'Ranura' 'Ranura' 1195 | Out-Null
    Celda 'marcasTren' $cols[2] 1080 'Marcas UP / DN' 'Marcas UP / DN' 1195 | Out-Null
    Celda 'perillaAbajo' $cols[3] 1080 'Perilla de llanta' 'Perilla de llanta' 1195 -max 1.4 -area 150 | Out-Null
    Celda 'rojaOn' $cols[0] 1370 'Luz roja encendida' 'Luz roja' 1445 -area 130 | Out-Null
    Celda 'rojaOff' $cols[1] 1370 'Luz roja apagada' 'Roja apagada' 1445 -area 100 | Out-Null
    Celda 'verdeOn' $cols[2] 1370 'Luz verde encendida' 'Luz verde' 1445 -area 130 | Out-Null
    Celda 'verdeOff' $cols[3] 1370 'Luz verde apagada' 'Verde apagada' 1445 -area 100 | Out-Null
    Celda 'placaSwitch' $cols[0] 1650 'Placa del switch' 'Placa del switch' 1785 -area 180 | Out-Null
    Poner 'tuerca' ($cols[1] - 42) 1680 1.18 'Tuerca' | Out-Null
    Celda 'palancaSwitch' $cols[1] 1650 'Palanca del switch' 'Palanca y tuerca' 1785 -max 1.2 -area 150 | Out-Null
    Celda 'guardaCerrada' $cols[2] 1650 'Guarda cerrada' 'Guarda cerrada' 1785 -area 180 | Out-Null
    Celda 'guardaAbierta' $cols[3] 1650 'Guarda abierta' 'Guarda abierta' 1785 -area 160 | Out-Null

    # ═══ 2 · Avisos y luces ═══
    Pagina 2 'Avisos y luces' 'Barra de aviso de pérdida, luces PAPI de la pista, iconos de vibración y pitido, y el botón SUBIR.'
    Seccion 370 'ARMADO'
    Armar @('avisoBrillo', 'avisoCarcasa', 'avisoLenteOn') 120 400 1.3 'Aviso de pérdida (encendido)' | Out-Null
    $script:T = @{ ox = 150; oy = 690; sc = 1.0 }
    $papi = @(); foreach ($k in 0..3) { $script:T = @{ ox = 150 + $k * 200; oy = 740; sc = 1.0 }; $papi += Papi 0 $(if ($k -lt 2) { 'FFF4DC' } else { 'FF3B30' }) }
    Agrupar $papi 'PAPI (2 y 2)' | Out-Null
    Texto 80 668 920 40 'Aviso de pérdida' 26 '9FB0C8' -alinear 2 | Out-Null
    Texto 80 836 920 40 'PAPI: 2 blancas y 2 rojas' 26 '9FB0C8' -alinear 2 | Out-Null
    Seccion 910 'PIEZAS SUELTAS'
    Celda 'avisoCarcasa' 300 1010 'Carcasa del aviso' 'Carcasa del aviso' 1055 -area 400 | Out-Null
    Celda 'avisoLenteOn' 780 1010 'Lente encendida' 'Lente encendida' 1055 -area 380 | Out-Null
    Celda 'avisoLenteOff' 300 1150 'Lente apagada' 'Lente apagada' 1205 -area 380 | Out-Null
    Celda 'avisoBrillo' 780 1150 'Brillo del aviso' 'Brillo' 1205 -area 360 | Out-Null
    Celda 'papiCaja' $cols[0] 1360 'Caja PAPI' 'Caja PAPI' 1435 -area 150 | Out-Null
    Celda 'papiBlanca' $cols[1] 1360 'Lente blanca' 'Lente blanca' 1435 -area 80 | Out-Null
    Celda 'papiRoja' $cols[2] 1360 'Lente roja' 'Lente roja' 1435 -area 80 | Out-Null
    Celda 'papiHalo' $cols[3] 1360 'Halo' 'Halo (brillo)' 1435 -area 120 | Out-Null
    Celda 'mando' $cols[0] 1590 'Mando' 'Mando' 1690 -area 150 | Out-Null
    Celda 'ondasVibracion' $cols[1] 1590 'Ondas de vibración' 'Ondas de vibración' 1690 -area 190 | Out-Null
    Celda 'bocina' $cols[2] 1590 'Bocina' 'Bocina' 1690 -area 130 | Out-Null
    Celda 'ondasSonido' $cols[3] 1590 'Ondas de sonido' 'Ondas de sonido' 1690 -area 140 | Out-Null
    Celda 'botonSubir' 300 1820 'Botón SUBIR' '' 0 -max 0.8 -area 240 | Out-Null
    Texto 470 1800 520 44 'Botón SUBIR (el texto se edita)' 26 '9FB0C8' | Out-Null

    $pres.SaveAs($salida, 24)
    New-Item -ItemType Directory -Force $vistas | Out-Null
    for ($k = 1; $k -le $pres.Slides.Count; $k++) {
        $pres.Slides.Item($k).Export((Join-Path $vistas ("vista_{0}.png" -f $k)), 'PNG', 1080, 1920)
    }
    Write-Host "OK $salida"
}
finally {
    $pres.Close()
    $app.Quit()
}

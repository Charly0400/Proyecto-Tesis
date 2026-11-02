# Ilustración de "Pérdida de sustentación" para la tarjeta de la diapositiva (920 x 444, se exporta al doble).
# Usa el P-47 armado de Kit_P-47.pptx (Descargas) para que tenga el mismo estilo que el manual, y le agrega
# horizonte, ángulo, aire revuelto sobre el ala, flecha de caída y la barra de aviso.
# Genera: perdida/perdida_con_fondo.png (fondo #13203A, como la tarjeta) y perdida/perdida_editable.pptx
# Uso:  powershell -ExecutionPolicy Bypass -File Docs\Instrucciones\Kit_Canva\crear_imagen_perdida.ps1
$ErrorActionPreference = 'Stop'
$kitP47 = Join-Path $env:USERPROFILE "Downloads\Kit_P-47.pptx"
$dir = Join-Path $PSScriptRoot "perdida"
New-Item -ItemType Directory -Force $dir | Out-Null

function Rgb($hex) {
    [int]([Convert]::ToInt32($hex.Substring(0, 2), 16) + 256 * [Convert]::ToInt32($hex.Substring(2, 2), 16) + 65536 * [Convert]::ToInt32($hex.Substring(4, 2), 16))
}
$Titulo = 'Barlow'

function Etiqueta($s, $x, $y, $w, $texto, $color, $tam = 22, $alinear = 1) {
    $tb = $s.Shapes.AddTextbox(1, $x, $y, $w, 30)
    $tf = $tb.TextFrame2
    $tf.WordWrap = -1; $tf.MarginLeft = 0; $tf.MarginRight = 0; $tf.MarginTop = 0; $tf.MarginBottom = 0
    $tf.TextRange.Text = $texto
    $tf.TextRange.Font.Name = $Titulo; $tf.TextRange.Font.Bold = -1; $tf.TextRange.Font.Size = $tam
    $tf.TextRange.Font.Spacing = [single]0.5
    $tf.TextRange.Font.Fill.ForeColor.RGB = Rgb $color
    $tf.TextRange.ParagraphFormat.Alignment = $alinear
    $tb
}
function Trazo($sh, $color, $grosor, $transp = 0) {
    $sh.Line.ForeColor.RGB = Rgb $color; $sh.Line.Weight = [single]$grosor; $sh.Line.Transparency = [single]$transp
    $sh.Shadow.Visible = 0
    $sh
}
function Relleno($sh, $color, $transp = 0) {
    $sh.Fill.ForeColor.RGB = Rgb $color; $sh.Fill.Transparency = [single]$transp; $sh.Line.Visible = 0; $sh.Shadow.Visible = 0
    $sh
}
# Rizo de aire: curva en S suave que avanza a la derecha
function Rizo($s, $x, $y, $largo, $alto) {
    $ff = $s.Shapes.BuildFreeform(1, $x, $y)
    $paso = $largo / 4
    for ($k = 0; $k -lt 4; $k++) {
        $x0 = $x + $k * $paso; $sg = $(if ($k % 2) { 1 } else { -1 })
        $ff.AddNodes(1, 1, $x0 + $paso * 0.3, $y + $sg * $alto, $x0 + $paso * 0.7, $y + $sg * $alto, $x0 + $paso, $y)
    }
    $sh = $ff.ConvertToShape(); $sh.Fill.Visible = 0
    Trazo $sh '3FD3EA' 2.5 0.15
}

$app = New-Object -ComObject PowerPoint.Application
$src = $app.Presentations.Open($kitP47, -1, 0, 0)
$pres = $app.Presentations.Add(0)
try {
    $pres.PageSetup.SlideWidth = 920
    $pres.PageSetup.SlideHeight = 444
    $s = $pres.Slides.Add(1, 12)
    $s.FollowMasterBackground = 0
    $s.Background.Fill.Solid()
    $s.Background.Fill.ForeColor.RGB = Rgb '13203A'

    # Centro del avión (también el vértice del ángulo) y su inclinación
    $cx = 340; $cy = 300; $grados = 24

    # Horizonte
    Trazo ($s.Shapes.AddLine(30, $cy, 890, $cy)) '3A4F75' 2 | ForEach-Object { $_.Line.DashStyle = 4; $_.Name = 'Horizonte' } | Out-Null

    # Ángulo entre el horizonte y el eje del avión
    $r = 265
    # Trazo libre sobre el círculo (la forma 'Arco' de PowerPoint mueve su marco al cambiarle los ángulos)
    $ff = $s.Shapes.BuildFreeform(1, $cx - $r, $cy)
    for ($a = 1; $a -le $grados; $a++) {
        $rad = $a * [Math]::PI / 180
        $ff.AddNodes(0, 1, $cx - $r * [Math]::Cos($rad), $cy - $r * [Math]::Sin($rad))
    }
    $arco = $ff.ConvertToShape()
    $arco.Fill.Visible = 0
    Trazo $arco 'FF5A5A' 3.5 | Out-Null
    $arco.Name = 'Ángulo'
    # Eje del avión (queda detrás del avión: solo se ve la parte que sale por la nariz)
    $eje = $s.Shapes.AddLine($cx, $cy, $cx - ($r + 10) * [Math]::Cos($grados * [Math]::PI / 180), $cy - ($r + 10) * [Math]::Sin($grados * [Math]::PI / 180))
    Trazo $eje 'FF5A5A' 2.5 | Out-Null
    $eje.Line.DashStyle = 4; $eje.Name = 'Eje del avión'
    Etiqueta $s 40 ($cy - 175) 200 'NARIZ MUY' 'FF5A5A' 22 | ForEach-Object { $_.Name = 'Texto nariz 1' } | Out-Null
    Etiqueta $s 40 ($cy - 150) 200 'ARRIBA' 'FF5A5A' 22 | ForEach-Object { $_.Name = 'Texto nariz 2' } | Out-Null

    # P-47 del kit (formas 5 a 31 de la diapositiva 1: el ARMADO), sin el tren: en vuelo va guardado
    # (15 compuerta, 16 pata de cola, 20 pata del tren, 21 rueda de cola, 24 rueda, 25 rin de cola, 27 rin)
    $indices = [int[]](5..31 | Where-Object { $_ -notin 15, 16, 20, 21, 24, 25, 27 })
    $src.Slides.Item(1).Shapes.Range($indices).Copy()
    $pegado = $s.Shapes.Paste()
    $avion = $pegado.Group()
    $avion.Name = 'P-47'
    $avion.LockAspectRatio = -1
    $avion.Width = 400
    $avion.Left = $cx - $avion.Width / 2
    $avion.Top = $cy - $avion.Height / 2
    $avion.Rotation = $grados

    # Aire revuelto sobre el ala (ya no la sostiene)
    $rizos = @()
    $rizos += Rizo $s ($cx + 10) ($cy - 74) 120 7
    $rizos += Rizo $s ($cx + 55) ($cy - 46) 130 8
    $rizos += Rizo $s ($cx + 100) ($cy - 18) 120 7
    $g = $s.Shapes.Range([string[]]($rizos | ForEach-Object { $_.Name })).Group(); $g.Name = 'Aire revuelto'
    Etiqueta $s ($cx + 60) ($cy - 118) 220 'AIRE REVUELTO' '3FD3EA' 18 | ForEach-Object { $_.Name = 'Texto aire' } | Out-Null

    # Flecha de caída
    $flecha = Relleno ($s.Shapes.AddShape(36, 700, 150, 70, 160)) 'FF5A5A'
    $flecha.Name = 'Flecha cae'
    Etiqueta $s 660 320 150 'CAE' 'FF5A5A' 26 2 | ForEach-Object { $_.Name = 'Texto cae' } | Out-Null


    # Barra de aviso (como la del tablero), arriba a la derecha
    $brillo = Relleno ($s.Shapes.AddShape(5, 570, 22, 320, 56)) 'FFB020' 0.82; $brillo.Adjustments.Item(1) = [single]0.5
    $caja = $s.Shapes.AddShape(5, 584, 30, 292, 40); $caja.Adjustments.Item(1) = [single]0.4
    $caja.Fill.ForeColor.RGB = Rgb '1B222E'; Trazo $caja '4A6283' 2 | Out-Null
    $lente = Relleno ($s.Shapes.AddShape(5, 594, 37, 272, 26)) 'FFB020'; $lente.Adjustments.Item(1) = [single]0.4
    $txt = $lente.TextFrame2
    $txt.TextRange.Text = '¡AVISO DE PÉRDIDA!'
    $txt.TextRange.Font.Name = $Titulo; $txt.TextRange.Font.Bold = -1; $txt.TextRange.Font.Size = 15
    $txt.TextRange.Font.Fill.ForeColor.RGB = Rgb '1A1205'
    $txt.TextRange.ParagraphFormat.Alignment = 2; $txt.VerticalAnchor = 3
    $txt.MarginLeft = 0; $txt.MarginRight = 0; $txt.MarginTop = 0; $txt.MarginBottom = 0
    $g = $s.Shapes.Range([string[]]@($brillo.Name, $caja.Name, $lente.Name)).Group(); $g.Name = 'Barra de aviso'

    $pres.SaveAs((Join-Path $dir "perdida_editable.pptx"), 24)
    $s.Export((Join-Path $dir "perdida_con_fondo.png"), 'PNG', 1840, 888)

    Write-Host "OK $dir"
}
finally {
    $pres.Close()
    $src.Close()
    $app.Quit()
}

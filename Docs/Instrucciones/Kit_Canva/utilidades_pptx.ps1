# Utilidades comunes para armar diapositivas del manual con PowerPoint (COM), en el estilo del manual de Canva.
# Se cargan con:  . (Join-Path $PSScriptRoot 'utilidades_pptx.ps1')   (antes, definir $png = carpeta de imágenes)

# ---------- Utilidades (todo se escribe en píxeles de la diapositiva de 1080 x 1920) ----------
function Px($v) { [single]($v * 0.75) }   # 1080 px = 810 pt
function Rgb($hex) {
    [int]([Convert]::ToInt32($hex.Substring(0, 2), 16) + 256 * [Convert]::ToInt32($hex.Substring(2, 2), 16) + 65536 * [Convert]::ToInt32($hex.Substring(4, 2), 16))
}
$C = @{ bg = '0D1424'; card = '131F36'; line = '26385A'; text = 'EEF3FB'; muted = '9FB0C8'; amber = 'FFB020'
        cyan = '3FD3EA'; green = '4ADE80'; red = 'FF5A5A'; off = '3A4660'; dark = '0B1222' }
$Titulo = 'Barlow'; $Cuerpo = 'Noto Sans'

function Caja($s, $x, $y, $w, $h, $relleno, $borde = $null, $radio = 26, $nombre = $null, $transp = 0) {
    $sh = $s.Shapes.AddShape(5, (Px $x), (Px $y), (Px $w), (Px $h))
    $sh.Adjustments.Item(1) = [single]([Math]::Min(0.5, $radio / [Math]::Min($w, $h)))
    $sh.Fill.ForeColor.RGB = Rgb $relleno
    $sh.Fill.Transparency = [single]$transp
    if ($borde) { $sh.Line.ForeColor.RGB = Rgb $borde; $sh.Line.Weight = Px 2 } else { $sh.Line.Visible = 0 }
    $sh.Shadow.Visible = 0
    if ($nombre) { $sh.Name = $nombre }
    $sh
}

# Texto con marcas: **negritas** en blanco y [[negritas]] en ámbar. Un salto de línea (`n) separa el renglón
# principal del secundario (más chico y en gris) cuando se pasa $chico.
function Texto($s, $x, $y, $w, $h, $texto, $tam, $color = $C.text, $fuente = $Cuerpo, [switch]$negrita,
               $alinear = 1, $anclar = 1, $espaciado = 0, $chico = 0, $nombre = $null, $interlineado = 1.2) {
    $tb = $s.Shapes.AddTextbox(1, (Px $x), (Px $y), (Px $w), (Px $h))
    $tf = $tb.TextFrame2
    $tf.AutoSize = 0; $tf.WordWrap = -1
    $tf.MarginLeft = 0; $tf.MarginRight = 0; $tf.MarginTop = 0; $tf.MarginBottom = 0
    $tf.VerticalAnchor = $anclar

    # Quitar las marcas y recordar dónde iban
    $marcas = @(); $plano = ''
    foreach ($parte in [regex]::Split($texto, '(\*\*.+?\*\*|\[\[.+?\]\])')) {
        if ($parte -eq '') { continue }
        if ($parte.StartsWith('**')) { $t = $parte.Substring(2, $parte.Length - 4); $marcas += , @($plano.Length, $t.Length, $C.text) }
        elseif ($parte.StartsWith('[[')) { $t = $parte.Substring(2, $parte.Length - 4); $marcas += , @($plano.Length, $t.Length, $C.amber) }
        else { $t = $parte }
        $plano += $t
    }
    $plano = $plano -replace "`n", "`r"
    $tr = $tf.TextRange
    $tr.Text = $plano
    $tr.Font.Name = $fuente
    $tr.Font.Size = Px $tam
    $tr.Font.Fill.ForeColor.RGB = Rgb $color
    $tr.Font.Bold = $(if ($negrita) { -1 } else { 0 })
    if ($espaciado) { $tr.Font.Spacing = [single]$espaciado }
    $tr.ParagraphFormat.Alignment = $alinear
    $tr.ParagraphFormat.SpaceWithin = [single]$interlineado
    foreach ($m in $marcas) {
        $r = $tr.Characters($m[0] + 1, $m[1])
        $r.Font.Bold = -1
        $r.Font.Fill.ForeColor.RGB = Rgb $m[2]
    }
    if ($chico -and $tr.Paragraphs().Count -gt 1) {
        $p2 = $tr.Paragraphs(2)
        $p2.Font.Size = Px $chico
        $p2.Font.Fill.ForeColor.RGB = Rgb $C.muted
        $p2.ParagraphFormat.SpaceBefore = Px 4
    }
    if ($nombre) { $tb.Name = $nombre }
    $tb
}

function Imagen($s, $archivo, $x, $y, $w, $h, $nombre = $null) {
    $ruta = $(if ([IO.Path]::IsPathRooted($archivo)) { $archivo } else { Join-Path $png $archivo })
    $pic = $s.Shapes.AddPicture($ruta, 0, -1, (Px $x), (Px $y), (Px $w), (Px $h))
    if ($nombre) { $pic.Name = $nombre }
    $pic
}

function Flecha($s, $x1, $y1, $x2, $y2, $color = $C.amber, $grosor = 9) {
    $l = $s.Shapes.AddLine((Px $x1), (Px $y1), (Px $x2), (Px $y2))
    $l.Line.ForeColor.RGB = Rgb $color
    $l.Line.Weight = Px $grosor
    $l.Line.EndArrowheadStyle = 2
    $l.Line.EndArrowheadLength = 2; $l.Line.EndArrowheadWidth = 2
    $l
}

function Agrupar($s, $formas, $nombre) {
    # PowerPoint repite nombres automáticos dentro de los grupos: se renombran a nombres únicos antes de agruparlas
    $nombres = @()
    foreach ($f in $formas) {
        $script:unico++
        $original = $f.Name
        $f.Name = "tmp$script:unico"
        $f.AlternativeText = $original
        $nombres += $f.Name
    }
    try {
        $g = $s.Shapes.Range([string[]]$nombres).Group()
        $g.Name = $nombre
        foreach ($k in 1..$g.GroupItems.Count) { $item = $g.GroupItems.Item($k); $item.Name = $item.AlternativeText; $item.AlternativeText = '' }
        $g
    }
    catch { Write-Warning "No se pudo agrupar '$nombre': $($_.Exception.Message)" }
}

$script:contador = 0
$script:unico = 0
function Nombre($base) { $script:contador++; "$base $script:contador" }

function Fondo($s) {
    $s.FollowMasterBackground = 0
    $s.Background.Fill.Solid()
    $s.Background.Fill.ForeColor.RGB = Rgb $C.bg
}

# Etiqueta, contador y barra de progreso de 21 segmentos (las primeras 'llenas' en ámbar)
function Encabezado($s, $etiqueta, $derecha, $llenas) {
    Texto $s 80 88 700 44 $etiqueta 30 $C.amber $Titulo -negrita -espaciado 7 -nombre 'Etiqueta' | Out-Null
    Texto $s 600 88 400 44 $derecha 30 $C.muted $Titulo -alinear 3 -nombre 'Contador' | Out-Null
    if ($llenas -ge 0) { Barra $s 150 $llenas }
}

function Barra($s, $y, $llenas) {
    $seg = (920 - 20 * 10) / 21; $partes = @()
    for ($k = 0; $k -lt 21; $k++) {
        $color = $(if ($k -lt $llenas) { $C.amber } else { $C.line })
        $partes += Caja $s (80 + $k * ($seg + 10)) $y $seg 6 $color $null 3 "Progreso $($k + 1)"
    }
    Agrupar $s $partes 'Barra de progreso' | Out-Null
}

# Fila tipo tarjeta: pastilla de color a la izquierda y texto (con renglón chico opcional) a la derecha
function Fila($s, $y, $h, $pastilla, $colorPastilla, $texto, $colorTextoPastilla = $C.dark, $imagen = $null, $anchoImagen = 250) {
    $n = Nombre 'Fila'
    $partes = @(Caja $s 80 $y 920 $h $C.card $C.line 26 "$n fondo")
    if ($imagen) {
        $alto = $anchoImagen * $imagen[1]
        $partes += Imagen $s $imagen[0] (116 + (250 - $anchoImagen) / 2) ($y + ($h - $alto) / 2) $anchoImagen $alto "$n imagen"
    } else {
        $p = Caja $s 116 ($y + ($h - 62) / 2) 250 62 $colorPastilla $null 14 "$n pastilla"
        $p.TextFrame2.TextRange.Text = $pastilla
        $p.TextFrame2.TextRange.Font.Name = $Titulo
        $p.TextFrame2.TextRange.Font.Bold = -1
        $p.TextFrame2.TextRange.Font.Size = Px 32
        $p.TextFrame2.TextRange.Font.Fill.ForeColor.RGB = Rgb $colorTextoPastilla
        $p.TextFrame2.TextRange.ParagraphFormat.Alignment = 2
        $p.TextFrame2.VerticalAnchor = 3
        $p.TextFrame2.MarginLeft = 0; $p.TextFrame2.MarginRight = 0
        $partes += $p
    }
    $partes += Texto $s 400 ($y + 16) 570 ($h - 32) $texto 36 -anclar 3 -chico 29 -nombre "$n texto"
    Agrupar $s $partes $n | Out-Null
}

# Paso numerado (círculo ámbar + texto)
function Paso($s, $y, $h, $numero, $texto) {
    $n = Nombre 'Paso'
    # Círculo y texto centrados en la altura del paso
    $circ = $s.Shapes.AddShape(9, (Px 80), (Px ($y + ($h - 76) / 2)), (Px 76), (Px 76))
    $circ.Fill.ForeColor.RGB = Rgb $C.amber; $circ.Line.Visible = 0; $circ.Shadow.Visible = 0; $circ.Name = "$n círculo"
    $circ.TextFrame2.TextRange.Text = "$numero"
    $circ.TextFrame2.TextRange.Font.Name = $Titulo; $circ.TextFrame2.TextRange.Font.Bold = -1
    $circ.TextFrame2.TextRange.Font.Size = Px 40
    $circ.TextFrame2.TextRange.Font.Fill.ForeColor.RGB = Rgb '1A1205'
    $circ.TextFrame2.TextRange.ParagraphFormat.Alignment = 2; $circ.TextFrame2.VerticalAnchor = 3
    $circ.TextFrame2.MarginLeft = 0; $circ.TextFrame2.MarginRight = 0; $circ.TextFrame2.MarginTop = 0; $circ.TextFrame2.MarginBottom = 0
    $t = Texto $s 186 $y 814 $h $texto 38 -anclar 3 -nombre "$n texto"
    Agrupar $s @($circ, $t) $n | Out-Null
}

# Recuadro TIP (ámbar) u OJO (rojo)
function Aviso($s, $y, $h, $texto, [switch]$ojo) {
    $n = Nombre $(if ($ojo) { 'OJO' } else { 'TIP' })
    $color = $(if ($ojo) { $C.red } else { $C.amber })
    $fondo = Caja $s 80 $y 920 $h $color $color 26 "$n fondo" 0.92
    $fondo.Line.Transparency = [single]0.45
    $p = Caja $s 116 ($y + ($h - 54) / 2) 96 54 $color $null 12 "$n etiqueta"
    $p.TextFrame2.TextRange.Text = $(if ($ojo) { 'OJO' } else { 'TIP' })
    $p.TextFrame2.TextRange.Font.Name = $Titulo; $p.TextFrame2.TextRange.Font.Bold = -1
    $p.TextFrame2.TextRange.Font.Size = Px 30; $p.TextFrame2.TextRange.Font.Spacing = [single]2
    $p.TextFrame2.TextRange.Font.Fill.ForeColor.RGB = Rgb $(if ($ojo) { '200606' } else { '1A1205' })
    $p.TextFrame2.TextRange.ParagraphFormat.Alignment = 2; $p.TextFrame2.VerticalAnchor = 3
    $p.TextFrame2.MarginLeft = 0; $p.TextFrame2.MarginRight = 0
    $t = Texto $s 240 ($y + 20) 730 ($h - 40) $texto 34 -anclar 3 -nombre "$n texto"
    Agrupar $s @($fondo, $p, $t) $n | Out-Null
}

function Tarjeta($s, $y, $h, $nombre) { Caja $s 80 $y 920 $h $C.card $C.line 34 $nombre }

function Subtitulo($s, $y, $texto, $color = $C.amber) { Texto $s 80 $y 920 50 $texto 34 $color $Titulo -negrita | Out-Null }
function Nota($s, $y, $h, $texto) { Texto $s 80 $y 920 $h $texto 30 $C.muted | Out-Null }

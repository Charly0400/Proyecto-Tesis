# Diapositiva 18 · Pérdida de sustentación, completa y con la info nueva (aviso: barra ámbar, vibración y pitido).
# Usa la ilustración de perdida/perdida_con_fondo.png (crear_imagen_perdida.ps1). Genera la diapositiva editable
# (Diapositiva_18_Perdida.pptx) y su imagen (diapositivas/Diapositiva_18_Perdida.png), 1080 x 1920.
# Uso:  powershell -ExecutionPolicy Bypass -File Docs\Instrucciones\Kit_Canva\crear_diapo_perdida.ps1
$ErrorActionPreference = 'Stop'
$kit = $PSScriptRoot
$png = Join-Path $kit "png"
$ilustracion = Join-Path $kit "perdida\perdida_con_fondo.png"
$salida = Join-Path $kit "Diapositiva_18_Perdida.pptx"
$imagen = Join-Path $kit "diapositivas\Diapositiva_18_Perdida.png"

. (Join-Path $PSScriptRoot 'utilidades_pptx.ps1')

# Fila compacta: icono centrado en una columna de 180 px y texto a la derecha
function FilaIcono($s, $y, $h, $archivo, $ancho, $alto, $texto) {
    $n = Nombre 'Aviso'
    $partes = @(Caja $s 80 $y 920 $h $C.card $C.line 26 "$n fondo")
    $partes += Imagen $s $archivo (100 + (180 - $ancho) / 2) ($y + ($h - $alto) / 2) $ancho $alto "$n icono"
    $partes += Texto $s 310 ($y + 10) 660 ($h - 20) $texto 32 -anclar 3 -nombre "$n texto"
    Agrupar $s $partes $n | Out-Null
}

$app = New-Object -ComObject PowerPoint.Application
$pres = $app.Presentations.Add(0)
try {
    $pres.PageSetup.SlideWidth = Px 1080
    $pres.PageSetup.SlideHeight = Px 1920
    $s = $pres.Slides.Add(1, 12)
    Fondo $s

    Encabezado $s 'VUELO' '18 / 21' 18
    Texto $s 80 200 920 95 'Pérdida de sustentación' 72 $C.text $Titulo -negrita -nombre 'Título' | Out-Null
    Texto $s 80 312 920 110 'Si la nariz apunta **demasiado arriba** o vas muy lento, las alas dejan de sostener al avión y **cae**.' 36 $C.muted -nombre 'Texto' -interlineado 1.3 | Out-Null

    # Ilustración en una tarjeta redondeada (la imagen ya trae el color de la tarjeta)
    $art = Imagen $s $ilustracion 80 440 920 444 'Ilustración'
    $art.AutoShapeType = 5
    $art.Adjustments.Item(1) = [single](34 / 444)
    $art.Line.Visible = -1; $art.Line.ForeColor.RGB = Rgb $C.line; $art.Line.Weight = Px 2

    Subtitulo $s 916 'El avión te avisa:' $C.text
    FilaIcono $s 974 96 'aviso_perdida_encendido.png' 170 53 'La **barra ámbar** del tablero parpadea'
    FilaIcono $s 1082 96 'icono_vibracion.png' 72 72 'El **mando vibra** en la mano del joystick'
    FilaIcono $s 1190 96 'icono_pitido.png' 72 72 'Suena un **pitido**'

    Subtitulo $s 1318 'Para recuperarte:' $C.text
    Paso $s 1376 80 1 '[[Empuja]] el joystick para bajar la nariz.'
    Paso $s 1466 80 2 'Acelerador al [[100%]].'
    Paso $s 1556 110 3 'Nivela las alas y, cuando recuperes velocidad, sube [[suave]].'

    Aviso $s 1700 140 'Si parpadea y pita [[más rápido]], ya estás en pérdida. En tierra no avisa.'

    $pres.SaveAs($salida, 24)
    $s.Export($imagen, 'PNG', 1080, 1920)
    Write-Host "OK $salida"
}
finally {
    $pres.Close()
    $app.Quit()
}

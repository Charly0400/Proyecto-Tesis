# Arma "Manual_v2_nuevas_y_piezas.pptx" con PowerPoint (COM): las 2 diapositivas nuevas y páginas de "piezas"
# (filas, pasos, TIP/OJO e imágenes ya armados) para copiar a las diapositivas que cambian. Todo es nativo y editable:
# cuadros de texto, formas y las ilustraciones del kit en PNG. Tamaño 1080 x 1920 (formato teléfono).
# Uso:  powershell -ExecutionPolicy Bypass -File Docs\Instrucciones\Kit_Canva\crear_pptx.ps1
$ErrorActionPreference = 'Stop'
$kit = $PSScriptRoot
$png = Join-Path $kit "png"
$salida = Join-Path $kit "Manual_v2_nuevas_y_piezas.pptx"
$vistas = Join-Path $kit "pptx_vista_previa"

. (Join-Path $PSScriptRoot 'utilidades_pptx.ps1')

# ---------- PowerPoint ----------
$app = New-Object -ComObject PowerPoint.Application
$pres = $app.Presentations.Add(0)
try {
    $pres.PageSetup.SlideWidth = Px 1080
    $pres.PageSetup.SlideHeight = Px 1920
    $i = 0
    function Nueva() { $script:i++; $s = $pres.Slides.Add($script:i, 12); Fondo $s; $s }

    # ═══ 09 · Tren de aterrizaje ═══
    $s = Nueva
    Encabezado $s 'CABINA' '09 / 21' 9
    Texto $s 80 205 920 110 'Tren de aterrizaje' 84 $C.text $Titulo -negrita -nombre 'Título' | Out-Null
    Texto $s 80 330 920 170 'La palanca con **perilla de llanta**, a la izquierda del tablero. Agárrala con el grip: [[arriba]] guarda las ruedas, [[abajo]] las saca.' 38 $C.muted -nombre 'Texto' -interlineado 1.3 | Out-Null
    $p = @(Tarjeta $s 527 528 'Ilustración fondo')
    $p += Imagen $s 'palanca_tren_abajo.png' 180 600 240 312 'Palanca abajo'
    $p += Imagen $s 'palanca_tren_arriba.png' 660 600 240 312 'Palanca arriba'
    $p += Texto $s 140 980 320 44 'ABAJO · DN' 32 $C.green $Titulo -negrita -alinear 2
    $p += Texto $s 620 980 320 44 'ARRIBA · UP' 32 $C.muted $Titulo -negrita -alinear 2
    $p += Texto $s 420 680 240 36 'EN EL AIRE' 26 $C.amber $Titulo -negrita -alinear 2
    $p += Flecha $s 490 745 590 745
    $p += Flecha $s 590 825 490 825
    $p += Texto $s 400 865 280 36 'PARA ATERRIZAR' 26 $C.amber $Titulo -negrita -alinear 2
    Agrupar $s $p 'Ilustración' | Out-Null
    Fila $s 1097 142 'VERDE' $C.green "Tren abajo y asegurado`nYa puedes aterrizar"
    Fila $s 1263 142 'ROJA' $C.red "El tren se está moviendo`nTarda unos 6 segundos"
    Fila $s 1429 118 'APAGADAS' $C.off 'Tren arriba (guardado)' $C.text
    Aviso $s 1640 200 'En tierra no sube: la palanca **regresa sola**. Si aterrizas con el tren arriba tocas con la panza y **cuenta como choque**.' -ojo

    # ═══ 17 · Luces PAPI ═══
    $s = Nueva
    Encabezado $s 'VUELO' '17 / 21' 17
    Texto $s 80 205 920 110 'Luces PAPI' 84 $C.text $Titulo -negrita -nombre 'Título' | Out-Null
    Texto $s 80 330 920 120 '4 luces a la **izquierda de la pista** que te dicen si bajas con el **ángulo correcto** para aterrizar.' 38 $C.muted -nombre 'Texto' -interlineado 1.3 | Out-Null
    $p = @(Tarjeta $s 475 458 'Ilustración fondo')
    $p += Imagen $s 'papi_senda.png' 84 487 900 400 'Senda PAPI'
    $p += Texto $s 106 505 320 40 'MUY ALTO' 28 'FFF4DC' $Titulo -negrita
    $p += Texto $s 106 630 320 40 'CORRECTO (3°)' 28 $C.green $Titulo -negrita
    $p += Texto $s 106 725 320 40 'MUY BAJO' 28 $C.red $Titulo -negrita
    $p += Texto $s 576 872 140 36 'PAPI' 26 $C.muted $Titulo -negrita -alinear 2
    $p += Texto $s 774 872 140 36 'PISTA' 26 $C.muted $Titulo -negrita -alinear 2
    Agrupar $s $p 'Ilustración' | Out-Null
    Fila $s 977 142 '' '' "[[4 blancas:]] vas muy alto`nBaja la nariz o reduce un poco" -imagen @('papi_alto.png', 0.25)
    Fila $s 1143 142 '' '' "[[2 y 2:]] ¡vas perfecto!`nMantén esa bajada hasta la pista" -imagen @('papi_correcto.png', 0.25)
    Fila $s 1309 142 '' '' "[[4 rojas:]] vas muy bajo`nSube un poco la nariz o acelera" -imagen @('papi_bajo.png', 0.25)
    Aviso $s 1640 200 'Hay un PAPI en **cada extremo** de la pista. Para encontrar el aeródromo busca el **faro verde y blanco** que gira en la torre.'

    # ═══ Piezas · 03 Subir y bajar ═══
    $s = Nueva
    Encabezado $s 'PIEZAS · DIAPOSITIVA 03' 'Copiar y pegar' -1
    Texto $s 80 160 920 110 'Subir y bajar del avión' 72 $C.text $Titulo -negrita | Out-Null
    Nota $s 270 90 'Cambia el paso 1 y agrega el TIP. La imagen es opcional: muestra desde dónde funciona el botón.'
    Paso $s 400 200 1 'Acércate por un **costado** de la cabina: el botón [[SUBIR]] gira hacia ti. Si se ve transparente, muévete hasta que se vea completo.'
    $p = @(Tarjeta $s 640 700 'Ilustración fondo')
    $p += Imagen $s 'boton_subir_sectores.png' 230 690 600 600 'Botón subir sectores'
    $p += Texto $s 110 950 160 44 'AQUÍ SÍ' 30 $C.green $Titulo -negrita -alinear 2
    $p += Texto $s 810 950 160 44 'AQUÍ SÍ' 30 $C.green $Titulo -negrita -alinear 2
    $p += Texto $s 380 656 300 40 'AQUÍ NO (frente)' 26 $C.muted $Titulo -negrita -alinear 2
    $p += Texto $s 380 1286 300 40 'AQUÍ NO (atrás)' 26 $C.muted $Titulo -negrita -alinear 2
    Agrupar $s $p 'Ilustración' | Out-Null
    Aviso $s 1390 160 'Desde el suelo **no puedes mover** los controles de la cabina: primero siéntate.'

    # ═══ Piezas · 04 Encender el motor ═══
    $s = Nueva
    Encabezado $s 'PIEZAS · DIAPOSITIVA 04' 'Copiar y pegar' -1
    Texto $s 80 160 920 110 'Encender el motor' 72 $C.text $Titulo -negrita | Out-Null
    Nota $s 270 90 'Reemplaza la imagen del switch y la fila ON. Agrega el TIP. Verifica que la cabina del P-47 aún tenga el botón RESET.'
    $p = @(Tarjeta $s 400 560 'Ilustración fondo')
    $p += Imagen $s 'switch_guarda_cerrada.png' 150 440 300 383 'Switch guarda cerrada'
    $p += Imagen $s 'switch_guarda_abierta.png' 630 440 300 383 'Switch guarda abierta'
    $p += Flecha $s 480 640 600 640 $C.green
    $p += Texto $s 150 860 300 44 'APAGADO' 32 $C.red $Titulo -negrita -alinear 2
    $p += Texto $s 630 860 300 44 'ENCENDIDO' 32 $C.green $Titulo -negrita -alinear 2
    Agrupar $s $p 'Ilustración' | Out-Null
    Fila $s 1010 160 'ON ▲' $C.green 'Acerca la mano: la **guarda roja** se levanta sola. Agarra el switch con el grip y [[súbelo]].'
    Aviso $s 1220 170 'También se cambia con un **toque rápido** (agarrar y soltar). El mando vibra cuando cambia.'

    # ═══ Piezas · 15 Despegue y 16 Aterrizaje ═══
    $s = Nueva
    Encabezado $s 'PIEZAS · DIAPOSITIVAS 15 Y 16' 'Copiar y pegar' -1
    Texto $s 80 160 920 110 'Despegue y aterrizaje' 72 $C.text $Titulo -negrita | Out-Null
    Subtitulo $s 300 'Para la 15 · Despegue (paso nuevo y OJO)'
    Paso $s 370 120 6 'Ya en el aire, [[sube el tren]] (palanca arriba). La luz roja se apaga cuando termina.'
    Aviso $s 510 200 'No levantes demasiado la nariz: el avión pierde velocidad y **entra en pérdida**. Si suena un **pitido** y parpadea la **barra ámbar**, baja la nariz.' -ojo
    Subtitulo $s 770 'Para la 16 · Aterrizaje (pasos nuevos y OJO)'
    Paso $s 840 120 2 '[[Baja el tren]] a tiempo y espera la **luz verde**.'
    Paso $s 960 120 4 'Usa las [[luces PAPI]]: 2 blancas y 2 rojas = vas bien (ver diapositiva 17).'
    Aviso $s 1100 160 'Aterrizar **sin tren**, muy fuerte o muy inclinado **cuenta como choque**.' -ojo
    Nota $s 1300 240 "Orden de los pasos del aterrizaje:`n1 Alinéate · 2 Baja el tren · 3 Reduce el acelerador · 4 Usa las PAPI · 5 Nivela las alas · 6 Toca, acelerador a 0 y frena.`nSi no caben 6, junta el 1 y el 4."

    # ═══ Piezas · 18 Pérdida ═══
    $s = Nueva
    Encabezado $s 'PIEZAS · DIAPOSITIVA 18' 'Copiar y pegar' -1
    Texto $s 80 160 920 110 'Pérdida de sustentación' 72 $C.text $Titulo -negrita | Out-Null
    Nota $s 270 90 'Agrega este bloque antes de "Para recuperarte". Si no cabe, achica la ilustración del avión.'
    Subtitulo $s 400 'El avión te avisa:' $C.text
    Fila $s 470 150 '' '' 'Una **barra ámbar** parpadea en la visera del tablero, frente a ti' -imagen @('aviso_perdida_encendido.png', 0.3125)
    Fila $s 640 150 '' '' 'El **mando vibra** en la mano del joystick' -imagen @('icono_vibracion.png', 1) -anchoImagen 110
    Fila $s 810 150 '' '' 'Suena un **pitido**' -imagen @('icono_pitido.png', 1) -anchoImagen 110
    Aviso $s 1000 170 'Si parpadea y pita [[más rápido]], ya estás en pérdida. No avisa en tierra ni a muy baja velocidad.'

    # ═══ Piezas · cambios cortos (01, 11, 14, 21 y la barra de 21) ═══
    $s = Nueva
    Encabezado $s 'PIEZAS · 01, 11, 14 Y 21' 'Copiar y pegar' -1
    Texto $s 80 160 920 110 'Cambios cortos' 72 $C.text $Titulo -negrita | Out-Null
    Subtitulo $s 290 'Para la 01 · Índice'
    $idx = @(@('02', 'Controles VR'), @('10', 'Instrumentos'), @('03', 'Subir al avión'), @('15', 'Despegue'),
             @('04', 'Motor y palancas'), @('16', 'Aterrizaje'), @('09', 'Tren de aterrizaje'), @('19', 'Ametralladora'))
    $p = @()
    for ($k = 0; $k -lt 8; $k++) {
        $col = $k % 2; $fil = [Math]::Floor($k / 2)
        $x = 80 + $col * 475; $y = 360 + $fil * 84
        $p += Texto $s $x $y 445 60 "[[$($idx[$k][0])]]  $($idx[$k][1])" 34 $C.muted -anclar 3
        $p += Caja $s $x ($y + 66) 445 2 $C.line $null 1
    }
    Agrupar $s $p 'Índice' | Out-Null
    Subtitulo $s 720 'Para la 11 · Altímetro (texto de arriba)'
    Texto $s 80 780 920 60 'Tu altura **en metros**. Se lee **como un reloj** con tres agujas.' 36 $C.muted | Out-Null
    Subtitulo $s 910 'Para la 14 · Reloj y munición (TIP)'
    Aviso $s 975 160 'Cuando el contador llega a **0000** el arma empieza a recargar sola (ver diapositiva **19**).'
    Subtitulo $s 1175 'Para la 21 · Si te estrellas (fila CHOQUE)'
    Fila $s 1245 180 'CHOQUE' $C.red 'Golpear el suelo o un obstáculo con el cuerpo o las alas, aterrizar muy fuerte o **sin tren**'
    Subtitulo $s 1475 'Para todas · Barra de 21 (pinta de ámbar las primeras N)'
    Barra $s 1555 0
    Texto $s 80 1595 920 60 'Contador: **NN** / 21' 34 $C.muted $Titulo | Out-Null

    $pres.SaveAs($salida, 24)
    New-Item -ItemType Directory -Force $vistas | Out-Null
    for ($k = 1; $k -le $pres.Slides.Count; $k++) {
        $pres.Slides.Item($k).Export((Join-Path $vistas ("vista_{0:D2}.png" -f $k)), 'PNG', 1080, 1920)
    }
    Write-Host "OK $salida ($($pres.Slides.Count) diapositivas)"
}
finally {
    $pres.Close()
    $app.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($app) | Out-Null
}

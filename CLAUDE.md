# Proyecto Tesis — Simulador de vuelo VR

## Lo esencial
- **Motor:** Unity 6 (`6000.0.33f1`).
- **Render pipeline:** URP (Universal Render Pipeline). El paquete de HDRP también está en `Packages/manifest.json`, pero el proyecto usa URP.
- **Plataforma:** PC VR (OpenXR / Oculus, XR Interaction Toolkit 3.x).
- **Escena principal:** `MainScene` → `Assets/VR_FlightSimulato_Proy/Scenes/MainScene.unity`.
- **Terreno:** se pinta con **Procedural Terrain Painter** (`Assets/ProceduralTerrainPainter/`), junto con Unity Terrain Tools.

## Notas
- El código y los assets propios del proyecto viven en `Assets/VR_FlightSimulato_Proy/`.
- Los `TerrainData_*.asset` en la raíz de `Assets/` son generados; no editarlos a mano.

# Operación y diagnóstico

Esta guía cubre las tareas que normalmente aparecen después de conseguir la primera ruta: inspeccionar el grid, medir la cola y mantener actualizado el snapshot cuando cambia el escenario.

## Ciclo de vida del grid

- El grid se construye en `Awake` mediante `Refresh()`.
- En Edit Mode no se reconstruye automáticamente al modificar el Inspector. Usar `Rebuild grid preview` en el inspector del pathfinder.
- `Refresh()` reconstruye el snapshot completo. Debe reservarse para cargar o cambiar por completo un mapa.
- `TryRefreshRegion()` vuelve a muestrear sólo las celdas que intersectan unos bounds de mundo. Es la opción normal para colocar o retirar un edificio.
- `GridVersion` sólo avanza cuando cambia el contenido efectivo del grid. `PathfinderMovement` valida las rutas conservadas y recalcula las que hayan quedado invalidadas.

Los agentes y obstáculos dinámicos no se hornean. Sólo los colliders incluidos en `Static Obstacle Mask` participan en el snapshot.

## Ver el grid con gizmos

1. Seleccionar el GameObject que contiene `PathfindingRectangle` o `PathfindingTilemap`.
2. Activar `Gizmos` en la vista Scene.
3. Mantener `Show Gizmos` activado en el componente.
4. Elegir el nivel de detalle:
   - `Bounds Only`: dibuja únicamente el perímetro; su coste es constante.
   - `Sampled Cells`: inspecciona como máximo `Maximum Gizmo Cell Samples`.
5. Elegir el filtro: todas, bloqueadas, transitables, terreno ponderado o cambios de la última versión.

Colores actuales:

- Verde: transitable.
- Rojo: bloqueada.
- Amarillo: terreno con coste distinto del normal.
- Cian: celda modificada en la última actualización regional.

`Sampled Cells` distribuye la muestra por el grid completo. Si el Inspector indica menos celdas inspeccionadas que celdas totales, la visualización no es exhaustiva. Aumentar el máximo sólo durante el diagnóstico y volver a un valor moderado después.

Los cuadrados pueden mostrar separación visual porque se dibujan reducidos deliberadamente. Esa separación no representa huecos físicos entre las celdas.

## Colocar un obstáculo estático

El collider debe usar una capa incluida sólo en `Static Obstacle Mask`. En un `PathfindingTilemap`, `Sample Static Obstacles` debe estar activado si se espera que esos colliders modifiquen el grid. Después de colocarlo o activarlo, sincronizar Physics2D antes de actualizar la región:

```csharp
using SparkyGames.Pathfinder;
using UnityEngine;

public static class NavigationBuildings
{
    public static bool Place(
        Pathfinding pathfinder,
        Collider2D buildingCollider,
        Vector3 worldPosition)
    {
        buildingCollider.transform.position = worldPosition;
        buildingCollider.enabled = true;
        Physics2D.SyncTransforms();

        return pathfinder.TryRefreshRegion(
            buildingCollider.bounds,
            out GridRegionUpdateResult result);
    }
}
```

Conviene ampliar ligeramente los bounds cuando el collider coincida exactamente con el borde de una celda:

```csharp
Bounds affected = buildingCollider.bounds;
affected.Expand(0.01f);
pathfinder.TryRefreshRegion(affected, out GridRegionUpdateResult result);
```

## Retirar un obstáculo estático

Capturar los bounds antes de desactivar o destruir el collider. `Destroy` se aplica al final del frame, por lo que desactivarlo explícitamente evita volver a muestrearlo como obstáculo:

```csharp
Bounds affected = buildingCollider.bounds;
buildingCollider.enabled = false;
Physics2D.SyncTransforms();

bool refreshed = pathfinder.TryRefreshRegion(
    affected,
    out GridRegionUpdateResult result);

Object.Destroy(buildingCollider.gameObject);
```

Si `TryRefreshRegion` devuelve `false`, consultar `LastGridUpdateError`. En una actualización correcta, `GridRegionUpdateResult` informa de bounds y celdas evaluadas, celdas modificadas y versiones anterior/actual.

Cuando una operación mueve un obstáculo estático, actualizar la unión de sus bounds anteriores y nuevos; de lo contrario pueden quedar celdas bloqueadas en la posición antigua.

## Scheduler y presupuesto por frame

`PathRequestScheduler` ejecuta A* de forma síncrona en el hilo principal, pero reparte las solicitudes entre frames. Sus valores iniciales son:

- Máximo de 4 solicitudes iniciadas por frame.
- Presupuesto blando de 2 ms por frame.
- Aging cada 0,5 segundos.
- Caché exacta de 64 entradas durante 2 frames.

El tiempo se comprueba entre consultas. Una búsqueda A* ya iniciada no puede pausarse y puede superar por sí sola el presupuesto; además, al menos una solicitud puede comenzar para garantizar progreso. Una solicitud sólo puede cancelarse mientras continúa en cola.

Prioridades disponibles, de menor a mayor: `Low`, `Normal`, `High` y `Critical`. Dentro de la misma prioridad se conserva FIFO. El aging eleva solicitudes antiguas para evitar starvation.

La caché sólo reutiliza una consulta con el mismo pathfinder, origen, destino, opciones, radio del agente y versión del grid. No comparte caminos aproximados ni rutas entre distintos orígenes.

## Métricas y Profiler

El scheduler publica:

- `PendingCount` y `LastFrameProcessedCount`.
- `LastFrameElapsedMilliseconds`.
- `AverageExecutionMilliseconds` y `GetExecutionPercentile95Milliseconds()`.
- `MaximumExecutionMilliseconds` y `MaximumQueueWaitMilliseconds`.
- Totales de completadas, canceladas, cache hits, cache misses y prioridades envejecidas.

Cada `PathRequestHandle` conserva `PathRequestMetrics`: espera en cola, ejecución, tiempo exclusivo en A*, overhead, frames, cache hit y aging.

Marcadores relevantes del Unity Profiler:

```text
SparkyGames.Pathfinder.Scheduler.ProcessFrame
SparkyGames.Pathfinder.Scheduler.ProcessRequest
SparkyGames.Pathfinder.Scheduler.CacheLookup
SparkyGames.Pathfinder.Scheduler.FindPath
SparkyGames.Pathfinder.Scheduler.CacheStore
SparkyGames.Pathfinder.RtsLocalSeparation.Evaluate
SparkyGames.Pathfinder.RtsLocalSeparation.NeighborQuery
SparkyGames.Pathfinder.RtsLocalSeparation.Steering
```

Medir en un Development Player después de una pasada de calentamiento. El primer A* de un proceso limpio puede incluir inicialización de runtime y no representa por sí solo el coste estable.

## Diagnóstico rápido

| Síntoma | Comprobaciones |
|---|---|
| No existe grid | Revisar `LastGridBuildError`, referencias, tamaños, límite de celdas y máscaras solapadas. |
| Todo aparece bloqueado | El suelo o su TilemapCollider probablemente está en `Static Obstacle Mask`. |
| Las unidades se convierten en paredes | Su capa está incluida en la máscara estática; debe estar sólo en `Agent Mask`. |
| Un edificio nuevo se atraviesa | Falta `Physics2D.SyncTransforms()` o `TryRefreshRegion()` tras activar su collider. |
| Quedan celdas bloqueadas al destruir | Se capturaron los bounds después de desactivar, o no se refrescó la región anterior. |
| Ruta atraviesa una esquina | Activar `Prevent Corner Cutting`; revisar también el radio del agente. |
| El destino resuelto no coincide | `Find Nearest Reachable Destination` aplicó fallback. Consultar estado y ambos destinos. |
| Muchas rutas esperan | Revisar prioridad, aging, `PendingCount`, espera máxima y presupuesto por frame. |
| Una consulta causa un pico | Reducir grid/área, ajustar `Max Expanded Nodes` sólo con criterio de fallo aceptable y medir calentado. |
| Separación errática | Verificar que `Agent Mask` no incluye otros colliders y que el buffer de vecinos no se satura. |

## Checklist antes de integrar en producción

1. Instalar mediante tag o commit fijo en un proyecto Unity limpio.
2. Confirmar que Runtime, Consumers y Editor compilan sin warnings del paquete.
3. Probar escenas con y sin Tilemap, según el uso real.
4. Validar los radios de todos los tipos de agente.
5. Medir una ráfaga con el número real de unidades simultáneas.
6. Probar construcción, destrucción y movimiento de obstáculos.
7. Probar cancelación, cambio de escena y cierre del Player.
8. Decidir qué fallo verá el jugador cuando no exista destino alcanzable.
9. Guardar una captura de Profiler representativa y los valores usados.
10. Mantener el paquete y el proyecto consumidor como repositorios independientes.

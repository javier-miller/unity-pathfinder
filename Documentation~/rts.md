# Integración RTS

## Responsabilidades

El paquete aporta movimiento individual, asignación de formación, separación y diagnóstico de atasco. El proyecto debe implementar:

- Lectura de ratón o mando.
- Selección individual y mediante rectángulo.
- Equipos, órdenes permitidas y prioridades de gameplay.
- Indicadores visuales de selección y destino.
- Animación, sorting y orientación del sprite.

Esta separación permite cambiar el sistema de selección sin modificar el pathfinder.

## Prefab de infantería

Componentes recomendados:

```text
Unit
  SpriteRenderer / Animator
  Rigidbody2D
  Collider2D
  PathfinderMovement
  RtsUnitMovementController
  RtsLocalSeparation       opcional, recomendado para grupos
  RtsStuckDetector         opcional, recomendado con física dinámica
```

Configuración inicial orientativa para un grid de `0.5` unidades:

| Propiedad | Punto de partida |
|---|---:|
| `PathfinderMovement.Speed` | `3–5` |
| `Waypoint Tolerance` | `0.08–0.15` |
| `Arrival Tolerance` | `0.08–0.15` |
| `Agent Profile Radius` | `0.25–0.4` |
| `Minimum Repath Interval` | `0.5–0.75 s` |
| Collider | ligeramente mayor que el radio de navegación |

Son valores de partida, no reglas universales. El radio del perfil bloquea conservadoramente celdas próximas y debe guardar relación con la huella real de la unidad.

La unidad debe usar una capa incluida únicamente en `Agent Mask`. Si se incluye también en `Static Obstacle Mask`, una reconstrucción puede hornearla como pared.

## Orden individual

```csharp
using SparkyGames.Pathfinder.Consumers;
using UnityEngine;

public sealed class UnitCommandExample : MonoBehaviour
{
    [SerializeField] private RtsUnitMovementController unit;

    public void Move(Vector3 destination)
    {
        unit.IssueMoveOrder(destination);
    }
}
```

`Path Options` permite activar diagonales, prevención de corner cutting, fallback, suavizado, radio y presupuesto de nodos.

## Orden para una selección

Crear un GameObject con `RtsFormationDestinationPlanner`. La lista de seleccionados pertenece al controlador del proyecto:

```csharp
using System.Collections.Generic;
using SparkyGames.Pathfinder.Consumers;
using UnityEngine;

public sealed class GroupCommandExample : MonoBehaviour
{
    [SerializeField] private RtsFormationDestinationPlanner planner;
    private readonly List<RtsUnitMovementController> selected = new();

    public bool MoveSelection(Vector3 target)
    {
        if (selected.Count == 0)
        {
            return false;
        }

        Vector2 center = Vector2.zero;
        foreach (RtsUnitMovementController unit in selected)
        {
            center += (Vector2)unit.transform.position;
        }

        center /= selected.Count;
        Vector2 forward = (Vector2)target - center;
        return planner.IssueMoveOrder(
            selected,
            target,
            forward.sqrMagnitude > 0.001f
                ? forward.normalized
                : Vector2.up);
    }
}
```

El planner genera slots, valida una ruta por unidad y entrega la ruta ya calculada al movimiento. No debe ejecutarse además otra búsqueda manual para el mismo slot.

## Ajustar la formación

En `RtsFormationDestinationPlanner > Settings`:

- `Spacing`: distancia entre slots solicitados.
- `Columns`: `0` elige una distribución casi cuadrada; otro valor fuerza columnas.
- `Maximum Candidate Attempts Per Unit`: candidatos alternativos antes de fallar.
- `Candidate Search Step`: paso de la espiral de alternativas.
- `Minimum Resolved Slot Separation`: evita asignar dos unidades al mismo punto resuelto.
- `Find Nearest Reachable Slot`: permite fallback.
- `Maximum Fallback Distance`: impide que un slot alternativo aparezca demasiado lejos; `0` significa ilimitado.

Escuchar `AssignmentCompleted` y revisar `AssignedCount`, `FailedCount`, `State` y los `CurrentAssignments` para mostrar feedback o diagnosticar una formación parcial.

## Separación local

`RtsLocalSeparation` modifica la velocidad cuando detecta agentes cercanos. Utiliza `Agent Mask` del pathfinder inicialmente y no modifica el grid.

Parámetros principales:

- `Neighbor Radius`: distancia de búsqueda de vecinos.
- `Separation Strength`: magnitud de la repulsión.
- `Maximum Separation Ratio`: porcentaje máximo de velocidad dedicado a separar.
- `Minimum Forward Ratio`: avance mínimo conservado hacia el waypoint.
- `Arrival Fade Distance`: reduce la repulsión al aproximarse al slot.
- `Maximum Neighbor Colliders`: capacidad del buffer físico reutilizado.

Si `Was Neighbor Buffer Full` o `Neighbor Buffer Saturation Count` aumenta, elevar el buffer o reducir densidad/radio y volver a medir.

La separación no sustituye la formación ni garantiza resolver un cuello de botella.

## Detección de atasco

`RtsStuckDetector` observa ventanas sin progreso mientras la unidad debería avanzar. Puede solicitar un replan con cooldown y limita `Maximum Recovery Attempts`.

Escuchar:

- `StuckDetected`: se detectó un episodio y puede haberse solicitado recuperación.
- `RecoveryExhausted`: el gameplay debe decidir si cancela, teletransporta, abre paso o informa al jugador.

Un obstáculo dinámico que no pertenece al snapshot puede producir la misma ruta de nuevo. En ese caso no basta con aumentar intentos: hace falta una política de evitación o coordinación.

## Vehículos

Añadir `RtsVehicleSteering` sólo a vehículos. Se ejecuta después de la separación, limita el giro y reduce velocidad en curvas cerradas.

A* continúa usando un radio circular y no conoce orientación ni radio de giro. Si un vehículo no puede seguir una ruta que la infantería sí recorre, debe medirse antes de ampliar el algoritmo.

## Escala y renderizado 2D

El pathfinder no cambia el sorting ni la animación. Para una vista cenital suele usarse:

```csharp
spriteRenderer.sortingOrder =
    1000 - Mathf.RoundToInt(transform.position.y * 10f);
```

El origen de la unidad debe representar sus pies o centro de apoyo. El cuerpo visual puede extenderse hacia arriba sin aumentar necesariamente el radio de navegación.

## Validación mínima

1. Una unidad llega a un destino libre.
2. Una selección completa recibe slots distintos.
3. El grupo cruza una zona estrecha sin hornear agentes.
4. Un destino bloqueado genera fallback acotado o fallo explicable.
5. Una orden nueva cancela limpiamente la anterior.
6. Cerrar o cambiar la escena no deja excepciones.
7. El Profiler confirma que scheduler y separación caben en el presupuesto del juego.

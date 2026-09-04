# Integración point-and-click

## Jerarquía mínima

```text
Navigation
  PathfindingRectangle o PathfindingTilemap

Pathfinding Manager
  PathRequestScheduler
  PathfindingManager -> Navigation

Player
  SpriteRenderer / Animator
  Rigidbody2D
  Collider2D
  PathfinderMovement
  PointAndClickMovementController
  Script de input del proyecto
```

`PointAndClickMovementController` traduce una coordenada de pantalla o mundo a una orden. No lee el ratón por sí mismo, para que cada juego pueda decidir si el clic pertenece a UI, interacción, diálogo o navegación.

## Configuración recomendada

En `PathfinderMovement`:

- `Speed`: velocidad en unidades de mundo por segundo.
- `Waypoint Tolerance`: puede ser algo más amplia para atravesar puntos intermedios sin oscilación.
- `Arrival Tolerance`: más estricta si el personaje debe colocarse junto a una interacción.
- `Minimum Repath Interval`: evita tormentas de recálculo.

En `PointAndClickMovementController > Path Options`:

- `Allow Diagonal Movement`: normalmente activado.
- `Prevent Corner Cutting`: activado para no atravesar esquinas.
- `Find Nearest Reachable Destination`: activado para corregir clics ligeramente fuera del suelo.
- `Smooth Path`: activado para una trayectoria menos cuadriculada.
- `Agent Profile > Radius`: clearance del personaje. `0` representa un agente puntual.
- `Max Expanded Nodes`: `0` no impone límite; usar otro valor sólo después de medir.

## Input con el Input System

El paquete no depende de un backend de input. Si el proyecto usa el Input System de Unity:

```csharp
using SparkyGames.Pathfinder.Consumers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class AdventureInput : MonoBehaviour
{
    [SerializeField] private PointAndClickMovementController controller;

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame &&
            (EventSystem.current == null ||
             !EventSystem.current.IsPointerOverGameObject()))
        {
            controller.MoveFromScreenPoint(mouse.position.ReadValue());
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            controller.CancelMovement();
        }
    }
}
```

Asignar `World Camera` en el Inspector. Si queda vacío, el controlador intenta usar `Camera.main`.

## Interacciones

Para una puerta, NPC u objeto interactivo, el gameplay debe calcular primero un punto de aproximación y ordenar el movimiento a ese punto:

```csharp
PathfinderMovementNotification terminal =
    await controller.MoveToWorldPointAsync(interactionPoint.position);

if (terminal.State == PathfinderMovementState.Arrived)
{
    interaction.BeginInteraction();
}
```

Debe utilizarse `ResolvedDestination` para conocer dónde acabó realmente el personaje. Con fallback activo puede ser diferente de `RequestedDestination`.

## Animación

`MovementDirection` representa intención hacia el waypoint. Para animar debe preferirse la velocidad física observada:

```csharp
Vector2 velocity = controller.Movement.ActualVelocity;
animator.SetFloat("MoveX", velocity.x);
animator.SetFloat("MoveY", velocity.y);
animator.SetFloat("Speed", velocity.magnitude);
```

`ActualVelocity` vuelve a cero al pausar, cancelar o terminar.

## Eventos importantes

`PathfinderMovement` publica:

- `MovementStarted`.
- `MovementReplanned`.
- `WaypointReached`.
- `MovementArrived`.
- `MovementBlocked`.
- `MovementFailed`.
- `MovementCancelled`.

Suscribirse y desuscribirse simétricamente en `OnEnable` y `OnDisable`. Las notificaciones incluyen operación, estado, causa, destinos, posición, velocidad y versión del grid.

## Errores habituales

- El clic no mueve: comprobar cámara, manager, scheduler y que el destino esté dentro del grid.
- El personaje atraviesa esquinas: activar `Prevent Corner Cutting`.
- Se detiene demasiado lejos: reducir `Arrival Tolerance`.
- No cabe por una puerta: revisar `Agent Profile Radius`, tamaño de celda y anchura real disponible.
- Un clic sobre una pared termina cerca: comportamiento esperado con `Find Nearest Reachable Destination`; desactivarlo para exigir el punto exacto.
- El sprite y la colisión parecen desplazados: situar el origen del agente en los pies y alinear allí el collider de navegación.

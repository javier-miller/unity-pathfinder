# Instalación y configuración base

## 1. Instalar el paquete

### Desde Package Manager

Cuando el repositorio tenga un tag, abrir `Window > Package Management > Package Manager`, seleccionar `Install package from git URL...` e introducir:

```text
https://github.com/javier-miller/unity-pathfinder.git#<tag>
```

Es recomendable fijar un tag o commit. Usar una rama móvil hace que dos instalaciones puedan resolver código diferente.

### Desde `Packages/manifest.json`

```json
{
  "dependencies": {
    "com.sparkygames.pathfinder": "https://github.com/javier-miller/unity-pathfinder.git#<tag>"
  }
}
```

Para desarrollar el propio paquete puede conservarse como paquete embebido en `Packages/com.sparkygames.pathfinder`. No deben coexistir una copia embebida y otra referencia Git con el mismo nombre UPM.

## 2. Crear las capas de navegación

Crear tres capas distintas en `Project Settings > Tags and Layers`:

| Ejemplo | Uso | ¿Se hornea en el grid? |
|---|---|---:|
| `NavigationStatic` | paredes, rocas y edificios estables | Sí |
| `NavigationDynamic` | puertas y obstáculos móviles no agentes | No |
| `Units` | personajes y unidades | No |

Los números de capa no forman parte del paquete; pueden elegirse libremente. Lo obligatorio es que las tres máscaras del pathfinder no se solapen.

No incluir el suelo ni un `TilemapCollider2D` de terreno en `Static Obstacle Mask`: hacerlo bloquearía el propio terreno navegable.

## 3. Elegir una fuente de grid

### Rectángulo

1. Crear un GameObject llamado `Navigation`.
2. Añadir `PathfindingRectangle`.
3. Configurar `Grid Size` en unidades de mundo.
4. Configurar `Tile Size`, por ejemplo `0.5 × 0.5`.
5. Asignar las tres máscaras de capas.
6. Mantener `Maximum Grid Cells` en su valor inicial salvo que exista una razón medida para aumentarlo.

El transform del GameObject representa el centro del rectángulo. El grid se construye en `Awake`. En Edit Mode, el botón `Rebuild grid preview` permite comprobar la configuración sin entrar en Play Mode.

Configuración equivalente por código:

```csharp
using SparkyGames.Pathfinder;
using UnityEngine;

PathfindingRectangle pathfinder = navigationObject
    .AddComponent<PathfindingRectangle>();

pathfinder.ConfigureObstacleMasks(
    staticObstacles,
    dynamicObstacles,
    agents,
    refresh: false);

pathfinder.Configure(
    worldGridSize: new Vector2(40f, 22.5f),
    worldCellSize: new Vector2(0.5f, 0.5f),
    refresh: true);
```

### Tilemap

1. Crear un `Grid` de Unity y un Tilemap de navegación.
2. Añadir `PathfindingTilemap` al mismo GameObject que contiene el `Tilemap`.
3. Usar inicialmente `Tiles Define Navigable Area`: una posición con tile es terreno potencial y un hueco queda bloqueado.
4. Mantener `Trim Empty Border` activado para que `cellBounds` no reserve bordes vacíos.
5. Usar `Explicit Bounds` cuando el mapa necesite límites estables independientes de los tiles pintados.
6. Desactivar `Sample Static Obstacles` si toda la navegación se expresa exclusivamente mediante tiles y reglas.
7. Añadir `Tile Rules` para bloquear tiles concretos o darles un coste mayor.

`Entire Bounds Define Navigable Area` hace transitables también los huecos dentro de los bounds. Sólo debe elegirse deliberadamente.

## 4. Crear el manager y scheduler

Crear un único GameObject llamado `Pathfinding Manager`:

1. Añadir `PathRequestScheduler`.
2. Añadir `PathfindingManager`; Unity exigirá el scheduler automáticamente.
3. Arrastrar el `PathfindingRectangle` o `PathfindingTilemap` de la escena a `Pathfinding Selected`.

Por código:

```csharp
GameObject managerObject = new GameObject("Pathfinding Manager");
managerObject.AddComponent<PathRequestScheduler>();
PathfindingManager manager = managerObject.AddComponent<PathfindingManager>();
manager.SetActivePathfinder(pathfinder);
```

Debe existir un solo `PathfindingManager` activo. Un duplicado se elimina y emite una advertencia.

## 5. Crear un agente básico

El GameObject del agente necesita:

- `Rigidbody2D`.
- Un `Collider2D` apropiado para la física del juego.
- `PathfinderMovement`.
- La capa incluida en `Agent Mask`.

El origen del GameObject o del Rigidbody representa la posición usada por navegación. En personajes 2D suele convenir situarlo en los pies.

Orden mínima:

```csharp
using SparkyGames.Pathfinder;
using UnityEngine;

public sealed class MoveExample : MonoBehaviour
{
    [SerializeField] private PathfinderMovement movement;
    [SerializeField] private Vector3 destination;

    private void Start()
    {
        bool accepted = movement.MoveTo(destination);
        Debug.Log($"Accepted by scheduler: {accepted}");
    }
}
```

`MoveTo` confirma que la orden entró en la cola; no significa que haya llegado. Para esperar el resultado terminal:

```csharp
PathfinderMovementNotification result =
    await movement.MoveToAsync(destination);

Debug.Log($"{result.State}: {result.PathStatus}");
```

## 6. Validar antes de añadir gameplay

1. Seleccionar el pathfinder y comprobar que el Inspector no muestra errores.
2. Pulsar `Rebuild grid preview` o entrar en Play Mode.
3. Confirmar `Has Grid`, dimensiones y número de celdas.
4. Activar gizmos y revisar al menos bounds, celdas caminables y bloqueadas.
5. Ordenar una ruta corta y otra que rodee un obstáculo.
6. Probar destino bloqueado, cancelación y cambio de escena.
7. Revisar la consola; una prueba que visualmente parece correcta no debe dejar excepciones durante el cierre.

Después de este punto, continuar con la guía [point-and-click](point-and-click.md) o [RTS](rts.md).

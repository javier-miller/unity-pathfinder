# Sparky Games Pathfinder

Paquete embebido de navegación 2D basada en grid para Unity. Se utiliza como base compartida para movimiento point-and-click en aventuras gráficas y para un prototipo RTS con varias unidades.

> Estado: primera versión `1.0.0` preparada. P0–P4 y la documentación de integración de P5 están implementados. Los samples de aventura gráfica y RTS se validaron en Development Player sobre Unity 6. Los tests automatizados siguen aplazados; antes de consumir el tag desde producción falta una instalación Git final desde un proyecto limpio.

## Entorno actual

- Nombre UPM: `com.sparkygames.pathfinder`.
- Versión declarada: `1.0.0`.
- Compatibilidad mínima declarada: Unity `6000.0`.
- Proyecto consumidor actual: Unity `6000.5.9f1`.
- Ubicación embebida: `Packages/com.sparkygames.pathfinder`.
- Dependencias directas: `com.unity.modules.physics2d` y `com.unity.modules.tilemap`, ambas `1.0.0`.

La carpeta física coincide ya con el nombre UPM, por lo que Unity puede resolver el paquete embebido sin advertencias de nomenclatura. `unityRelease` se omite deliberadamente: el paquete requiere la línea Unity 6, no un parche o una alfa concreta. La primera API pública se fija en `1.0.0`; el número `1.0.2` heredado no representaba una publicación anterior. La licencia MIT está declarada mediante `LICENSE.md` y `licensesUrl`; `CHANGELOG.md` contiene la entrada fechada de `1.0.0` y dispone de `changelogUrl`. La URL pública de documentación se añadirá cuando se elija su destino definitivo.

## Documentación de integración

La documentación autocontenida para instalar y configurar el paquete desde otro proyecto o continuar con otro chat está en [`Documentation~/index.md`](Documentation~/index.md).

Ruta recomendada:

1. [Instalación y configuración base](Documentation~/getting-started.md).
2. [Aventura point-and-click](Documentation~/point-and-click.md) o [RTS](Documentation~/rts.md).
3. [Operación y diagnóstico](Documentation~/operations.md).
4. [Continuidad para otra sesión](Documentation~/handoff.md).

Las escenas citadas en este README viven actualmente en el proyecto consumidor y no forman parte del repositorio Git del paquete. Las guías anteriores contienen la configuración necesaria sin depender de esas escenas.

## Licencia

El código del paquete se distribuye bajo la [licencia MIT](LICENSE.md), con copyright de Sparky Code Studios. Los assets de los proyectos consumidores y de futuros samples deben conservar su propia licencia y no quedan cubiertos automáticamente por la licencia del paquete.

Los cambios aún no publicados están registrados en [CHANGELOG.md](CHANGELOG.md).

## Organización del código

Los `asmdef` permanecen en las raíces de Runtime, Consumers y Editor e incluyen recursivamente estas carpetas:

```text
Runtime/
  Core/          Grid, A*, resultados y opciones de consulta.
  GridSources/   Construcción rectangular/Tilemap y muestreo.
  Components/    Fachadas MonoBehaviour y manager.
  Movement/      Seguimiento, estados, eventos y modificadores.
  Scheduling/    Cola, prioridades, handles y métricas.

Consumers/Runtime/
  Common/        Configuración compartida por consumidores.
  PointAndClick/ Controlador para aventuras gráficas.
  RTS/           Formación, separación, atasco y vehículos.

Editor/
  Pathfinding/   Inspectores de grids y fuentes.
  Movement/      Inspector de seguimiento de rutas.

Documentation~/  Instalación, recetas por género, operación y handoff.
```

Cada script se trasladó junto a su `.meta`; los GUID de los 49 scripts y sus referencias serializadas se conservaron. Las carpetas nuevas tienen sus propios `.meta` versionados. Se verificaron 69 GUID sin duplicados y Unity regeneró los proyectos C# conservando los tres ensamblados existentes.

## Qué proporciona actualmente

### Runtime

- `Grid`: almacena las celdas en una matriz bidimensional.
- `GridCell`: coordenadas, posición de mundo, transitabilidad, coste y versión de su último cambio.
- `IPathfindingGridSource`: contrato de construcción del grid y conversión de posiciones de mundo a coordenadas.
- `IIncrementalPathfindingGridSource`: capacidad opcional para volver a muestrear únicamente una región de un grid existente.
- `IVersionedPathfinding`: versión pública del snapshot, actualización regional y validación de rutas retenidas.
- `GridRegionUpdateResult`: resumen de celdas evaluadas, celdas modificadas y versiones anterior/nueva.
- `GridCellNavigationUpdate` y `GridNavigationChangeSummary`: lote público para que fuentes externas publiquen walkability y costes con una única versión.
- `IGridCellObstacleSampler`: contrato para decidir si la huella de una celda pertenece a un obstáculo horneable.
- `Physics2DStaticObstacleSampler`: implementación que consulta exclusivamente las capas estáticas mediante `Physics2D`.
- `RectangleGridSource`: geometría rectangular, presupuesto de celdas y muestreo desacoplado de obstáculos estáticos.
- `TilemapGridSource`, `TilemapGridSourceOptions` y `TilemapNavigationRule`: semántica, costes y límites controlados para un `Tilemap`.
- `PathResult` y `PathStatus`: contrato inmutable para resultados detallados de las consultas.
- `PathAgentProfile`: radio de clearance requerido por una consulta.
- `PathQueryOptions`: opciones clonables para configurar perfil, diagonales, corner cutting, destino alternativo, suavizado y presupuesto de nodos.
- `GridPathfinder`: núcleo A* independiente de `MonoBehaviour`, física y datos de escena.
- `PathPriorityQueue`: heap binario interno con una posición única por celda.
- `PathSearchContext`: ownership interno de costes, padres, estados y heap reutilizados entre consultas.
- `Pathfinding`: fachada de Unity que conserva el snapshot construido, traduce las consultas mediante su fuente y delega la búsqueda al núcleo.
- `PathfindingTilemap`: componente de escena que configura un `TilemapGridSource`.
- `PathfindingRectangle`: componente de escena que configura un `RectangleGridSource`.
- `PathRequestScheduler`: planificador central que procesa consultas síncronas mediante prioridad y presupuesto por frame.
- `IPathRequestScheduler`, `PathRequestHandle`, `PathRequestMetrics`, `PathRequestPriority` y `PathRequestStatus`: contratos de encolado, observación, métricas, finalización y cancelación.
- `PathfindingManager`: selecciona el pathfinder activo y proporciona el scheduler central mediante un singleton con acceso seguro.
- `PathfinderMovement`: máquina de estados que encola una ruta, admite pausa y cancelación, conserva destinos solicitado/resuelto y mueve un `Rigidbody2D` entre waypoints.
- `IPathfinderMovementVelocityModifier`: punto de extensión para aplicar steering desacoplado durante `FixedUpdate`.
- `IPathfinderMovementVelocityModifier` y `PathfinderMovementVelocityModifierOrder`: contrato ordenado y etapas convencionales para componer varios modificadores de forma determinista.
- `PathfinderMovementState`: estados `Idle`, `WaitingForPath`, `FollowingPath`, `Paused`, `Arrived`, `Blocked`, `Cancelled` y `Failed`.
- `PathfinderMovementNotification`: snapshot inmutable entregado por los eventos detallados de movimiento.
- `PathRepathReason`: distingue recálculo manual, ruta invalidada y recuperación de atasco.

### Consumers Runtime

- `MovementPathOptions`: representación serializable de las opciones de consulta; crea una instancia independiente por orden.
- `PointAndClickMovementController`: proyecta un punto de pantalla o mundo sobre el plano XY del agente y solicita el movimiento.
- `RtsUnitMovementController`: frontera de órdenes para una unidad RTS individual.
- `RtsFormationDestinationPlanner`: recibe una selección, busca mediante el scheduler un destino diferente y alcanzable para cada unidad y comienza sus rutas sin repetir A*.
- `RtsFormationSettings`, `RtsFormationAssignment` y `RtsFormationOrderNotification`: configuración y snapshots de diagnóstico de una asignación colectiva.
- `RtsLocalSeparation`: steering ligero que aparta agentes próximos sin incorporarlos al grid estático.
- `RtsStuckDetector` y `RtsStuckNotification`: detección por ventanas de progreso y recuperación acotada.
- `RtsVehicleSteering`: perfil opcional de giro limitado; la infantería permanece omnidireccional.

Los consumidores viven en `SparkyGames.Pathfinder.Consumers`, un ensamblado que depende del Runtime común. El núcleo no depende de este ensamblado.

### Editor

- Inspectores para los pathfinders de Tilemap y rectángulo.
- Inspector de depuración para `PathfinderMovement`.
- Gizmos del grid sólo al seleccionar, con contorno barato y muestreo de celdas acotado y filtrable.

### Diagnóstico visual del grid

Los gizmos de `PathfindingRectangle` y `PathfindingTilemap` sólo se procesan mientras el componente está seleccionado. `Bounds Only` es el modo inicial: dibuja el perímetro del grid con cuatro líneas y no inspecciona ninguna celda. Es la opción adecuada para mantener visible la extensión navegable durante el trabajo normal.

`Sampled Cells` habilita el diagnóstico detallado. Inspecciona índices repartidos uniformemente por el grid hasta `Maximum Gizmo Cell Samples` —512 inicialmente, configurable entre 16 y 4096— y permite filtrar por todas las celdas, bloqueadas, transitables, terreno ponderado o cambios de la última versión. Los wireframes son el modo inicial; el relleno translúcido es opcional.

El inspector muestra `Cells inspected / total`. Si la cantidad inspeccionada es inferior al total, el filtro se aplica sólo a esa muestra y puede omitir celdas que sí cumplen la condición. Para una comprobación exhaustiva de un grid pequeño puede aumentarse temporalmente el límite hasta cubrirlo; no conviene mantenerlo alto durante el trabajo habitual. `Latest Changes` representa las celdas cuyo dato de navegación cambió en la versión vigente: tras una reconstrucción completa puede abarcar todo el grid y tras una actualización regional se limita a sus cambios efectivos.

## Flujo de uso actual

1. Crear un GameObject con `PathfindingTilemap` o `PathfindingRectangle`.
2. Configurar `Static Obstacle Mask`, `Dynamic Obstacle Mask` y `Agent Mask` con capas distintas.
3. Crear un `PathfindingManager`, asignar su `pathfindingSelected` y configurar el `PathRequestScheduler` requerido en el mismo GameObject.
4. Añadir `Rigidbody2D` y `PathfinderMovement` a un personaje o unidad.
5. Solicitar el movimiento:

```csharp
PathfinderMovement movement = GetComponent<PathfinderMovement>();

bool accepted = movement.MoveTo(destination);
```

`MoveTo` devuelve `true` cuando la orden ha sido aceptada por el scheduler, no cuando ya existe una ruta. El resultado definitivo se comunica mediante las notificaciones o `MoveToAsync`, que devuelve el snapshot terminal completo. Esta tarea espera la búsqueda encolada y todo el desplazamiento; A* continúa ejecutándose sincrónicamente en el hilo principal cuando el scheduler concede turno a la solicitud.

Una orden nueva termina la anterior con estado `Cancelled`. También puede cancelarse explícitamente:

```csharp
movement.CancelMovement();
```

Desactivar o destruir el componente cancela la operación pendiente, evitando que un `MoveToAsync` quede esperando indefinidamente.

## Planificador de solicitudes

`PathfindingManager` requiere un `PathRequestScheduler` y lo publica mediante `GetScheduler()` y `TryGetScheduler(...)`. `PathfinderMovement` lo utiliza automáticamente para la primera ruta y para los replans provocados por una versión nueva del grid. Mientras espera, su estado es `WaitingForPath`; no conserva una ruta antigua ni intenta mover el rigidbody.

El scheduler dispone de cuatro prioridades: `Low`, `Normal`, `High` y `Critical`. Cada nivel conserva orden FIFO y el aging promueve la prioridad efectiva de una solicitud por cada intervalo de espera —`0.5 s` inicialmente— hasta `Critical`. Con igual prioridad efectiva se elige la petición más antigua. Cada movimiento usa `RequestPriority`, configurable en el Inspector; su valor inicial es `Normal`.

El presupuesto combina:

- `Max Requests Per Frame`: número máximo de búsquedas iniciadas durante un frame; el valor inicial es `4`.
- `Max Milliseconds Per Frame`: límite de tiempo comprobado entre búsquedas; el valor inicial es `2 ms` y `0` desactiva este límite.

Siempre puede comenzar al menos una consulta si la cola no está vacía. Como A* sigue siendo una operación síncrona, una única ruta costosa puede superar el límite de milisegundos y no puede cancelarse a mitad. El presupuesto evita ráfagas de varias búsquedas en el mismo frame, pero no garantiza por sí solo un tiempo máximo duro. Todas las operaciones del scheduler pertenecen al hilo principal de Unity.

También puede usarse directamente:

```csharp
IPathfinding pathfinder = PathfindingManager.GetInstance();
IPathRequestScheduler scheduler = PathfindingManager.GetScheduler();

PathRequestHandle request = scheduler.Enqueue(
    pathfinder,
    start,
    destination,
    options,
    PathRequestPriority.High,
    (handle, result) => Debug.Log(result.Status));

// Sólo tiene efecto mientras Status sea Queued.
request.Cancel();
PathResult result = await request.Completion;
```

`PathRequestHandle` expone `RequestId`, prioridad, estado, resultado terminal, `Completion` y un `PathRequestMetrics` terminal con espera, ejecución, tiempo exclusivo de `FindPath`, overhead del scheduler, frames, cache hit y aging. Cancelar una petición todavía en cola cuesta O(1), produce un `PathResult` con estado `Cancelled` y ejecuta exactamente una vez su finalización. Una consulta marcada `Running` ya no puede interrumpirse. Excepciones del pathfinder se transforman en `InvalidConfiguration`; excepciones de un callback se registran sin detener la cola.

El scheduler publica contadores por frame y acumulados, tiempo medio/máximo por búsqueda, espera máxima, promociones y cache hits. `GetExecutionPercentile95Milliseconds()` calcula P95 sobre las últimas 128 consultas y está pensado para diagnóstico ocasional, porque ordena una copia del buffer al invocarse. Unity Profiler muestra los marcadores `SparkyGames.Pathfinder.Scheduler.ProcessFrame`, `ProcessRequest`, `CacheLookup`, `FindPath` y `CacheStore`.

La caché conserva como máximo 64 resultados durante dos frames por defecto. Sólo reutiliza una consulta exactamente igual —misma instancia de pathfinder, origen, destino, opciones, radio y `GridVersion`—, por lo que el `PathResult` inmutable es seguro. No intenta entregar a una unidad una ruta calculada desde otro origen. Muchas unidades con orígenes diferentes hacia una zona común necesitarían un árbol por objetivo o flow field y se reevaluarán únicamente después de medir.

## Fuentes de construcción del grid

`Grid` continúa siendo únicamente el contenedor de celdas consumido por `GridPathfinder`. Ya no conoce Tilemaps, transforms, colliders ni `Physics2D`. La construcción y la conversión de coordenadas están detrás de `IPathfindingGridSource`:

```csharp
public interface IPathfindingGridSource
{
    bool TryBuildGrid(out Grid grid, out string errorMessage);

    bool TryGetCellCoordinates(
        Vector3 worldPosition,
        out Vector2Int cellCoordinates);
}
```

La fuente debe construir un snapshot completo y conservar la geometría necesaria para convertir posiciones contra ese mismo snapshot. Una coordenada fuera del grid sigue siendo un resultado válido de la conversión: es `GridPathfinder` quien la convierte después en `StartOutsideGrid` o `DestinationOutsideGrid`. `false` queda reservado para una fuente no inicializada o incapaz de realizar la conversión.

Una fuente puede implementar además `IPathfindingGridSourceConfigurationValidator`. Su `TryValidateConfiguration` comprueba referencias, geometría y opciones sin construir el grid, recorrer tiles ni ejecutar consultas físicas. `RectangleGridSource` y `TilemapGridSource` ya lo implementan.

`Pathfinding.TryValidateConfiguration(out errorMessage)` combina esa prevalidación con las máscaras comunes. `Refresh()` llama al mismo método antes de construir, y los inspectores lo usan para mostrar exactamente el diagnóstico que impediría el build. Un conflicto de máscaras identifica los dos campos y enumera las capas compartidas por nombre y número.

Los inspectores distinguen cinco situaciones: configuración inválida, configuración válida todavía sin snapshot, snapshot activo, fallo del último build y fallo de la última actualización regional. Mientras la prevalidación sea inválida deshabilitan la reconstrucción. Los fallos dependientes del contenido completo —como bounds explícitos sin tiles de terreno— sólo se comprueban al reconstruir y quedan disponibles en `LastGridBuildError`; así el Inspector no recorre el Tilemap en cada repintado.

`Pathfinding.Refresh()` descarta el snapshot anterior, crea una fuente nueva y sólo publica otro grid si la construcción termina con un valor válido. Si falla, deja `HasGrid` en `false`, conserva el diagnóstico en `LastGridBuildError` y las consultas devuelven `InvalidConfiguration`. `GridSource` permite inspeccionar la implementación usada en el último intento.

Las dos fuentes actuales son clases C# independientes de la fachada:

- `RectangleGridSource` captura posición, tamaño, tamaño de celda, presupuesto y sampler; calcula centros y coordenadas con la misma geometría.
- `TilemapGridSource` captura los límites usados en la construcción, utiliza las conversiones del Tilemap y delega el muestreo al mismo contrato.

Ambas fuentes usan inicialmente `PathfindingGridLimits.DefaultMaximumCellCount`, actualmente 262.144 celdas, y rechazan el snapshot antes de reservarlo cuando lo supera. `PathfindingRectangle` expone `MaximumGridCells` en el Inspector y mediante este overload para niveles construidos en runtime:

```csharp
rectangle.Configure(
    worldGridSize: new Vector2(100f, 100f),
    worldCellSize: new Vector2(0.5f, 0.5f),
    maximumCellCount: 262144,
    refresh: true);
```

La fuente rectangular calcula primero los ejes y su producto con `double`, antes de convertir a `int` o crear el array. Si se supera el límite, el diagnóstico indica dimensiones, total solicitado y tres correcciones posibles: aumentar `Cell Size`, reducir `Grid Size` o elevar conscientemente `Maximum Grid Cells`.

`PathfindingRectangle` y `PathfindingTilemap` validan su configuración y crean la fuente correspondiente. `Pathfinding.CreateGridSource` es abstracto: cualquier fachada nueva debe proporcionar explícitamente un `IPathfindingGridSource`; no existen hooks alternativos que puedan producir geometría y coordenadas incoherentes.

### Clasificación de obstáculos

La escena separa tres responsabilidades mediante máscaras de capas de `Physics2D`:

- `StaticObstacleMask`: paredes, rocas y edificios colocados. Es geometría estable entre actualizaciones publicadas y la única máscara que se hornea.
- `DynamicObstacleMask`: puertas animadas, objetos desplazables u otros bloqueos que se mueven con frecuencia. No se hornea en el snapshot actual.
- `AgentMask`: personajes y unidades. Nunca se hornea; una reconstrucción del grid no convierte sus posiciones actuales en celdas bloqueadas permanentes.

Las tres máscaras deben ser disjuntas. Si una capa aparece en más de una categoría, `Refresh()` rechaza la configuración, deja `HasGrid` en `false` y publica el motivo en `LastGridBuildError`. Los inspectores muestran además el conflicto sin esperar a una consulta de ruta. Las capas que no pertenecen a ninguna máscara se ignoran para navegación.

Una configuración recomendada del proyecto es crear capas dedicadas, por ejemplo `NavigationStatic`, `NavigationDynamic` y `Units`. La capa del Tilemap que representa el suelo debe quedar fuera de `StaticObstacleMask` cuando tenga un `TilemapCollider2D`, o el propio terreno bloqueará todas las celdas.

`RectangleGridSource` y `TilemapGridSource` reciben un `IGridCellObstacleSampler`. Los componentes incluidos crean un `Physics2DStaticObstacleSampler` a partir de `StaticObstacleMask`; integraciones externas pueden inyectar datos explícitos del nivel en vez de consultas físicas por celda.

`RuntimeObstacleMask` combina las capas dinámicas y de agentes. `RtsLocalSeparation` reutiliza `AgentMask` para apartar unidades y `RtsStuckDetector` observa el progreso físico. Los obstáculos dinámicos que no son agentes siguen sin evitación predictiva; un replan puede producir una ruta equivalente si el bloqueo no se publica en el grid.

### Actualizaciones regionales y versionado

`Pathfinding.TryRefreshRegion(Bounds, out GridRegionUpdateResult)` vuelve a evaluar sólo las celdas cuyo volumen de muestreo puede intersectar los límites de mundo indicados. `RectangleGridSource` y `TilemapGridSource` implementan esta capacidad; fuentes externas pueden incorporarla mediante `IIncrementalPathfindingGridSource`.

La fachada expone esta capacidad mediante `IVersionedPathfinding`. `PathfinderMovement` usa `GridVersion` e `IsPathWalkable` para decidir si conserva una ruta después de un cambio, mientras construcción o gameplay pueden invocar `TryRefreshRegion`. El término «versioned» describe el snapshot mutable y evita sugerir que una búsqueda A* pueda pausarse entre frames.

```csharp
if (pathfinder is IVersionedPathfinding versioned)
{
    Debug.Log(versioned.GridVersion);
}
```

`IIncrementalPathfindingGridSource` conserva su nombre porque en ese nivel «incremental» sí describe con precisión el remuestreo parcial de una fuente de grid. No implica que A* pueda pausarse entre frames.

El resultado informa de:

- `EvaluatedCellBounds` y `EvaluatedCellCount`: región conservadora revisada.
- `ChangedCellCount`: celdas cuyo dato de navegación realmente cambió.
- `ChangedWalkabilityCellCount` y `ChangedTraversalCostCellCount`: desglose del cambio.
- `PreviousVersion` y `CurrentVersion`: revisión antes y después de la operación.

La versión sólo avanza una vez cuando cambia al menos una celda. Una petición válida fuera del grid o que produce exactamente el mismo estado devuelve éxito sin incrementar la versión. Una reconstrucción completa mediante `Refresh()` siempre publica una versión nueva y marca el comienzo de otro snapshot geométrico. `GridCell.LastNavigationDataChangedVersion` permite diagnosticar en qué revisión cambió una celda concreta y `PathResult.GridVersion` identifica el snapshot usado por cada consulta.

Al crear un edificio estático, el sistema de construcción debe activar primero sus colliders, sincronizar la física si necesita el resultado en el mismo frame y publicar sus límites:

```csharp
Collider2D buildingCollider = building.GetComponent<Collider2D>();
Physics2D.SyncTransforms();

if (!pathfinding.TryRefreshRegion(buildingCollider.bounds, out var update))
{
    Debug.LogError(pathfinding.LastGridUpdateError);
}
```

Para retirarlo hay que conservar primero sus límites, desactivar o destruir efectivamente sus colliders y actualizar después esa región:

```csharp
Bounds removedBounds = buildingCollider.bounds;
buildingCollider.enabled = false;
Physics2D.SyncTransforms();
pathfinding.TryRefreshRegion(removedBounds, out var update);
```

`Physics2D.SyncTransforms()` no se llama dentro del paquete porque su coste y el batching pertenecen al sistema de construcción. Si la actualización se hace después del siguiente paso de física puede omitirse. Cuando un edificio tiene varios colliders, se debe enviar el `Bounds` que englobe todos ellos.

Una actualización regional no cambia la geometría ni amplía los límites del grid. En un Tilemap puede actualizar tiles dentro de `EffectiveBounds`, pero añadir terreno fuera de esos límites exige `Refresh()` completo. Para mapas con construcción resulta preferible `ExplicitBounds`, porque sus dimensiones permanecen estables.

`PathfinderMovement` conserva la versión usada por su ruta. Cuando detecta otra versión:

- Si todos los tramos pendientes siguen siendo transitables, adopta la versión nueva sin solicitar otro A*.
- Si un tramo quedó bloqueado o el snapshot fue reconstruido, recalcula desde la posición actual hacia `RequestedDestination`.
- Si el resultado anterior era `SuccessNearestReachable`, vuelve a consultar para aprovechar un destino original que quizá haya quedado libre.
- El recálculo conserva `OperationId`, destino y tarea asíncrona. Si falla, finaliza normalmente como `Blocked` o `Failed`.

`MovementReplanned`, `PathGridVersion`, `RepathCount` y `LastRepathReason` permiten observar este proceso. El replan entra en `WaitingForPath` y comparte cola, prioridad y presupuesto con las órdenes iniciales. `MinimumRepathInterval` —`0.5 s` inicialmente— limita tanto cambios del grid como solicitudes manuales y recuperación de atasco. Si una ruta queda inválida durante el cooldown, la unidad se detiene y espera; nunca continúa por el tramo obsoleto.

### Semántica del Tilemap

La decisión predeterminada es utilizar un Tilemap dedicado como superficie de navegación:

- Cualquier posición que contenga un tile representa terreno potencialmente transitable.
- Una posición vacía dentro del rectángulo se conserva como celda bloqueada. El grid continúa siendo rectangular, pero A* no puede atravesar el hueco.
- `StaticObstacleMask` se aplica después: un collider incluido en esa máscara bloquea incluso una celda con terreno.
- La capa del propio Tilemap de terreno no debe incluirse en `StaticObstacleMask` si tiene `TilemapCollider2D`, porque bloquearía todas sus celdas.
- `TilemapNavigationRule` puede sobrescribir transitabilidad y coste para assets de tile concretos.

`TilemapCellSemantics.TilesDefineNavigableArea` implementa este comportamiento y es el valor inicial. `EntireBoundsDefineNavigableArea` conserva el comportamiento histórico, donde cada posición del bounds es terreno aunque no tenga tile. Este segundo modo sólo debe usarse cuando el rectángulo completo sea deliberadamente navegable.

Este valor inicial cambia deliberadamente el resultado de escenas antiguas que utilizaban celdas vacías como suelo. Al migrarlas hay que asignar `EntireBoundsDefineNavigableArea` para conservar su comportamiento, o pintar explícitamente el Tilemap de navegación antes de adoptar la semántica nueva.

Los límites se controlan mediante `TilemapBoundsMode`:

- `TilemapCellBounds`: parte de `Tilemap.cellBounds`. Con `TrimEmptyBorder` activo —valor inicial— busca el primer y último tile por eje y recorta bordes vacíos sin llamar a `CompressBounds` ni modificar el Tilemap.
- `ExplicitBounds`: utiliza un `BoundsInt` configurado por el nivel. Es la opción indicada cuando sólo una región del Tilemap pertenece a navegación.

Ambos modos representan exactamente una capa Z. Una configuración con más capas se rechaza para no proyectar silenciosamente varias celdas sobre la misma coordenada 2D. `MaximumGridCells` vale `262144` inicialmente y se comprueba antes de recorrer o reservar el grid. Si los límites originales superan ese presupuesto, la construcción falla con un mensaje que recomienda límites explícitos, retirar tiles lejanos o comprimir el Tilemap.

El recorte elimina bordes vacíos, pero dos tiles extremadamente alejados siguen definiendo un rectángulo grande con huecos bloqueados entre ellos. El límite máximo impide el peor caso; mapas con regiones separadas deben usar varios Tilemaps/pathfinders o límites explícitos.

El Inspector muestra la semántica activa, errores de construcción, bounds efectivo, reglas, uso de física y los contadores `CandidateCellCount`, `OccupiedTileCount` y `WalkableCellCount`. También muestra el desglose de la última actualización regional.

### Costes de terreno y perfiles de agente

`GridCell.TraversalCost` es un multiplicador entero positivo. El valor normal es `1`; A* multiplica por él el coste de entrar en la celda —`10` en ortogonal y `14` en diagonal— y mantiene una heurística admisible porque nunca se aceptan valores menores que uno. `PathResult.TotalCost` publica el coste acumulado de la ruta encontrada antes del suavizado.

`TilemapGridSourceOptions.DefaultTraversalCost` define el valor general. La lista `TileRules` relaciona cada `TileBase` con `IsWalkable` y `TraversalCost`; si un asset aparece varias veces, prevalece su última regla. El núcleo A* sólo recibe números y booleanos, por lo que no depende de Tilemaps ni assets.

Los cambios regionales de coste avanzan la misma versión que la transitabilidad. Como una reducción de coste fuera de la ruta actual puede abrir una alternativa mejor, las rutas retenidas anteriores a ese cambio se recalculan. El suavizado se omite automáticamente mientras exista terreno con coste distinto de uno: una línea recta geométrica no debe atravesar terreno caro ignorando la decisión de A*.

`PathAgentProfile.Radius` expresa en unidades de mundo el espacio requerido por la consulta. Radio `0` conserva el agente puntual histórico. Para radios mayores, `Grid.HasClearance` comprueba un rectángulo conservador de celdas completas alrededor de cada candidato; los límites exteriores cuentan como bloqueados. La misma regla se aplica a origen, destino, vecinos, diagonales, corner cutting, suavizado y validación de rutas versionadas.

El perfil se clona dentro de `PathQueryOptions` y también está disponible en `MovementPathOptions` para aventuras gráficas y RTS. El clearance conservador puede rechazar un paso diagonal donde un círculo exacto todavía cabría, pero evita atravesar obstáculos sin introducir geometría o física dentro del A*.

### Muestreo físico opcional

Una máscara estática vacía ya no crea un `Physics2DStaticObstacleSampler`, por lo que las fuentes no hacen consultas físicas inútiles. En Tilemap también puede desactivarse `SampleStaticObstacles`: tiles, huecos y `TileRules` pasan a ser la única fuente de navegación. Es la configuración más barata para niveles completamente explícitos.

Si existen edificios colocables en `StaticObstacleMask`, el muestreo debe permanecer activo y sus altas/bajas se publican mediante actualizaciones regionales. `RectangleGridSource` admite igualmente un sampler `null` o uno personalizado para que futuras fuentes de nivel eviten `Physics2D`.

## Consumidores separados

Ambos controladores son adaptadores pequeños sobre `PathfinderMovement`. Conservan las notificaciones y la máquina de estados del componente común, en lugar de duplicarlas.

### Point-and-click

Añadir `PointAndClickMovementController` al mismo GameObject que `PathfinderMovement`. Puede recibir una coordenada de pantalla:

```csharp
using SparkyGames.Pathfinder.Consumers;
using UnityEngine;

public PointAndClickMovementController pointAndClick;

public void OnPointerPressed(Vector2 screenPosition)
{
    pointAndClick.MoveFromScreenPoint(screenPosition);
}
```

También puede recibir directamente un punto de mundo mediante `MoveToWorldPoint`, útil cuando antes del movimiento hay que resolver una interacción o un punto de aproximación.

El controlador no lee teclado, ratón o touch por sí mismo. La capa de entrada decide qué pulsación es válida, descarta clics sobre UI o diálogos y entrega la coordenada. Esto evita acoplar el paquete al Input System nuevo, al sistema antiguo o a reglas concretas de interacción. Usa la cámara asignada o `Camera.main` y proyecta el rayo sobre el plano XY situado en la Z actual del agente.

### Unidad RTS

Añadir `RtsUnitMovementController` a cada unidad y hacer que el sistema de órdenes externo llame a:

```csharp
using SparkyGames.Pathfinder.Consumers;
using UnityEngine;

public void MoveUnit(RtsUnitMovementController unit, Vector3 assignedDestination)
{
    unit.IssueMoveOrder(assignedDestination);
}
```

Una orden nueva sustituye la anterior conforme al contrato de `PathfinderMovement`. `CancelCurrentOrder`, `PauseCurrentOrder` y `ResumeCurrentOrder` son nombres propios del dominio RTS sobre las mismas operaciones comunes. El sistema de selección debe decidir qué unidades reciben la orden y el sistema de formación debe calcular un destino distinto para cada una; ninguno de esos conceptos entra en el controlador individual.

### Formación de una selección RTS

Añadir `RtsFormationDestinationPlanner` al objeto que coordina las órdenes del jugador. El sistema de selección continúa fuera del paquete y entrega su lista actual:

```csharp
using System.Collections.Generic;
using SparkyGames.Pathfinder.Consumers;
using UnityEngine;

public RtsFormationDestinationPlanner formationPlanner;

public void MoveSelection(
    IReadOnlyList<RtsUnitMovementController> selection,
    Vector3 destination,
    Vector2 facing)
{
    formationPlanner.IssueMoveOrder(selection, destination, facing);
}
```

El planner genera una cuadrícula compacta alrededor del destino. `facing` orienta las filas; un vector cero usa `Vector2.up`. Las unidades se emparejan de forma greedy con slots cercanos para reducir cruces iniciales. Esta asignación es determinista, pero no pretende ser un óptimo global como el algoritmo húngaro.

Cada unidad se mantiene quieta mientras su candidato se consulta mediante `PathRequestScheduler`. La consulta usa sus propias `MovementPathOptions`, `PathAgentProfile` y `RequestPriority`. Cuando encuentra una ruta, reserva `ResolvedDestination` y entrega ese mismo `PathResult` a `PathfinderMovement.FollowPrecomputedPath`, por lo que validar el slot no duplica el cálculo A*.

`RtsFormationSettings` permite configurar:

- `Spacing` y `Columns`; cero columnas produce una formación casi cuadrada.
- `Maximum Candidate Attempts Per Unit` y `Candidate Search Step` para la búsqueda en espiral alrededor del slot ideal.
- `Minimum Resolved Slot Separation`, que impide reservar destinos iguales o demasiado próximos.
- `Find Nearest Reachable Slot`, que permite aceptar el punto alcanzable más cercano cuando el candidato exacto no sirve.
- `Maximum Fallback Distance`, distancia máxima en mundo entre el candidato pedido y un resultado `SuccessNearestReachable`. El valor inicial es `2`; `0` desactiva el límite para conservar el comportamiento ilimitado anterior.

`CurrentAssignments` muestra para cada unidad el slot ideal, el último candidato, el destino aceptado, los intentos y el último `PathStatus`. También conserva `LastCandidateResolvedDestination`, `LastCandidateFallbackDistance` y `LastRejectionReason`, incluso cuando el último fallback se rechazó. Los motivos distinguen ruta no disponible, fallback demasiado lejano, destino ya reservado, orden individual posterior y rechazo del movimiento. Al terminar se publica `AssignmentCompleted` con estado `Assigned`, `PartiallyAssigned` o `Failed`. `AssignmentCancelled` informa de una sustitución o cancelación; `CancelCurrentOrder()` cancela por defecto tanto búsquedas pendientes como movimientos ya emitidos.

Una orden individual iniciada después de la orden colectiva tiene precedencia. El planner compara el `OperationId` de cada movimiento y descarta cualquier callback de formación antiguo antes de que pueda sobrescribirla.

La reserva sólo garantiza destinos finales distintos. Para reducir solapamientos durante el trayecto hay que añadir también `RtsLocalSeparation`; aun así, dos rutas pueden cruzarse porque la separación es una influencia local y no un planificador espaciotemporal.

### Separación local RTS

Añadir `RtsLocalSeparation` a cada unidad que deba apartarse de otras. El componente requiere `RtsUnitMovementController`, se registra como `IPathfinderMovementVelocityModifier` y sólo se evalúa mientras `PathfinderMovement` sigue una ruta.

Los colliders de las unidades deben pertenecer a `AgentMask`. Con `Use Pathfinder Agent Mask` activo —valor inicial— el componente toma esa máscara del pathfinder configurado; también puede utilizar una máscara propia. Los agentes continúan excluidos de cualquier reconstrucción del grid.

En cada `FixedUpdate` se ejecuta una consulta `Physics2D.OverlapCircle` sin listas temporales. Los colliders del mismo `Rigidbody2D` se agrupan para no contar dos veces una unidad y se ignoran los colliders propios. La repulsión crece al reducirse la distancia, se limita mediante `Maximum Separation Ratio` y se mezcla con la velocidad que apunta al waypoint. `Minimum Forward Ratio` evita que la separación invierta indefinidamente el movimiento.

Parámetros principales:

- `Neighbor Radius`: radio donde otro agente comienza a influir.
- `Separation Strength`: intensidad de la repulsión antes de limitarla.
- `Maximum Separation Ratio`: fracción máxima de la velocidad base dedicada a separación.
- `Minimum Forward Ratio`: avance mínimo conservado hacia el waypoint.
- `Arrival Fade Distance`: distancia desde la que la influencia empieza a desaparecer hasta ser cero dentro de `ArrivalTolerance`.
- `Responsiveness`: rapidez con la que cambia el vector suavizado.
- `Maximum Neighbor Colliders`: capacidad del buffer reutilizado, entre `1` y `128`.

`NeighborCount`, `SeparationVelocity`, `WasNeighborBufferFull` y `NeighborBufferCapacity` permiten diagnosticar el frame actual. Si el buffer se satura hay que ampliar su capacidad o reducir radio/densidad. Al seleccionar la unidad, los gizmos muestran el radio y el vector de separación.

El Unity Profiler expone siempre `SparkyGames.Pathfinder.RtsLocalSeparation.Evaluate`, `.NeighborQuery` y `.Steering`. Para un diagnóstico agregado, `SetTimingDiagnosticsEnabled(true)` activa contadores con `Stopwatch`: muestras, tiempo total/medio/máximo, tiempo de consulta física, máximo de vecinos y número de saturaciones. Esta medición es opt-in para no añadir cronometraje al gameplay normal; `ResetTimingDiagnostics()` permite comenzar una ventana nueva.

En dos Development Players calentados, las ocho unidades del vertical slice completaron `8/8` destinos y generaron cero avisos. La media total fue `0,0095–0,0097 ms` por unidad y evaluación activa, repartida casi por igual entre consulta física (`0,0048–0,0049 ms`) y resto (`0,0047–0,0048 ms`). Se observaron cuatro vecinos como máximo sobre capacidad 24 y cero saturaciones. Al desactivar sólo `RtsLocalSeparation`, ambas pasadas terminaron `7/8`, con una unidad bloqueada y un aviso: para esta densidad la separación no sólo es barata, también evita el fallo reproducido por la física local.

El motor limita la velocidad final a la velocidad configurada en `PathfinderMovement`. Si ningún modificador altera el vector, conserva exactamente el comportamiento anterior con `Vector2.MoveTowards`; aventuras gráficas y unidades sin `RtsLocalSeparation` no cambian.

Esta implementación reduce amontonamientos, pero no garantiza ausencia de colisiones, no anticipa trayectorias y no evita puertas u obstáculos dinámicos. Un grupo todavía puede bloquearse en un paso estrecho; `RtsStuckDetector` aporta una señal y recuperación acotada, no una garantía de resolución.

### Detección de atasco y cooldown

`RtsStuckDetector` requiere `RtsUnitMovementController` y `Rigidbody2D`. Mientras `PathfinderMovement` está en `FollowingPath`, tiene dirección deseada y aún no está dentro de `ArrivalTolerance`, compara el desplazamiento real durante ventanas temporales. Varias ventanas consecutivas por debajo de `Minimum Progress Distance` publican `StuckDetected`.

Si `Automatically Request Repath` está activo, llama a `PathfinderMovement.RequestRepath(PathRepathReason.StuckRecovery)`. El movimiento conserva su `OperationId`, destino y tarea asíncrona, pero respeta `MinimumRepathInterval`. `Maximum Recovery Attempts` limita los intentos por operación y `RecoveryExhausted` se publica si la unidad vuelve a quedar atascada después de consumirlos.

El detector no transforma el estado en `Blocked`: ese estado sigue reservado para un fallo real de navegación. Esto permite que gameplay decida si empujar unidades, abrir una puerta, cancelar una orden o mostrar feedback cuando `RecoveryExhausted` ocurra. Si el bloqueo dinámico no forma parte del grid, A* puede devolver la misma ruta; en ese caso hace falta una política de evitación o coordinación superior.

### Perfil opcional de vehículo

`RtsVehicleSteering` implementa el mismo contrato de modificador de velocidad que la separación. Limita los grados de giro por segundo, reduce la velocidad durante giros cerrados y opcionalmente orienta el `Rigidbody2D` usando su eje local derecho o superior. Una unidad de infantería no añade este componente y conserva movimiento omnidireccional.

El componente sólo modifica cómo se sigue una ruta. El grid continúa usando un radio circular conservador y A* no incorpora orientación ni radio de giro en el estado; si un vehículo no puede seguir esquinas que la infantería sí supera, esa restricción debe medirse antes de ampliar el buscador.

### Orden de los modificadores de velocidad

Los modificadores implementan `IPathfinderMovementVelocityModifier` y declaran obligatoriamente `VelocityModifierOrder`. Se componen en orden ascendente y los empates conservan el orden de registro.

El paquete reserva etapas convencionales en `PathfinderMovementVelocityModifierOrder`: `LocalAvoidance = 100` y `LocomotionConstraint = 200`. Por ello `RtsLocalSeparation` altera primero la dirección solicitada y `RtsVehicleSteering` aplica después el límite de giro a esa dirección combinada. El resultado ya no depende del orden de `OnEnable`. Modificadores externos pueden utilizar valores intermedios; su propiedad debe permanecer estable mientras estén registrados o deben desregistrarse y registrarse de nuevo.

`PathfinderMovement.GetVelocityModifierExecutionIndex` permite inspeccionar el pipeline durante configuración o diagnóstico. El vertical slice fuerza el registro inverso del vehículo y confirma `separation=100@0`, `vehicle=200@1` y `modifierOrder=PASS` en Development Player.

`MovementPathOptions` aparece en el Inspector de ambos consumidores. El fallback al punto alcanzable más cercano está activado en sus valores iniciales y cada unidad puede configurar su `AgentProfile`. El suavizado continúa desactivado por defecto para preservar escenas existentes. Cada orden obtiene opciones y perfil nuevos, por lo que cambios posteriores del Inspector no alteran una consulta ya iniciada.

## Máquina de estados de movimiento

`PathfinderMovement.State` es la fuente de verdad de la operación actual o de la última operación terminada:

- `Idle`: no se ha iniciado todavía una orden nueva.
- `WaitingForPath`: la ruta inicial o un replan está en la cola central; el cuerpo no se mueve.
- `FollowingPath`: el `Rigidbody2D` está avanzando.
- `Paused`: conserva ruta y operación, pero no mueve el cuerpo.
- `Arrived`: alcanzó el destino resuelto.
- `Blocked`: la consulta terminó por origen/destino bloqueado o ruta inaccesible.
- `Cancelled`: una orden en espera, activa o pausada se canceló explícitamente, al desactivar el componente o al ser sustituida.
- `Failed`: configuración inválida, destino fuera del grid, límite de búsqueda u otro fallo no clasificable como bloqueo.

Los estados terminales se conservan hasta la siguiente orden para facilitar diagnóstico. `MoveToAsync` devuelve un `PathfinderMovementNotification` terminal, de modo que llegada, bloqueo, fallo y cancelación no se colapsan en un booleano.

Pausa y reanudación son explícitas:

```csharp
movement.PauseMovement();
movement.ResumeMovement();
```

No existe un control booleano ambiguo: gameplay debe elegir explícitamente entre pausar y reanudar.

### Destino solicitado y destino resuelto

`RequestedDestination` conserva el punto pedido por gameplay. `ResolvedDestination` conserva el punto elegido por navegación y sólo debe consumirse cuando `HasResolvedDestination` sea `true`.

Para permitir fallback a la celda alcanzable más cercana:

```csharp
PathQueryOptions options = PathQueryOptions.Default;
options.FindNearestReachableDestination = true;

PathfinderMovementNotification terminal =
    await movement.MoveToAsync(destination, options);

Debug.Log($"State: {terminal.State}");
Debug.Log($"Requested: {terminal.RequestedDestination}");
Debug.Log($"Resolved: {terminal.ResolvedDestination}");
```

En `SuccessNearestReachable`, el agente termina en `ResolvedDestination` y pasa a `Arrived`, aunque ese punto sea diferente del solicitado. `MoveToAsync(position, options)` ofrece el mismo comportamiento y entrega el estado terminal detallado. El cálculo del camino se encola por frame, pero cada búsqueda concedida continúa siendo síncrona.

### Notificaciones detalladas

`IPathfinderMovement` y `PathfinderMovement` exponen siete eventos C#:

- `MovementStarted`: se ha aceptado una ruta y comienza su seguimiento. También se emite antes de una llegada inmediata con `AlreadyAtDestination`.
- `MovementReplanned`: una versión nueva invalidó la ruta retenida y se encontró otra dentro de la misma operación.
- `WaypointReached`: se ha alcanzado un waypoint; incluye índice, waypoint y número total.
- `MovementArrived`: se alcanzó el destino resuelto.
- `MovementBlocked`: navegación devolvió origen/destino bloqueado o ruta inaccesible.
- `MovementFailed`: configuración inválida u otro fallo no clasificable como bloqueo.
- `MovementCancelled`: una operación en espera, activa o pausada fue cancelada.

Todas reciben un `PathfinderMovementNotification` con:

- `OperationId`, para correlacionar notificaciones aunque un manejador lance otra orden.
- Estado y `PathStatus` capturados en el momento del evento.
- Destino solicitado, destino resuelto y `HasResolvedDestination`.
- Posición, velocidad real observada, nodos expandidos y coste de ruta.
- Para waypoints, `HasWaypoint`, `WaypointIndex`, `WaypointCount` y `Waypoint`.

Ejemplo:

```csharp
private PathfinderMovement movement;

private void OnEnable()
{
    movement.MovementStarted += OnMovementStarted;
    movement.MovementReplanned += OnMovementReplanned;
    movement.WaypointReached += OnWaypointReached;
    movement.MovementArrived += OnMovementArrived;
    movement.MovementBlocked += OnMovementBlocked;
    movement.MovementFailed += OnMovementFailed;
    movement.MovementCancelled += OnMovementCancelled;
}

private void OnDisable()
{
    movement.MovementStarted -= OnMovementStarted;
    movement.MovementReplanned -= OnMovementReplanned;
    movement.WaypointReached -= OnWaypointReached;
    movement.MovementArrived -= OnMovementArrived;
    movement.MovementBlocked -= OnMovementBlocked;
    movement.MovementFailed -= OnMovementFailed;
    movement.MovementCancelled -= OnMovementCancelled;
}

private void OnWaypointReached(PathfinderMovementNotification notification)
{
    Debug.Log($"Waypoint {notification.WaypointIndex + 1}/{notification.WaypointCount}");
}

private void OnMovementReplanned(PathfinderMovementNotification notification)
{
    Debug.Log($"Replan #{notification.RepathCount}, grid {notification.GridVersion}");
}
```

Las notificaciones incluyen también `GridVersion`, `RepathCount` y `PathCost`. Tras un recálculo, los índices de waypoint vuelven a comenzar en cero porque describen el plan vigente dentro de la misma operación.

Una terminación publica primero el evento detallado y después completa `MoveToAsync` con el mismo snapshot inmutable. El resultado no cambia si un manejador inicia otra orden. Una excepción de un manejador se registra con `Debug.LogException` para no impedir la limpieza de la operación ni la finalización de la tarea.

### Velocidad para animación

`MovementDirection` continúa representando la dirección deseada hacia el siguiente waypoint. No demuestra que el cuerpo se haya desplazado y, por tanto, no debe usarse como velocidad real.

`ActualVelocity` se calcula a partir del desplazamiento observado de `Rigidbody2D.position` entre pasos de física y se expresa en unidades de mundo por segundo. `ActualSpeed` es su magnitud. Al pausar, cancelar, comenzar una orden o completar una operación se reinicia la muestra para evitar que una animación conserve velocidad obsoleta.

Ejemplo de integración con Animator:

```csharp
Vector2 velocity = movement.ActualVelocity;
animator.SetFloat("MoveX", velocity.x);
animator.SetFloat("MoveY", velocity.y);
animator.SetFloat("Speed", movement.ActualSpeed);
```

El muestreo refleja desplazamiento físico, incluidas fuerzas o teletransportes externos observados entre dos `FixedUpdate`. Puede tener un paso de física de latencia respecto al último `MovePosition`, algo esperado al medir desplazamiento real en vez de publicar la velocidad configurada.

### Tolerancias de waypoint y llegada

Se decidió separarlas porque representan compromisos diferentes:

- `waypointTolerance` se usa sólo para puntos intermedios. Puede ser relativamente amplia para no frenar ni oscilar en cada celda del camino.
- `arrivalTolerance` se usa para el último punto y para `AlreadyAtDestination`. Puede ser más estricta para puntos de interacción de una aventura gráfica o slots de formación de un RTS.

Ambas valen `0.1` por defecto, preservando el comportamiento anterior. No se obliga a que una sea menor que la otra porque algunos juegos pueden aceptar una zona final más amplia. Se validan como valores positivos y también pueden ajustarse en runtime:

```csharp
movement.SetWaypointTolerance(0.1f);
movement.SetArrivalTolerance(0.03f);
```

El último waypoint devuelto por navegación sólo evita añadir `ResolvedDestination` cuando ya está dentro de `arrivalTolerance`; de esta forma una tolerancia intermedia amplia no puede hacer que la unidad dé por alcanzado prematuramente el destino final.

## Mejoras P0 completadas

- Las referencias a `UnityEditor` del ensamblado Runtime se han eliminado o protegido con `UNITY_EDITOR`.
- Solicitar el pathfinder sin un manager configurado devuelve `null` de manera segura.
- El singleton se limpia al destruirse y un duplicado elimina únicamente su propio componente.
- La lista interna de waypoints se inicializa siempre.
- `MoveToAsync` se completa una sola vez con un snapshot terminal y no ejecuta `Task.Yield` continuamente.
- El movimiento usa estados explícitos y conserva el estado terminal para diagnóstico.
- El destino solicitado y el resuelto por navegación se almacenan por separado.
- Inicio, waypoint, llegada, bloqueo, fallo y cancelación publican snapshots detallados.
- Se exponen velocidad vectorial y rapidez medidas desde el desplazamiento físico para alimentar animaciones.
- El movimiento utiliza `Rigidbody2D.position` y un paso limitado mediante `MoveTowards`.
- Las tolerancias de waypoint intermedio y llegada final son independientes, configurables y positivas.
- El Inspector ya no modifica directamente por reflexión el estado activo del movimiento.

- `PathfindingRectangle` sanea tamaños no finitos, cero o negativos, garantiza al menos una celda completa por eje y rechaza el snapshot si supera su presupuesto de celdas antes de reservar memoria.
- Las consultas físicas del rectángulo usan un tamaño proporcional a la celda y funcionan con celdas menores de `0.1` unidades.
- Se ha eliminado `SetWalkableArea`, que no tenía consumidores y duplicaba el cálculo de transitabilidad de las fuentes del grid.

Verificación realizada: compilación de los ensamblados Runtime, Consumers Runtime y Editor con 0 errores y 0 advertencias. Todavía no se ha generado un Player build completo.

## API detallada de búsqueda

`PathResult` es el único resultado de búsqueda de caminos. Puede representar éxito, destino alternativo, origen o destino fuera del grid, celdas bloqueadas, ruta imposible, cancelación y límite de búsqueda.

`PathQueryOptions` define actualmente:

- Movimiento diagonal.
- Prevención de corner cutting.
- Búsqueda del destino alcanzable más cercano.
- Suavizado opcional mediante línea de visión sobre el grid.
- Perfil de agente con radio de clearance.
- Límite opcional de nodos expandidos; `0` significa sin límite.

```csharp
PathQueryOptions options = PathQueryOptions.Default;
options.AllowDiagonalMovement = true;
options.PreventCornerCutting = true;
options.FindNearestReachableDestination = false;
options.SmoothPath = false;
options.AgentProfile = new PathAgentProfile(radius: 0.4f);
options.MaxExpandedNodes = 0;
```

La API ya está conectada a `IPathfinding`:

```csharp
PathResult result = pathfinder.FindPath(start, destination, options);

if (result.Succeeded)
{
    IReadOnlyList<Vector3> waypoints = result.Waypoints;
    Vector3 actualDestination = result.ResolvedDestination;
    int accumulatedCost = result.TotalCost;
}
```

## Ownership y asignaciones de la búsqueda

Cada instancia de `GridPathfinder` conserva un único `PathSearchContext`. Los arrays de costes, padres, estados, generaciones y heap sólo crecen cuando la instancia recibe un grid mayor que cualquiera de los anteriores. Las celdas se reinicializan de forma perezosa mediante un número de generación, por lo que tampoco se limpia el grid completo al comenzar cada consulta.

El contexto pertenece en exclusiva a una búsqueda. Si varios hilos llaman simultáneamente a la misma instancia de `GridPathfinder`, las consultas se serializan mediante un lock. Esto evita corrupción de buffers, pero no convierte `Grid` en thread-safe ni sustituye el futuro planificador de solicitudes por frames.

Los buffers nunca se exponen en `PathResult`. Una ruta correcta crea un array del tamaño exacto de la ruta y transfiere su ownership al resultado, que lo publica mediante una colección de sólo lectura. Esa reserva final es necesaria porque el resultado puede sobrevivir y utilizarse después de que empiece la siguiente búsqueda. Los fallos y `AlreadyAtDestination` reutilizan una colección vacía compartida.

## Suavizado de rutas

`PathQueryOptions.SmoothPath` elimina puntos redundantes después de que A* encuentre una ruta. Está desactivado por defecto para no cambiar consumidores existentes. Sus comprobaciones utilizan el radio de `AgentProfile`; si el grid contiene costes distintos de uno, se conserva la ruta original para no ignorar el terreno ponderado.

El suavizado trabaja exclusivamente con datos del grid:

1. Reconstruye la ruta original en un buffer interno reutilizable.
2. Desde cada punto conservado busca el waypoint original más lejano con línea de visión.
3. Una travesía supercover inspecciona cada celda tocada por el segmento.
4. Si el segmento cruza exactamente una esquina y `PreventCornerCutting` está activo, exige que las dos celdas ortogonales sean transitables.
5. Con `AllowDiagonalMovement` desactivado sólo combina puntos de la misma fila o columna.

```csharp
PathQueryOptions options = PathQueryOptions.Default;
options.SmoothPath = true;
options.PreventCornerCutting = true;

PathResult result = pathfinder.FindPath(start, destination, options);
```

El algoritmo no consulta `Physics2D` y no crea otro array temporal por resultado: compacta los índices seleccionados dentro del buffer de reconstrucción. El único array nuevo continúa siendo la colección final inmutable de waypoints.

La seguridad usa el clearance conservador del perfil. No representa todavía la forma exacta de un collider, orientación de vehículos ni radios distintos por eje.

## Validación manual

El proyecto consumidor incluye `Assets/Scenes/PathfinderManualValidation.unity`. Para usarla:

1. Abrir la escena y entrar en Play Mode.
2. Comprobar que el panel muestra seis resultados `PASS`.
3. Activar `Gizmos` en Game View para ver las celdas y rutas.
4. Revisar la consola: cada escenario escribe su estado real.

La escena construye los grids en memoria y cubre:

- Ruta ortogonal recta con diagonales desactivadas.
- Ruta diagonal.
- Inicio encerrado por una esquina bloqueada, que debe devolver `Unreachable` cuando se impide corner cutting.
- Destino transitable pero rodeado: la consulta estricta devuelve `Unreachable` y la consulta con fallback devuelve `SuccessNearestReachable`.
- Comparación de una ruta original y otra suavizada alrededor de una pared; la suavizada debe contener menos puntos sin atravesar la pared ni sus esquinas.
- Comparación de una diagonal suavizada estricta y otra permisiva; la estricta debe conservar el desvío necesario ante una esquina bloqueada.

No utiliza `Physics2D`, Tilemaps, `PathfindingManager` ni `PathfinderMovement`, por lo que aísla la comprobación del núcleo. Tampoco es un test automatizado: esa fase continúa aplazada.

El proyecto incluye además `Assets/Scenes/RtsVerticalSlice.unity`. Al entrar en Play Mode construye ocho unidades, dos muros que forman un cuello de botella, un pathfinder rectangular, scheduler y formación. Arrastrar con botón izquierdo cambia la selección y el botón derecho emite una orden colectiva; los botones permiten cruzar el cuello, volver y encolar 64 consultas de profiling. El panel presenta métricas por frame, media, P95, máximo, espera, aging, caché y avisos de atasco. La unidad naranja utiliza `RtsVehicleSteering`.

El menú `Tools > Sparky Games > Run RTS Vertical Slice Profile` ejecuta un diagnóstico repetible, respeta escenas abiertas con cambios sin guardar y escribe `Logs/RtsVerticalSliceProfile.log`. El runner mide primero el burst aislado y después emite la orden de formación. Dos pasadas en Unity 6000.5.9f1 dieron media `0,3113–0,3243 ms`, P95 `0,6180–0,6710 ms` y máximo `4,5360–5,8086 ms` para las 64 consultas. El P95 no justifica todavía reescribir A*, pero el pico de arranque debe medirse en un Development Player calentado.

La medición posterior ejecuta por proceso 64 consultas frías y tres ráfagas calientes de 64, esperando a que expire la caché exacta. En tres pasadas, el máximo total bajó de `3,56–3,69 ms` en frío a `0,517–1,425 ms` en caliente; el máximo exclusivo de `FindPath` bajó de `2,39–2,45 ms` a `0,509–0,667 ms`. La primera consulta realmente ejecutada produjo el pico frío. Para el alcance actual se conserva A* síncrono; se reevaluará con mapas mayores o máximos calientes por encima del presupuesto.

La reproducción en Player normal demostró que el antiguo cuello era incompatible con el clearance configurado: sólo quedaban dos filas útiles donde los perfiles requerían tres. Tras ampliar el hueco, la formación terminó `Assigned` con ocho llegadas y cero avisos. `RtsStuckDetector` deja de repetir el evento una vez agotada la recuperación; en el escenario estrecho de control publica tres replans y un único agotamiento. El límite de fallback se validó con una orden fuera del mapa: rechazó a `15,25` unidades con límite `1,25` y diagnóstico `FallbackTooFar`, sin afectar a la formación válida posterior.

La escena emplea las capas `NavigationStatic` (8), `Units` (9) y reserva `NavigationDynamic` (10). Es una comprobación manual de integración, no un test automatizado. Tras cada cambio de física o scheduling debe recorrerse visualmente en el hardware objetivo.

## Limitaciones conocidas

### Corrección pendiente

- El clearance es rectangular y conservador; no modela la geometría exacta del agente.
- El steering de vehículo limita el giro al seguir la ruta, pero A* no modela orientación, huella anisotrópica ni radio de giro.

### Ciclo de vida de Unity

- `OnValidate` sólo sanea valores y no reconstruye el grid. La previsualización de Edit Mode se actualiza explícitamente desde el botón del Inspector.
- `Awake` construye el snapshot al entrar en Play Mode; cambiar configuración durante Play requiere usar el mismo botón o llamar a `Refresh()`.
- Aunque el acceso al manager ya es seguro, todavía no existe una inicialización explícita entre la construcción del grid y sus primeros consumidores.
- Mover un rectángulo o Tilemap después de construirlo requiere `Refresh`; las fuentes convierten posiciones contra la geometría del último snapshot.

### Rendimiento

- La primera consulta —o una consulta con un grid mayor— amplía los buffers internos; las siguientes reutilizan esa capacidad.
- Cada ruta correcta reserva un array exacto de waypoints y un wrapper de sólo lectura para que `PathResult` sea independiente de los buffers reutilizables.
- El suavizado básico es greedy sobre la ruta reconstruida; su coste puede crecer en rutas largas y debe medirse antes de habilitarlo masivamente en RTS.
- `PathQueryOptions` se clona por consulta para aislar cambios del llamador.
- Con muestreo físico activo, una construcción completa ejecuta una consulta por celda navegable; una actualización regional consulta sólo su rectángulo conservador.
- El clearance de radio mayor que media celda inspecciona celdas vecinas durante A* y debe medirse con perfiles grandes.
- El scheduler reparte solicitudes simultáneas, pero su presupuesto temporal sigue siendo blando porque una búsqueda individual no se interrumpe.
- La caché exacta tiene pocos hits cuando los orígenes difieren, aunque el objetivo sea la misma zona.
- La separación ejecuta una consulta física por unidad en movimiento y paso de física. Con las ocho unidades actuales cuesta `0,0095–0,0097 ms` por evaluación y no satura el buffer; debe repetirse el perfil si cambia la cantidad o densidad real de agentes.
- El modo `Sampled Cells` de los gizmos es deliberadamente no exhaustivo cuando el grid supera su presupuesto; puede omitir una celda relevante y no sustituye una herramienta de consulta o validación completa.

### Funcionalidad

- Los agentes se excluyen del horneado y disponen de separación suave opcional; los demás obstáculos dinámicos todavía no tienen respuesta de movimiento.
- Las actualizaciones parciales conservan la geometría actual; cambios de bounds requieren un `Refresh()` completo.
- No hay perfiles anisotrópicos ni categorías de terreno por tipo de unidad; el giro de vehículo no participa en A*.
- La falta de progreso se detecta y puede solicitar replans con cooldown, pero un obstáculo dinámico ausente del grid puede producir repetidamente la misma ruta.
- El scheduler limita ráfagas y evita starvation mediante aging, pero una búsqueda individual puede exceder su presupuesto blando.
- Hay slots de formación, separación local y recuperación acotada, pero no evitación predictiva, reservas temporales ni resolución garantizada de cuellos de botella.
- `Blocked` representa un fallo devuelto por navegación; un atasco durante el recorrido se comunica por `RtsStuckDetector` sin falsear ese estado.

## Arquitectura objetivo

El paquete evolucionará hacia módulos con responsabilidades separadas:

```text
Grid sources
  Tilemap / Rectangle / static obstacle sampler
                    ↓
Navigation grid data
  Walkability / costs / clearance / version
                    ↓
Pathfinder core
  Pure C# A* / query options / path result
                    ↓
Path request scheduler
  Queue / priorities / cancellation / frame budget
                    ↓
Movement consumers
  Point-and-click agent / RTS unit agent
                    ↓
Optional group behavior
  Stuck recovery / shared paths / flow fields
```

El núcleo de búsqueda no debe depender de `MonoBehaviour`, `Physics2D`, Tilemaps, singletons ni clases de Editor. Las integraciones de Unity traducirán la escena a datos consumibles por el núcleo.

## Casos de uso previstos

### Aventura gráfica

- Un personaje o pocos agentes.
- Click-to-move.
- Rutas suavizadas.
- Posición alcanzable más cercana al clic.
- Puntos de aproximación para interactuar con objetos.
- Eventos de llegada, fallo y cancelación.

### RTS sencillo

- Decenas de unidades en el primer vertical slice.
- Destinos de formación en lugar de un único punto compartido.
- Obstáculos estáticos y edificios actualizables.
- Peticiones repartidas entre frames.
- Separación local, detección de bloqueo y recálculo.
- Reutilización de rutas cuando sea apropiado.

El objetivo inicial no es resolver una simulación de cientos o miles de agentes. Ese escenario requerirá profiling y podría justificar Jobs/Burst, flow fields o una solución externa.

## Sample mínimo de aventura gráfica

El proyecto consumidor incluye `Assets/Scenes/GraphicAdventureSample.unity`. La escena conecta `PathfindingRectangle`, `PathRequestScheduler`, `PathfindingManager`, `PathfinderMovement` y `PointAndClickMovementController` sobre un fondo y un personaje pixel art.

El clic izquierdo solicita una ruta suavizada con destino alcanzable más cercano; clic derecho o `Escape` cancela la orden y `R` reinicia el personaje. Un bloqueo estático invisible limita el suelo navegable y permite observar la diferencia entre destino solicitado y destino resuelto. El panel consume las notificaciones terminales detalladas.

Los sprites están configurados a 16 PPU, filtro Point, sin mipmaps ni compresión. La cámara URP usa `PixelPerfectCamera` a 640 × 360. La escena puede regenerarse desde `Tools > Sparky Games > Build Graphic Adventure Sample Scene`; la configuración y la validación están documentadas en `Docs/GRAPHIC_ADVENTURE_SAMPLE.md` del proyecto.

Por ahora este ejemplo pertenece al proyecto, no a la distribución UPM. Antes de crear `Samples~` deben confirmarse las licencias de sus assets y separar el arte específico del juego.

## Sample RTS con 15 soldados

`Assets/Scenes/CommandAndConquerSample.unity` utiliza un mapa pixel art de 1280 × 720 y quince soldados de 32 × 32 a 16 PPU. La cámara pixel perfect muestra 640 × 360 y puede desplazarse por el mapa con `WASD` o las flechas.

La escena mantiene input y presentación fuera del paquete: clic o arrastre seleccionan, botón derecho entrega la selección al `RtsFormationDestinationPlanner` y `Escape` cancela. Cada soldado combina `RtsUnitMovementController`, separación local y detección de atasco. Los anillos de selección, el marcador de orden y el sorting por Y pertenecen al controlador del proyecto consumidor.

El Development Player asignó y llevó a destino `15/15` unidades sin errores runtime. La configuración completa y los controles están en `Docs/COMMAND_AND_CONQUER_SAMPLE.md` del proyecto.

## Principios de implementación

- Una celda debe tener un único estado de búsqueda por consulta.
- El resultado debe indicar por qué una ruta ha fallado.
- Los límites y parámetros inválidos deben rechazarse explícitamente.
- El movimiento y la búsqueda de caminos deben poder sustituirse de forma independiente.
- Los obstáculos móviles no deben hornearse como bloqueos permanentes del grid.
- Las optimizaciones avanzadas sólo se introducirán después de medir.
- La primera API publicada no incluye adaptadores para versiones que nunca llegaron a producción.

## Continuación del trabajo

La lista priorizada y los criterios de finalización están en [ROADMAP.md](ROADMAP.md). Debe actualizarse al terminar cada tarea o cuando cambie una decisión arquitectónica.

Los tests automatizados están aplazados por decisión del proyecto y no forman parte de la fase actual. P0–P4 están implementados y P5 incluye ya una guía autocontenida de integración y continuidad. La estructura, el manifiesto y la API heredada del paquete se sanearon para establecer una primera línea pública sin deuda de compatibilidad. El paquete conserva su repositorio Git independiente y declara Unity `6000.0`. Los samples de aventura gráfica y RTS compilaron en Development Player y terminaron con `result=PASS`; el ejemplo RTS asignó y movió `15/15` unidades. La siguiente tarea recomendada es preparar la estrategia de distribución mediante `Samples~`. `ROADMAP.md` conserva además los criterios que pueden reabrir A* incremental, reservas o flow fields.

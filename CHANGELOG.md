# Changelog

Todos los cambios relevantes de Sparky Games Pathfinder se documentarán en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto utiliza [versionado semántico](https://semver.org/lang/es/).

## [Unreleased]

## [1.0.0] - 2026-09-04

### Added

- Resultado inmutable `PathResult`, estados de terminación detallados y opciones explícitas mediante `PathQueryOptions`.
- A* sobre grid con un único estado de búsqueda por celda, cola de prioridad binaria y buffers internos reutilizados.
- Movimiento diagonal configurable, prevención de corner cutting, destino alcanzable más cercano y límite de nodos expandidos.
- Suavizado de caminos por línea de visión que respeta obstáculos, clearance y esquinas bloqueadas.
- `PathAgentProfile` con radio conservador por consulta y costes de terreno ponderados.
- Fuentes independientes `RectangleGridSource` y `TilemapGridSource` detrás de `IPathfindingGridSource`.
- Semánticas seguras para Tilemap, bounds explícitos, recorte de bordes vacíos y reglas por asset de tile.
- Límites configurables de celdas para grids rectangulares y Tilemap, validados antes de reservar memoria.
- Clasificación separada de obstáculos estáticos, obstáculos dinámicos y agentes mediante máscaras no solapadas.
- Actualizaciones regionales del grid, versionado de snapshots y validación de rutas retenidas mediante `IVersionedPathfinding`.
- `PathRequestScheduler` con prioridades, FIFO, cancelación en cola, aging, presupuesto blando por frame, métricas y caché de consultas exactas.
- Máquina de estados explícita en `PathfinderMovement`, destino solicitado y resuelto, tolerancias separadas, velocidad observada y cooldown de recálculo.
- Notificaciones para inicio, replan, waypoint, llegada, bloqueo, fallo y cancelación; APIs asíncronas con resultado terminal detallado.
- Consumidor `PointAndClickMovementController` independiente del sistema de input.
- Consumidor `RtsUnitMovementController` y planificación de destinos mediante `RtsFormationDestinationPlanner`.
- Separación local sin colecciones por frame, detección de atasco y steering opcional para vehículos.
- Prioridad determinista para modificadores de velocidad.
- Gizmos acotados por selección, nivel de detalle, filtros y máximo configurable de celdas inspeccionadas.
- Validaciones compartidas entre Runtime e inspectores, con mensajes concretos para configuraciones inválidas.
- Manuales de instalación, point-and-click, RTS, operación, diagnóstico y continuidad en `Documentation~`.
- Licencia MIT con copyright de Sparky Code Studios.

### Changed

- Compatibilidad mínima declarada actualizada a Unity `6000.0`.
- Carpeta física normalizada a `Packages/com.sparkygames.pathfinder`.
- Scripts reorganizados por responsabilidad en `Runtime`, `Consumers` y `Editor`, conservando GUID, namespaces y ensamblados.
- El grid se construye en `Awake`; la previsualización de Edit Mode se solicita explícitamente desde el Inspector.
- Las unidades y obstáculos dinámicos dejan de hornearse como bloqueos permanentes del snapshot.
- El seguimiento de rutas recalcula sólo cuando una versión nueva invalida el tramo pendiente.
- La asignación de formación reutiliza las rutas calculadas durante la validación de slots.
- Los destinos alternativos de formación pueden limitarse por distancia respecto al slot solicitado.
- La separación local se aplica antes que el steering de vehículos con independencia del orden de componentes.
- Los samples y el input específico permanecen en el proyecto consumidor; el paquete expone consumidores reutilizables sin apropiarse del gameplay.

### Fixed

- Acceso seguro al singleton y scheduler durante inicialización, desactivación y cambio de escena.
- Cancelación y sustitución de operaciones de movimiento sin completar dos veces sus callbacks o tareas.
- Cruce incorrecto de esquinas durante A*, suavizado y validación de rutas versionadas.
- Regiones vacías accidentales producidas por `Tilemap.cellBounds` inflados.
- Reservas excesivas de memoria ante tamaños, bounds o límites de celda inválidos.
- Asignaciones parciales de formación causadas por aceptar destinos alternativos demasiado alejados.
- Referencias Unity destruidas usadas por `RtsFormationDestinationPlanner` durante el cierre de escena.
- Coste innecesario de gizmos que recorrían grids completos en cada repintado.

### Removed

- Dependencias de `UnityEditor` desde el ensamblado Runtime.
- API legacy anterior a producción y todos los miembros marcados `[Obsolete]`.
- Callback booleano de movimiento, sustituido por notificaciones y resultados terminales detallados.
- Nombre ambiguo `IIncrementalPathfinding`; las actualizaciones regionales y el versionado se expresan mediante `IVersionedPathfinding`.
- Reconstrucciones automáticas del grid desde `OnValidate` y Edit Mode.

[Unreleased]: https://github.com/javier-miller/unity-pathfinder/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/javier-miller/unity-pathfinder/releases/tag/v1.0.0

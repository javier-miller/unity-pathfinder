# Manual de Sparky Games Pathfinder

Sparky Games Pathfinder es un paquete de navegación 2D sobre grid para Unity 6. Sirve como núcleo común para personajes point-and-click y grupos pequeños de unidades RTS.

## Compatibilidad y ensamblados

- Nombre UPM: `com.sparkygames.pathfinder`.
- Unity mínimo declarado: `6000.0`.
- Runtime: `SparkyGames.Pathfinder`.
- Consumidores opcionales: `SparkyGames.Pathfinder.Consumers`.
- Herramientas de Editor: `SparkyGames.Pathfinder.Editor`.
- Dependencias directas: módulos integrados `Physics2D` y `Tilemap`.

El paquete no recoge input, no crea UI, no decide qué unidades están seleccionadas y no contiene reglas específicas de gameplay. Esas responsabilidades pertenecen al proyecto consumidor.

## Ruta de lectura

1. [Instalación y configuración base](getting-started.md): instalar desde Git, crear capas, grid, manager y primer agente.
2. [Aventura point-and-click](point-and-click.md): personaje, input, animación, interacción y destino alternativo.
3. [RTS](rts.md): unidades, selección, formación, separación, atasco y vehículos.
4. [Operación y diagnóstico](operations.md): gizmos, scheduler, edificios, actualización regional y resolución de problemas.
5. [Continuidad para otra sesión](handoff.md): estado arquitectónico, checklist y prompt reutilizable para otro chat o proyecto.

`README.md` describe con más profundidad los contratos y decisiones internas. `ROADMAP.md` registra lo implementado, las mediciones y los criterios para reabrir optimizaciones avanzadas.

## Elección rápida

| Necesidad | Componentes principales |
|---|---|
| Área rectangular libre | `PathfindingRectangle` |
| Terreno pintado con huecos o costes | `PathfindingTilemap` |
| Acceso global y reparto de consultas | `PathfindingManager` + `PathRequestScheduler` |
| Seguir una ruta con física 2D | `Rigidbody2D` + `PathfinderMovement` |
| Personaje point-and-click | `PointAndClickMovementController` |
| Unidad RTS | `RtsUnitMovementController` |
| Formación multiunidad | `RtsFormationDestinationPlanner` |
| Separación local | `RtsLocalSeparation` |
| Recuperación ante falta de progreso | `RtsStuckDetector` |
| Vehículo con giro limitado | `RtsVehicleSteering` |

## Estado de la primera línea pública

La API heredada y los adaptadores obsoletos anteriores a producción se retiraron. El paquete compila y se ha ejercitado en Development Player con escenas de aventura gráfica y RTS. Los tests automatizados continúan aplazados por decisión del proyecto.

El paquete se distribuye bajo [licencia MIT](../LICENSE.md) y registra los cambios en [CHANGELOG.md](../CHANGELOG.md). La primera versión pública es `1.0.0`, con tag `v1.0.0`. Antes de consumirla desde producción debe realizarse una comprobación final de instalación Git en un proyecto limpio.

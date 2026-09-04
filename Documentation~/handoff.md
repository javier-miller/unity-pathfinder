# Continuidad para otro chat o proyecto

Este documento es el punto de entrada para continuar el trabajo sin depender del historial de la conversación que creó el paquete.

## Fuente de verdad

- `Documentation~/index.md`: índice de integración.
- `Documentation~/getting-started.md`: instalación y escena mínima.
- `Documentation~/point-and-click.md`: aventura gráfica.
- `Documentation~/rts.md`: RTS.
- `Documentation~/operations.md`: diagnóstico, profiling y cambios del escenario.
- `CHANGELOG.md`: cambios públicos acumulados y contenido de la próxima versión.
- `README.md`: contratos, arquitectura y detalles técnicos.
- `ROADMAP.md`: tareas realizadas, pendientes y criterios de reevaluación.
- Código Runtime: comportamiento real cuando documentación y código discrepen.

Leer primero este archivo y la guía del caso de uso. Antes de editar, revisar `git status` dentro de la raíz del paquete porque es un repositorio Git independiente.

## Decisiones que no deben redescubrirse

- Unity mínimo declarado: `6000.0`.
- El núcleo es A* sobre grid, con un único estado por celda y cola de prioridad binaria.
- `PathfindingRectangle` y `PathfindingTilemap` son fachadas Unity respaldadas por fuentes de grid separadas.
- Sólo los obstáculos estáticos se hornean. Obstáculos dinámicos y agentes tienen máscaras diferentes.
- Los cambios estáticos locales usan actualización regional y versionado del snapshot.
- `PathRequestScheduler` centraliza cola, prioridad, cancelación, aging, caché exacta y presupuesto blando.
- Cada A* sigue siendo síncrono y no pausable; no existe una implementación “incremental A*”.
- `PathfinderMovement` es una máquina de estados y separa destino solicitado de destino resuelto.
- Point-and-click y RTS son consumidores separados. El paquete no posee input, selección, UI ni reglas de gameplay.
- La formación valida y reutiliza la ruta de cada slot; la separación local y el steering son modificadores de velocidad ordenados.
- No se conservan API obsoletas ni adaptadores de compatibilidad anteriores a la primera publicación.
- Tests automatizados, Jobs/Burst, flow fields, ECS/DOTS y multithreading propio están fuera de la fase actual.

No introducir estas técnicas avanzadas sin una medición que satisfaga los criterios de `ROADMAP.md`.

## Información que debe aportar un proyecto nuevo

Antes de pedir a otro chat que configure una escena, indicar:

1. Versión exacta de Unity y pipeline de render.
2. Tipo de juego: point-and-click, RTS o ambos.
3. Fuente del grid: rectángulo o Tilemap.
4. Tamaño del mundo, unidad de escala y tamaño de celda deseado.
5. Capas existentes y qué objetos son estáticos, dinámicos o agentes.
6. Prefab del agente: `Rigidbody2D`, collider, pivote y radio físico.
7. Número normal y máximo de agentes simultáneos.
8. Sistema de input usado y controles esperados.
9. Necesidad de diagonales, suavizado, fallback y terrenos con costes.
10. Comportamiento esperado si un destino o una formación no es alcanzable.

Si faltan valores, un tamaño de celda cercano al diámetro del pie del personaje suele ser un punto de partida, pero debe validarse contra puertas y pasillos reales.

## Checklist de implementación en un proyecto nuevo

1. Instalar el paquete mediante tag o commit fijo.
2. Crear capas disjuntas para obstáculos estáticos, dinámicos y agentes.
3. Crear y validar una única fuente de grid.
4. Crear un único `PathfindingManager` con `PathRequestScheduler`.
5. Preparar un prefab de agente mínimo y situar su origen en los pies o centro de apoyo.
6. Añadir el consumidor point-and-click o RTS, no ambos al mismo agente salvo una razón explícita.
7. Implementar input, selección, UI y animación en el proyecto consumidor.
8. Probar ruta libre, obstáculo, esquina, destino bloqueado, cancelación y cambio de escena.
9. En RTS, probar formación, separación, cuello de botella y saturación con la cantidad real de unidades.
10. Documentar los valores finales y cualquier desviación de estas decisiones.

## Prompt reutilizable para otro chat

Copiar y adaptar este texto:

```text
Este proyecto Unity usa el paquete UPM com.sparkygames.pathfinder.
Antes de modificar nada, lee completos:
- Packages/com.sparkygames.pathfinder/Documentation~/handoff.md
- Packages/com.sparkygames.pathfinder/Documentation~/getting-started.md
- la guía point-and-click.md o rts.md según corresponda
- Packages/com.sparkygames.pathfinder/ROADMAP.md

Inspecciona también el código público que vayas a usar y el git status del
repositorio independiente Packages/com.sparkygames.pathfinder. Conserva los
GUID y no mezcles cambios del paquete con gameplay específico del proyecto.
No restaures API legacy ni añadas tests o tecnologías marcadas fuera de fase
salvo que te lo pida expresamente. Implementa y valida una escena para:

[describir aquí escena, fuente del grid, capas, agentes, input y resultado]

Al terminar, actualiza la documentación y ROADMAP.md si cambia una decisión
del paquete. Informa por separado de cambios en el paquete y en el consumidor.
```

## Escenas de referencia del proyecto actual

Estas escenas ayudaron a validar el paquete, pero pertenecen al proyecto consumidor y no se incluyen automáticamente al instalarlo desde Git:

| Escena | Qué demuestra |
|---|---|
| `Assets/Scenes/GraphicAdventureSample.unity` | Point-and-click pixel perfect, fallback y cancelación. |
| `Assets/Scenes/CommandAndConquerSample.unity` | Selección y formación de 15 soldados. |
| `Assets/Scenes/RtsVerticalSlice.unity` | Obstáculos, cuello de botella y profiling RTS. |
| `Assets/Scenes/PathfinderManualValidation.unity` | Casos básicos del núcleo y suavizado. |

La documentación específica de esas escenas está en `Docs/` del proyecto actual. Un proyecto nuevo debe poder reconstruir su configuración únicamente con las guías de `Documentation~`.

## Pendientes antes de una publicación pública

- Verificar instalación Git desde un proyecto Unity limpio.
- Publicar el commit y el tag `v1.0.0` después de esa comprobación.
- Decidir qué ejemplos y assets redistribuibles se publicarán mediante `Samples~`.
- Revisar los comentarios XML y nombres públicos restantes.

La licencia del paquete ya está fijada como MIT, con `Copyright (c) 2026 Sparky Code Studios`. La versión inicial es `1.0.0`; el changelog conserva esa entrada fechada y deja `Unreleased` para cambios posteriores. No asumir que la licencia cubre automáticamente los assets del proyecto consumidor o de futuros samples.

Hasta completar esos puntos, consumir un commit concreto es más seguro que depender de `main`.

## Criterio de tarea terminada

Una integración no termina sólo porque una unidad se mueva. Debe quedar:

- Escena guardada y referencias serializadas válidas.
- Grid visible y coherente con los colliders.
- Rutas normales y fallos previsibles comprobados.
- Consola limpia durante ejecución y cierre.
- Presupuesto medido con la carga real prevista.
- Configuración y controles documentados en el proyecto consumidor.

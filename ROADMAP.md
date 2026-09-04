# Pathfinder Roadmap

Hoja de ruta priorizada para convertir el paquete actual en una base reutilizable para aventuras gráficas y un prototipo RTS 2D.

Las tareas se deben abordar en orden. No se deben empezar optimizaciones avanzadas o comportamiento de grupos hasta estabilizar el núcleo.

## P0 — Compatibilidad y fallos inmediatos

- [x] Eliminar las dependencias de `UnityEditor` de los archivos de `Runtime` o protegerlas completamente con `#if UNITY_EDITOR`.
  - Terminado cuando el ensamblado Runtime no importe `UnityEditor` y pueda incluirse en un Player build.
- [x] Hacer segura la obtención del pathfinder activo.
  - `PathfindingManager.GetInstance` no debe lanzar una excepción si no existe manager.
  - Debe quedar definido qué ocurre con managers duplicados y referencias ausentes.
- [x] Validar la configuración de `PathfindingRectangle`.
  - Sanear o rechazar tamaños de grid o celda cero y negativos.
  - Evitar divisiones entre cero y matrices con dimensiones inválidas.
- [x] Hacer segura la inicialización de `PathfinderMovement`.
  - Activarlo sin ruta no debe producir una excepción.
  - Desactivar el componente o destruir el GameObject debe finalizar o cancelar cualquier operación pendiente.
- [x] Corregir el cálculo no utilizado de `SetWalkableArea` o eliminar el método.
  - Se eliminó porque no tenía consumidores y `1 / 2` producía un radio entero igual a cero.
- [x] Eliminar variables y utilidades sin uso que dificultan entender el comportamiento actual.
  - Revisar `Tools`, `OnGridValueChangedEventArgs`, `_originPosition` y métodos auxiliares duplicados.

## P1 — Reescritura controlada del núcleo A*

- [x] Definir los contratos nuevos antes de sustituir el algoritmo.
  - `PathResult` con estado, waypoints y destino realmente alcanzado.
  - `PathQueryOptions` con diagonales, corner cutting, destino alternativo y límite de búsqueda.
  - Antes de la primera publicación se retiró el adaptador booleano; `PathResult` es el único contrato de resultado.
- [x] Separar el núcleo A* de `MonoBehaviour` y de las APIs de escena de Unity.
  - El núcleo debe operar sobre coordenadas y datos del grid ya construidos.
- [x] Representar el estado de búsqueda por índice de celda.
  - Una celda tendrá un único `gCost`, `hCost`, padre y estado abierto/cerrado durante cada consulta.
  - No se crearán nodos diferentes para una misma celda dentro de la consulta.
- [x] Sustituir la búsqueda lineal del coste mínimo por una cola de prioridad o binary heap.
- [x] Implementar correctamente la actualización de un nodo abierto cuando aparece un coste menor.
- [x] Eliminar el límite fijo de 200 elementos.
  - Si se necesita protección, usar un presupuesto configurable y devolver un estado explícito.
- [x] Validar origen y destino.
  - Fuera del grid, bloqueado, ya alcanzado y sin ruta deben producir resultados diferentes.
- [x] Impedir corner cutting.
  - Un movimiento diagonal sólo será válido si las celdas ortogonales requeridas también lo son.
- [x] Añadir búsqueda opcional de la celda alcanzable más cercana al destino solicitado.
- [x] Eliminar LINQ, diccionarios y asignaciones por cada celda expandida del camino crítico.
- [x] Reutilizar los arrays de costes, padres, estados y heap entre consultas.
  - Usar pooling o un contexto de búsqueda con ownership explícito.
- [x] Reducir la copia de la colección final sin perder la inmutabilidad pública de `PathResult`.
- [x] Crear una escena manual mínima para validar el núcleo antes de automatizar tests.
  - Cubre ruta recta, diagonal, esquina bloqueada, destino inaccesible y fallback a destino cercano.
  - Muestra estados `PASS/FAIL` en Play Mode y dibuja grids y rutas mediante Gizmos.
- [x] Revisar nombres y visibilidad de la API.
  - `Coordinates` es el único nombre público; el alias tipográfico se retiró antes de la primera publicación.
  - Evitar setters que dejen `TotalCost` desactualizado.

## P2 — Movimiento reutilizable

- [x] Convertir `PathfinderMovement` en una máquina de estados explícita.
  - Estados mínimos: `Idle`, `FollowingPath`, `Arrived`, `Blocked`, `Cancelled` y `Failed`.
  - Se añadió también `Paused` para que pausar no se confunda con inactividad o cancelación.
- [x] Definir una operación de movimiento cancelable.
  - Una orden nueva cancela la anterior exactamente una vez.
  - Eventos y tareas siempre terminan con un resultado definido.
- [x] Mover mediante `Rigidbody2D.position` y `Rigidbody2D.MovePosition` dentro de `FixedUpdate`.
- [x] Limitar el paso al waypoint para evitar overshoot y oscilación.
- [x] Hacer configurable la tolerancia de waypoint y validar que sea positiva.
- [x] Separar las tolerancias de waypoint y llegada final si los casos de uso demuestran que necesitan valores diferentes.
  - Los puntos intermedios priorizan fluidez; el destino final puede requerir precisión para interacción o formación.
  - Ambos valores mantienen `0.1` como default para no cambiar escenas existentes.
- [x] Separar el destino solicitado del destino alcanzable devuelto por navegación.
  - `PathfinderMovement` expone ambos destinos, `LastPathStatus` y overloads con `PathQueryOptions`.
- [x] Añadir eventos de movimiento.
  - Inicio, waypoint alcanzado, llegada, bloqueo, fallo y cancelación.
  - Cada evento publica un snapshot inmutable con `OperationId`, estado, destinos y diagnóstico de navegación.
  - El evento terminal se publica antes de completar la tarea con el mismo snapshot detallado.
- [x] Exponer dirección y velocidad real para alimentar animaciones.
  - `MovementDirection` es dirección deseada; `ActualVelocity` y `ActualSpeed` proceden del desplazamiento observado entre pasos de física.
- [x] Añadir suavizado básico del camino mediante comprobaciones de línea de visión.
  - Es opt-in con `PathQueryOptions.SmoothPath` y no cambia el comportamiento predeterminado.
  - Una travesía supercover rechaza celdas bloqueadas y respeta corner cutting.
  - Reutiliza el buffer de reconstrucción; sólo se reserva el array final de `PathResult`.
- [x] Separar los consumidores específicos:
  - Controlador point-and-click para aventuras gráficas.
  - Controlador de unidad para el RTS.
  - Viven en un ensamblado opcional que depende del núcleo, nunca al contrario.
  - La captura de input, selección e interacción específica permanecen fuera; las formaciones se implementan después como consumidor superior en P4.

## P3 — Construcción y actualización del grid

- [x] Separar `Grid` de sus fuentes de datos.
  - Fuentes independientes para Tilemap y rectángulo; ambas encapsulan su muestreo de colliders.
  - `IPathfindingGridSource` separa construcción y conversión de coordenadas de la fachada de consultas.
  - `RectangleGridSource` y `TilemapGridSource` producen snapshots completos y conservan su geometría de mapeo.
  - `CreateGridSource` es abstracto; se retiraron los hooks alternativos `BuildGrid` y `GetCellPosition` antes de publicar.
- [x] Definir claramente qué representa el Tilemap.
  - Por defecto, cualquier tile representa terreno potencialmente navegable y los huecos quedan bloqueados.
  - El modo de bounds permite tratar toda la región como terreno cuando sea intencional.
  - La máscara física se aplica después y la capa del Tilemap de terreno debe quedar fuera de ella.
- [x] Evitar que una celda vacía incluida accidentalmente en `Tilemap.cellBounds` amplíe el mapa sin control.
  - Los bordes vacíos se recortan sin modificar el Tilemap.
  - Se admiten límites explícitos y se rechazan grids que superen `MaximumGridCells`.
  - El Inspector informa del bounds efectivo y de celdas, tiles y posiciones transitables.
- [x] Separar obstáculos estáticos, obstáculos dinámicos y agentes móviles.
  - Sólo `StaticObstacleMask` se hornea mediante `IGridCellObstacleSampler`; obstáculos dinámicos y agentes quedan fuera del snapshot.
  - Las tres máscaras deben ser disjuntas y una clasificación solapada impide construir el grid con un diagnóstico explícito.
  - `StaticObstacleMask` es el único contrato público; se retiraron el alias protegido y la migración serializada anterior a producción.
- [x] Añadir actualizaciones parciales por región para edificios creados o destruidos.
  - `TryRefreshRegion` vuelve a muestrear un rectángulo conservador de celdas sin reconstruir la geometría.
  - Rectángulo y Tilemap reutilizan buffers internos y publican celdas evaluadas y modificadas.
  - Cambios fuera de los límites actuales requieren todavía una reconstrucción completa.
- [x] Versionar los datos del grid para detectar rutas invalidadas por cambios del escenario.
  - Cada lote con cambios incrementa una única versión y registra `LastNavigationDataChangedVersion` por celda.
  - Una reconstrucción completa inicia un snapshot nuevo aunque su walkability coincida.
  - El movimiento valida sus tramos pendientes al cambiar la versión y sólo recalcula si quedaron invalidados; los destinos alternativos se vuelven a consultar.
- [x] Añadir costes de terreno además de transitabilidad binaria.
  - Cada celda tiene un multiplicador entero positivo y A* lo aplica al entrar en ella.
  - `TilemapNavigationRule` traduce assets de tile a walkability y coste sin acoplar el núcleo a Tilemaps.
  - Los cambios regionales de coste participan en la misma versión y fuerzan a revisar rutas para conservar su optimalidad.
  - El suavizado se omite cuando hay terreno ponderado para no crear atajos que ignoren costes.
- [x] Añadir perfiles o clearance para agentes con tamaños distintos.
  - `PathAgentProfile` define un radio de mundo por consulta y el perfil se clona con las opciones.
  - El grid valida un clearance rectangular conservador alrededor de cada celda, incluyendo bordes y corner cutting.
  - El radio cero conserva exactamente el comportamiento histórico de agente puntual.
- [x] Reducir las consultas de `Physics2D` durante reconstrucciones.
  - Priorizar datos explícitos de Tilemap o nivel cuando estén disponibles.
  - Una máscara estática vacía evita crear el sampler físico.
  - El Tilemap permite desactivar `SampleStaticObstacles` y describir navegación enteramente con tiles y reglas.
  - Las actualizaciones regionales sólo consultan su rectángulo conservador y reutilizan el buffer del source.
- [x] Revisar el comportamiento en Edit Mode.
  - Evitar reconstrucciones costosas o recursivas desde `OnValidate`.
  - Sustituir `ExecuteInEditMode` por el ciclo de vida que se decida explícitamente.
  - `OnValidate` sólo sanea datos; no ejecuta física ni construye grids.
  - `Awake` construye en Play Mode y los inspectores ofrecen un botón explícito para regenerar la previsualización.

## P4 — Extensiones para RTS

- [x] Crear un planificador central de solicitudes de camino.
  - `PathRequestScheduler` mantiene cuatro colas FIFO y aplica prioridad con aging configurable.
  - `PathRequestHandle` expone estado, resultado, tarea de finalización y cancelación O(1) mientras espera.
  - Se limita tanto la cantidad de consultas como el tiempo consumido entre consultas en cada frame.
  - `PathfindingManager` es propietario del scheduler y `PathfinderMovement` encola órdenes iniciales y replans.
  - Una búsqueda A* ya iniciada sigue siendo síncrona y no puede interrumpirse; el presupuesto temporal es blando.
  - La caché acotada reutiliza resultados de consultas exactamente iguales sólo mientras la versión del grid sea la misma.
- [x] Instrumentar el peor caso de una búsqueda individual y decidir si A* necesita ejecución incremental.
  - El scheduler registra media, máximo y una ventana de 128 muestras para consultar P95, además de tiempo de espera y coste total por frame.
  - La escena RTS permite encolar 64 combinaciones de rutas largas, fallback, suavizado, prioridades y clearance.
  - Decisión para el primer vertical slice: conservar A* síncrono y el presupuesto blando; no asumir el coste y la complejidad de una búsqueda pausable sin observar antes un pico individual relevante.
  - La medición en el hardware objetivo queda como validación explícita posterior; si una sola consulta rebasa de forma repetida el presupuesto de navegación, se reabre la ejecución incremental.
- [x] Evitar starvation de solicitudes con prioridad baja.
  - Cada intervalo de espera configurable promueve una solicitud un nivel efectivo, hasta `Critical`.
  - Se compara el frente de cada cola y, con prioridad efectiva igual, gana la solicitud más antigua; FIFO se conserva dentro de cada nivel original.
  - Se exponen la espera máxima y el número de solicitudes promovidas para ajustar el intervalo con datos.
- [x] Asignar destinos de formación a una selección de unidades.
  - `RtsFormationDestinationPlanner` genera una cuadrícula compacta orientable y empareja unidades y slots cercanos de forma determinista.
  - Cada candidato se valida con una ruta real a través del scheduler y el resultado precomputado se reutiliza al comenzar el movimiento.
  - Los destinos resueltos se reservan con una separación mínima configurable para impedir que dos unidades reciban el mismo slot.
  - Un patrón en espiral busca alternativas cuando un candidato está bloqueado, es inaccesible o coincide con otra reserva.
  - El resultado puede ser `Assigned`, `PartiallyAssigned` o `Failed` después de un número máximo de intentos por unidad.
  - Una orden individual posterior prevalece sobre una formación todavía en cálculo y nunca es sobrescrita por un callback antiguo.
- [x] Añadir separación local entre unidades cercanas.
  - `IPathfinderMovementVelocityModifier` permite componer steering externo sin introducir reglas RTS en el núcleo de movimiento.
  - `RtsLocalSeparation` consulta agentes próximos con `Physics2D.OverlapCircle` y buffers reutilizados.
  - La repulsión está limitada respecto a la velocidad base y conserva un avance mínimo hacia el waypoint.
  - La influencia se suaviza y desaparece cerca del destino para permitir ocupar los slots de formación.
  - Las unidades siguen excluidas del grid estático; esto es separación suave, no navegación ni evitación de colisiones completa.
  - Se publican vecinos detectados, saturación del buffer y velocidad de separación para diagnóstico.
- [x] Detectar falta de progreso y unidades atascadas.
  - `RtsStuckDetector` compara el desplazamiento real durante ventanas consecutivas mientras la unidad debería avanzar.
  - Publica snapshots con operación, posición, progreso, intentos y agotamiento de recuperación.
  - Los intentos automáticos son opcionales y están limitados por operación; un atasco no se confunde con el estado de navegación `Blocked`.
- [x] Recalcular rutas con cooldown para evitar tormentas de solicitudes.
  - `PathfinderMovement.RequestRepath` conserva `OperationId`, tarea y destino solicitado.
  - Los replans manuales, por invalidación del grid y por atasco comparten un intervalo mínimo configurable.
  - Mientras espera el cooldown, el movimiento se detiene y no continúa sobre una ruta declarada inválida.
  - `LastRepathReason`, `RemainingRepathCooldown` y las notificaciones permiten diagnosticar la recuperación.
- [x] Evitar que agentes móviles se conviertan en obstáculos estáticos del grid.
  - La máscara de agentes se excluye de todas las reconstrucciones de las fuentes incluidas en el paquete.
- [x] Evaluar reservas temporales de celdas sólo para cuellos de botella si son necesarias.
  - Decisión: no introducirlas en el primer vertical slice. Slots únicos, separación y recuperación acotada cubren primero el caso común sin añadir planificación temporal ni deadlocks de reservas.
  - Se reabrirán únicamente si la escena medida reproduce bloqueos persistentes en pasos estrechos.
- [x] Compartir o almacenar resultados cuando varias unidades viajen a la misma zona.
  - La asignación de formación conserva y entrega su `PathResult` a la unidad, evitando calcular dos veces la misma ruta validada.
  - El scheduler mantiene una caché pequeña y de vida corta para consultas exactamente iguales: mismo pathfinder, origen, destino, opciones, perfil y versión del grid.
  - No se comparte una ruta completa entre orígenes distintos porque sus primeros tramos no son intercambiables; flow fields o árboles por objetivo quedan fuera hasta justificar su coste.
- [x] Mantener separada la lógica de infantería y vehículos si sus restricciones de giro divergen.
  - La infantería sigue usando velocidad omnidireccional sin componentes extra.
  - `RtsVehicleSteering` limita el giro por segundo, reduce velocidad durante giros cerrados y puede orientar el `Rigidbody2D`.
  - El giro es steering local; A* todavía usa clearance circular conservador y no modela radio de giro ni orientación en sus nodos.
- [x] Crear una escena manual de vertical slice con selección múltiple, orden de movimiento y obstáculos.
  - `Assets/Scenes/RtsVerticalSlice.unity` construye ocho unidades, una formación, obstáculos y un cuello de botella al entrar en Play Mode.
  - Permite selección por clic/arrastre, órdenes con botón derecho, ida/vuelta mediante botones y un burst manual de profiling.
  - El panel muestra estado de formación, atasco, carga del scheduler, espera, aging, caché, media, P95 y máximo.

## Hallazgos para revisar después del vertical slice

- [x] Ejecutar `RtsVerticalSlice` en el hardware objetivo y registrar media, P95 y máximo del burst de 64 rutas.
  - Dos ejecuciones aisladas en Unity 6000.5.9f1 completaron `64/64`: media `0,3113–0,3243 ms`, P95 `0,6180–0,6710 ms` y máximo `4,5360–5,8086 ms`.
  - El P95 queda por debajo del presupuesto blando de `2 ms`, pero el máximo lo supera de forma reproducible al arrancar un proceso limpio. A* incremental vuelve a quedar abierto para evaluación, no para implementación inmediata: primero hay que distinguir coste frío de JIT/buffers frente a un pico sostenido en un Player calentado y comprobar si existe un frame visible.
  - La espera máxima de `220–261 ms` no sirve para ajustar el scheduler: el runner batch avanza Play Mode mediante pasos del Editor y ese contador usa tiempo de pared.
- [x] Investigar el pico frío de `4,5–5,8 ms` con marcadores por consulta y una pasada calentada en Development Player.
  - `PathRequestScheduler` incorpora marcadores para frame, solicitud, caché, `FindPath` y almacenamiento, y `PathRequestMetrics` separa búsqueda de overhead.
  - Tres procesos limpios midieron 64 consultas frías y 192 calientes cada uno, siempre con `0` hits de caché.
  - En frío, el máximo total fue `3,56–3,69 ms` y `FindPath` consumió `2,39–2,45 ms`. La consulta máxima etiquetada fue `cold[3]`, primera en ejecutarse por su prioridad `Critical`, no un caso algorítmico excepcional.
  - En caliente, la media total quedó en `0,058–0,104 ms`, P95 en `0,191–0,327 ms`, máximo total en `0,517–1,425 ms` y máximo de `FindPath` en `0,509–0,667 ms`.
  - Decisión: mantener A* síncrono. El pico es de inicialización y desaparece por debajo del presupuesto blando de `2 ms`; una versión pausable sólo se reabrirá con mapas mayores o picos calientes repetidos en hardware objetivo inferior.
- [x] Corregir o justificar la asignación parcial de la formación del vertical slice.
  - Un Development Player normal reprodujo `PartiallyAssigned`: el hueco dejaba sólo dos filas útiles, mientras que el radio `0,32–0,38` sobre celdas de `0,5` exige tres filas por el clearance conservador.
  - El fallback resolvía todos los destinos al lado inicial del muro (`x = -1,25`); seis unidades reservaban posiciones allí y dos agotaban sus doce candidatos.
  - El hueco de la escena se amplió a cuatro filas transitables. La repetición visible terminó `Assigned`, con `8/8` llegadas, cero fallos y cero atascos.
- [x] Revisar el detector de atasco y su interacción con separación/repath en el cuello de botella.
  - Play Mode normal confirmó 21 notificaciones para una sola unidad: tres replans aceptados y el mismo agotamiento repetido en cada ventana posterior.
  - Una vez publicado el agotamiento, el detector continúa observando movimiento pero no vuelve a emitir el mismo episodio hasta detectar progreso o una operación nueva.
  - La variante estrecha de control publica ahora cuatro eventos —tres intentos y un agotamiento—; la escena corregida publica cero.
- [x] Limitar cuánto puede alejarse un fallback de formación del slot solicitado.
  - `MaximumFallbackDistance` limita en unidades de mundo la distancia entre cada candidato pedido y `ResolvedDestination`; `0` conserva explícitamente el comportamiento histórico ilimitado.
  - El valor inicial es `2`, mientras el vertical slice usa `1,25`. Sólo se aplica a `SuccessNearestReachable`; los destinos exactos no se penalizan.
  - `RtsFormationAssignment` conserva el último destino devuelto, su distancia y `LastRejectionReason`. Se distinguen `FallbackTooFar`, ruta no disponible, reserva ocupada, orden sustituida y rechazo del movimiento.
  - Un Development Player rechazó una alternativa a `15,25` unidades con límite `1,25` y estado `FallbackTooFar`; a continuación completó la formación válida con `8/8` llegadas y cero atascos.
- [x] Medir saturación y coste de `RtsLocalSeparation` con el número real de unidades simultáneas.
  - El componente incorpora marcadores separados para evaluación, `Physics2D.OverlapCircle` y steering, además de contadores temporales opt-in sin asignaciones por muestra.
  - Dos Development Players calentados midieron las ocho unidades actuales: `1.111–1.112` evaluaciones por recorrido, media total `0,0095–0,0097 ms`, consulta física `0,0048–0,0049 ms` y trabajo no físico `0,0047–0,0048 ms` por unidad y paso activo.
  - El máximo fue `0,2070–0,2274 ms` por evaluación. Se observaron como máximo cuatro vecinos con buffer de 24 y cero saturaciones.
  - Con separación, ambos recorridos acabaron `8/8` y sin avisos de atasco. El control sin separación acabó `7/8`, con una unidad bloqueada y un aviso en ambas pasadas; sus `3,97–4,02 s` no son una mejora frente a `4,45–4,47 s`, porque finaliza anticipadamente al fallar.
  - Decisión: el coste y el buffer son suficientes para las ocho unidades actuales. Repetir la medición si aumenta el máximo simultáneo, cambia el radio/densidad o aparecen saturaciones.
- [ ] Añadir reservas temporales sólo si se reproducen deadlocks persistentes en cuellos de botella; definir antes timeout, prioridad y recuperación de deadlock.
- [ ] Valorar árboles de rutas por objetivo o flow fields si muchas unidades parten de orígenes diferentes hacia la misma zona y la caché exacta apenas obtiene hits.
- [ ] Añadir evitación predictiva o una política específica para obstáculos dinámicos no agentes si el stuck detector sólo repite rutas equivalentes.
- [ ] Modelar radio de giro, orientación o perfiles anisotrópicos en la búsqueda únicamente si los vehículos cortan esquinas o no pueden seguir rutas válidas para infantería.
- [x] Definir prioridad explícita de los modificadores de velocidad si aparecen combinaciones cuyo resultado dependa del orden de `OnEnable`.
  - `IPathfinderMovementVelocityModifier` exige un orden de composición explícito.
  - Los valores menores se ejecutan primero y los empates conservan el orden de registro.
  - `RtsLocalSeparation` usa `LocalAvoidance = 100` y `RtsVehicleSteering` usa `LocomotionConstraint = 200`: la repulsión ajusta primero el rumbo deseado y el vehículo limita después su giro.
  - El diagnóstico registra deliberadamente vehículo antes que separación y confirma en Development Player `separation=100@0`, `vehicle=200@1`, `modifierOrder=PASS` y `result=PASS`.
- [x] Revisar el nombre `IIncrementalPathfinding`: hoy describe versionado/actualizaciones del grid, no una búsqueda A* pausable, y puede inducir a error en la API pública.
  - El contrato público se denomina ahora `IVersionedPathfinding`: expone versión del snapshot, actualización regional y validación de rutas retenidas.
  - `PathfinderMovement` y `PathRequestScheduler` consumen el nombre nuevo; ninguna ruta de ejecución lo interpreta ya como búsqueda A* incremental.
  - `IIncrementalPathfinding` se retiró antes de la primera publicación; `Pathfinding` implementa directamente `IVersionedPathfinding`.
  - `IIncrementalPathfindingGridSource` conserva su nombre porque sí vuelve a muestrear incrementalmente una región de una fuente sin reconstruir su geometría.
  - El archivo y su `.meta` se renombraron a `IVersionedPathfinding` conservando el GUID.
- [ ] Ejecutar manualmente la interacción completa de la escena en Play Mode.
  - El cruce automatizado ya se comprobó visualmente en Development Player; todavía falta recorrer selección por clic/arrastre, botón derecho y botones del panel de forma manual.

## P5 — Experiencia de Editor y paquete

- [x] Reducir el coste de los gizmos.
  - El grid se dibuja sólo al seleccionar el pathfinder; sin selección no se ejecuta trabajo de visualización.
  - `Bounds Only`, el modo inicial, traza únicamente cuatro líneas y no inspecciona celdas.
  - `Sampled Cells` inspecciona una muestra uniforme acotada —512 celdas inicialmente, configurable entre 16 y 4096— sin recorrer la colección completa.
  - La muestra puede filtrarse por todas, bloqueadas, transitables, terreno ponderado o cambios de la última versión. El relleno es opcional y queda desactivado inicialmente.
  - Los inspectores muestran `celdas inspeccionadas / total` y advierten cuando el filtro no es exhaustivo por estar limitado a una muestra.
  - El diagnóstico en Development Player terminó con `gizmoBudget=PASS/0/384` y `result=PASS`; esto valida la configuración inicial y la ausencia de regresiones runtime. La visualización Editor continúa siendo una comprobación manual.
- [x] Eliminar la reflexión del inspector de movimiento.
  - Exponer un snapshot de depuración de sólo lectura o propiedades internas apropiadas.
- [x] Añadir mensajes de configuración inválida claros en el Inspector.
  - `Pathfinding.TryValidateConfiguration` es la única prevalidación usada por Runtime y por los inspectores de rectángulo y Tilemap.
  - `IPathfindingGridSourceConfigurationValidator` permite que una fuente publique sus reglas sin construir el grid ni muestrear celdas; las dos fuentes incluidas implementan el contrato.
  - `Refresh` rechaza máscaras solapadas, referencias, tamaños, semánticas y bounds inválidos usando los mismos mensajes que muestra el Inspector.
  - Los conflictos de máscaras identifican campos y capas por nombre/número y explican cómo corregirlos.
  - El Inspector diferencia configuración inválida, prevalidación correcta sin snapshot, snapshot activo, último build fallido y última actualización regional fallida. El botón de reconstrucción queda deshabilitado mientras la prevalidación falla.
  - Los errores que dependen del contenido completo —por ejemplo, bounds explícitos sin ningún tile de terreno— se calculan sólo al reconstruir y se conservan en `LastGridBuildError`, evitando recorrer el Tilemap en cada repintado del Inspector.
  - Runtime y Editor compilaron sin avisos; el Development Player terminó con `configurationPreflight=PASS` y `result=PASS`. La apariencia de los mensajes continúa siendo una comprobación manual de Editor.
- [x] Añadir a `PathfindingRectangle` un máximo de celdas configurable y validarlo antes de reservar el grid, equivalente a `MaximumGridCells` del Tilemap.
  - `PathfindingGridLimits.DefaultMaximumCellCount` centraliza el valor inicial de 262.144 celdas para ambas fuentes.
  - `PathfindingRectangle.MaximumGridCells` se serializa, se muestra en el Inspector y dispone de un overload de `Configure` para escenas construidas en runtime.
  - Los constructores existentes de `RectangleGridSource` conservan su firma y adoptan el límite inicial; nuevos overloads permiten indicar otro presupuesto.
  - La prevalidación calcula dimensiones con `double`, comprueba cada eje y el producto antes de convertir a `int` o reservar el array bidimensional.
  - El error informa de dimensiones, celdas solicitadas, máximo y alternativas: aumentar la celda, reducir el mundo o elevar el presupuesto deliberadamente.
  - Un valor serializado ausente o cero se migra al límite inicial para no convertir escenas antiguas accidentalmente en grids de una sola celda.
  - Runtime y Editor compilaron sin avisos. El Development Player rechazó `4 > 3` con el mismo mensaje en preflight/build y terminó con `rectangleGridBudget=PASS/384/262144` y `result=PASS`.
- [x] Normalizar el nombre de carpeta a `Packages/com.sparkygames.pathfinder` cuando no rompa referencias del workspace.
  - La carpeta física coincide con `package.json` y `packages-lock.json` apunta a `file:com.sparkygames.pathfinder`.
  - Los 49 scripts se trasladaron junto a sus `.meta`, conservando contenido y GUID.
  - Runtime se divide en `Core`, `GridSources`, `Components`, `Movement` y `Scheduling`; Consumers en `Common`, `PointAndClick` y `RTS`; Editor en `Pathfinding` y `Movement`.
  - Los tres `asmdef` permanecen en sus raíces y siguen incluyendo recursivamente los scripts, sin cambiar nombres de ensamblado ni namespaces.
  - Unity regeneró los proyectos C#, importó las diez carpetas nuevas, resolvió el paquete desde la ruta normalizada y no volvió a emitir el aviso de nombre de directorio.
  - Se comprobaron 69 GUID de assets y carpetas sin duplicados. El Development Player y el diagnóstico RTS terminaron con `result=PASS` desde una copia aislada de la estructura nueva.
- [x] Actualizar la compatibilidad y metadatos de `package.json` para Unity 6 tras verificar el Player build.
  - El mínimo declarado es Unity `6000.0`; se retiró `unityRelease: 0a19` para no restringir el paquete a una alfa antigua concreta.
  - La descripción y las palabras clave identifican los dos usos previstos: aventuras point-and-click y prototipos RTS 2D.
  - El manifiesto declara sus dependencias directas sobre `com.unity.modules.physics2d` y `com.unity.modules.tilemap`, ambas `1.0.0`, y `packages-lock.json` conserva el mismo grafo.
  - La primera versión pública se fija en `1.0.0`; el `1.0.2` heredado no correspondía a tags o publicaciones anteriores. Licencia y changelog ya disponen de artefacto y URL propias; la URL de documentación se añadirá cuando exista su destino público definitivo.
  - Unity `6000.5.9f1` importó el manifiesto y resolvió el paquete desde una copia aislada. Runtime y Editor compilaron, el Development Player se construyó sin errores y el diagnóstico terminó con `result=PASS`.
- [x] Mantener el paquete como repositorio Git independiente.
  - `.git` permanece en la raíz del paquete, tal como ha decidido el proyecto.
  - `.vs` es estado local del IDE, ya está cubierto por `.gitignore` y no aparece entre los archivos versionados.
- [x] Retirar API obsoleta y adaptadores de compatibilidad antes de fijar la primera línea pública.
  - Eliminados los cinco contratos marcados `[Obsolete]`: constructor de `Grid` con origen ignorado, `GridCell.Coordenates`, `GridCell.LastChangedVersion`, `Pathfinding.colliderMask` e `IIncrementalPathfinding`.
  - Eliminados el `FindPath(..., out IEnumerable)`, `SetActive(bool)`, los hooks `BuildGrid`/`GetCellPosition`, el grid source legacy y los constructores de fuentes que ocultaban la creación del sampler.
  - `IPathfinding.TryGetWalkablePosition` sustituye al ambiguo `GetPath`; `PathResult` es el único resultado de búsqueda.
  - Los modificadores declaran el orden directamente en `IPathfinderMovementVelocityModifier`; ya no existe un segundo contrato opcional.
  - Los callbacks booleanos de movimiento se retiraron. `MoveToAsync` y los controladores especializados devuelven un `PathfinderMovementNotification` terminal con la causa completa.
  - Unity recompiló Runtime, Consumers, Editor y las escenas sin errores. Una copia aislada construyó el Development Player y terminó con código cero y `result=PASS`; el paquete no emitió avisos `CS0618`.
- [x] Añadir una escena o sample mínimo para aventura gráfica.
  - `Assets/Scenes/GraphicAdventureSample.unity` monta un fondo, un personaje, un grid rectangular, un bloqueo estático invisible, el scheduler y el consumidor point-and-click.
  - Los PNG se importan a 16 PPU, Point, sin mipmaps ni compresión; el personaje usa pivote en los pies y la cámara URP es pixel perfect a 640 × 360.
  - Clic izquierdo solicita movimiento con suavizado y destino alcanzable más cercano; clic derecho/Escape cancela y `R` reinicia el personaje.
  - El panel consume las notificaciones detalladas de movimiento y distingue el destino solicitado del resuelto.
  - El Development Player terminó con `result=PASS`, `fallback=PASS/SuccessNearestReachable` y `cancellation=PASS/Cancelled`.
  - La escena vive deliberadamente en el proyecto consumidor. Antes de publicar un sample UPM habrá que usar assets redistribuibles y decidir qué contenido se copia a `Samples~`.
- [x] Añadir una escena o sample mínimo para unidad RTS.
  - `Assets/Scenes/CommandAndConquerSample.unity` usa el fondo 1280 × 720 y el soldado 32 × 32 aportados por el proyecto, ambos a 16 PPU y con cámara pixel perfect 640 × 360.
  - Incluye 15 soldados, selección por clic/arrastre, órdenes de formación con botón derecho, cancelación y desplazamiento acotado de cámara.
  - Cada unidad integra movimiento, perfil de agente, separación local y detector de atasco; la presentación añade anillo de selección, marcador de destino y orden Y de sprites fuera del paquete.
  - El Development Player terminó `Assigned` con `15/15` slots y `15/15` llegadas, sin errores runtime.
  - La validación descubrió que `RtsFormationDestinationPlanner.OnDisable` podía usar una referencia Unity destruida durante el cierre; ahora la comprueba explícitamente antes de cancelar.
- [x] Añadir documentación autocontenida para integrar el paquete desde Git y continuar el trabajo desde otro chat o proyecto.
  - `Documentation~/index.md` enlaza instalación, point-and-click, RTS, operación y handoff siguiendo la convención de documentación UPM.
  - Las guías explican capas, fuentes de grid, manager, scheduler, agentes, formación, eventos, gizmos, métricas y actualizaciones regionales sin depender de las escenas del proyecto consumidor.
  - `Documentation~/handoff.md` conserva las decisiones arquitectónicas, la información mínima de un proyecto nuevo, un checklist y un prompt reutilizable para otra sesión.
  - El README del paquete y el del proyecto apuntan al manual; las escenas y assets actuales permanecen fuera del repositorio independiente del paquete.
- [ ] Preparar `Samples~` antes de distribuir ejemplos con el paquete.
  - No mover automáticamente los sprites actuales: primero confirmar su licencia y separar arte de juego de los recursos redistribuibles del paquete.
- [x] Añadir una licencia explícita antes de distribuir el paquete.
  - `LICENSE.md` contiene la licencia MIT con `Copyright (c) 2026 Sparky Code Studios`.
  - `package.json` publica `licensesUrl` hacia el archivo del repositorio y el README enlaza la licencia local.
  - La licencia cubre el código del paquete; no concede por sí sola derechos sobre assets de proyectos consumidores o futuros samples.
- [x] Añadir `CHANGELOG.md` antes de distribuir el paquete.
  - Sigue Keep a Changelog y versionado semántico.
  - La primera recopilación se publica como `[1.0.0] - 2026-09-04`; `Unreleased` queda reservado para trabajo posterior.
  - Resume API, navegación, movimiento, scheduler, consumidores, tooling, correcciones y retirada de compatibilidad legacy.
  - `package.json` expone `changelogUrl` hacia el archivo del repositorio.
- [x] Confirmar el número del primer tag de producción.
  - `package.json` declara `1.0.0` y el tag correspondiente es `v1.0.0`.
  - Esta versión define la primera API pública estable; los incrementos posteriores seguirán SemVer.
- [ ] Revisar comentarios XML y nombres públicos para que describan comportamiento, errores y ownership de las colecciones.

## Fuera de la fase actual

Estas líneas no deben adelantarse hasta disponer de un vertical slice medido:

- Tests automatizados, aplazados por decisión del proyecto.
- Unity Jobs y Burst.
- Flow fields.
- ECS/DOTS.
- Multithreading propio.
- Soporte para cientos o miles de agentes.
- Networking o simulación determinista.

## Próxima tarea recomendada

Preparar la futura distribución de ejemplos mediante `Samples~`, definiendo primero qué assets son redistribuibles y cuáles deben permanecer únicamente en el proyecto consumidor.

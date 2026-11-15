# Mission: Ares — Plan de trabajo de tres agentes

## 1. Alcance y estado de este documento

Esta entrega es de **planificación**, no de implementación del juego ni de certificación del controlador. Se inspeccionó el repositorio y se ejecutó una línea base automatizada. Las comprobaciones manuales, de navegador, de teléfono físico y de integración completa deben realizarse durante la ejecución del plan.

La misión detallada del controlador es un requisito de la fase de implementación: entonces exige correcciones justificadas, commits, publicación de `fix/phone-controller-integration` y, si hay autenticación, un PR sin fusionar. No convierte esta solicitud de planificación en una autorización para implementar inmediatamente.

Tres agentes participaron en paralelo: A1 inspeccionó escenas y narrativa; A2, controlador y consumidores; A3, integración y validaciones. Sus argumentos temporales y la resolución técnica se identifican más adelante. Las decisiones de producto que no puedan deducirse del código se remiten al equipo humano.

## 2. Línea base y protección del repositorio

- Repositorio remoto: `https://github.com/mqcro8/ares`.
- Estado inicial: `master`, árbol limpio, HEAD `b796ab8` (`Merge branch 'feat/audio-feedback-cc0'`).
- SHA completo de HEAD, master y origin/master en la inspección: `b796ab87fc9e98da73957002a68139d237f50311`.
- Se ejecutaron `git status --short --branch`, `git log --oneline -10`, `git branch -avv`, `git remote -v`, `git fetch origin`, `git rev-list --left-right --count HEAD...origin/master` y `git worktree list`.
- Tras el fetch, la comparación fue `0 0`: HEAD y `origin/master` coinciden en esta inspección. No se presupone que seguirán coincidiendo al ejecutar el plan.
- Ramas locales adicionales: `scene/gateway` (`6a11a1b`); `scene/pre-launch` (`2e0530a`, un commit por delante de su remoto); `scoring_system` (`ab788ab`, tres commits por delante). Esos commits no se descartan ni se incorporan por suposición.
- Ramas remotas relevantes existentes: `origin/controlers`, `origin/feature/vercel-phone-controller`, `origin/fix/controller-merge-recovery`, además de ramas de escenas. Su existencia no demuestra que sus cambios falten en master.
- Inicialmente solo existía el worktree principal. No se encontraron archivos `AGENTS.md` dentro del repositorio.
- Para escribir este documento se creó `docs/ares-multiagent-work-plan`. No se modificó la referencia `master`.

Los diez commits observados fueron `b796ab8`, `fa3c38a`, `b672057`, `067a8c2`, `e0dc990`, `8b0a8ce`, `d7eb719`, `8aa6161`, `6ec89d0` y `9c18ada`. La historia reciente contiene merges: al atribuir regresiones se inspeccionarán los padres y el diff, no solo el asunto del commit.

### Entorno observado

`ares.csproj` declara `Godot.NET.Sdk/4.7.2`, `net8.0` y una excepción Android `net9.0`. El SDK de .NET disponible es `10.0.402`; esto no cambia el target del juego. Node es `v24.16.0` y npm `11.13.0`.

`godot` no está en PATH. El ejecutable encontrado es `/Applications/Godot_mono.app/Contents/MacOS/Godot`; `--version` devolvió `4.7.2.stable.mono.official.ed1daf0bf`.

`project.godot` es la referencia para la configuración efectiva: incluye `MissionState`, `SceneManager`, `SceneTransition`, `AudioManager`, `PauseManager`, `ControllerRelay` y `DevCheats`. El README solo enumera parte de estos autoloads y contiene información histórica: no se usa como evidencia de funcionamiento.

Otra discrepancia con el contexto proporcionado: `.editorconfig` no contiene solamente UTF-8. Sus líneas 6–21 también especifican apertura de llaves C# y sugerencias de nombres para campos privados y constantes. No fija indentación. No se encontraron archivos versionados bajo `.github`; esto no demuestra ausencia de automatización externa.

## 3. Responsabilidades, límites y orden de trabajo

| Agente | Responsabilidad | Entregable | Límites |
|---|---|---|---|
| A1 | Sprites Training/Gateway; jugabilidad local de todas las escenas y minijuegos; narrativa; instrucciones visuales | Rama y reporte por escena, capturas, rutas de reproducción, revisión de textos | No cambia protocolo ni reglas de puntuación sin diagnóstico y coordinación |
| A2 | Mando telefónico, desde navegador hasta efecto real en cada escena y contexto de vuelta | Diagnóstico por escena, corrección mínima, pruebas y rama validada | No presume funcionamiento por handlers; preserva teclado/ratón y controlador opcional |
| A3 | Integración, prevención de conflictos, regresión de campaña y arbitraje técnico | Incidencias devueltas, integración trazable y rama final de revisión propuesta | No fusiona ni escribe en master; no sustituye decisiones humanas de producto |

### Fases y dependencias obligatorias

1. **P0 — Baseline común:** repetir fetch y diagnóstico Git; registrar SHA base, herramientas, pruebas y limitaciones. Crear worktrees independientes. A2 inicia su auditoría en la rama obligatoria desde el último `origin/master`.
2. **P1 — Sprites primero:** A1 compara Training y Gateway, acuerda qué elementos equivalentes se estandarizan, modifica lo mínimo y valida animación, escala, colisiones, oclusión y feedback. No impone identidad entre funciones visuales distintas. A2 puede auditar transporte y configuración; A3 prepara matriz, ownership y seguimiento.
3. **P2 — Revisión por escena:** A1 prueba cada escena y cada minijuego, registra rutas de éxito/fallo/retorno y revisa narrativa. Entrega incrementalmente cada escena terminada a A2. A2 trabaja mientras A1 pasa a la siguiente; A3 integra entregas A2 aceptadas mientras ambos continúan.
4. **P3 — Barrera de cobertura:** antes de transformar las instrucciones, todas las escenas del inventario deben tener una prueba registrada. Una escena no ejecutable se marca bloqueada y se eleva; no se cuenta como probada. Los prefabs se prueban en su anfitrión y los prototipos aislados se identifican expresamente.
5. **P4 — Instrucciones visuales:** A1 convierte instrucciones en demostraciones, iconos, estado y feedback, manteniendo inglés, accesibilidad y reglas actuales. Estas son nuevas revisiones de las ramas por escena; vuelven a pasar por A2 y A3.
6. **P5 — Cierre de campaña:** A3 prueba las combinaciones y regresiones transversales sobre el candidato exacto, devuelve defectos y prepara el reporte humano. La rama final propuesta es `review/ares-multiagent-integration`; el nombre no fue fijado por el usuario.

La auditoría técnica del controlador puede comenzar en paralelo con P1. La aprobación telefónica de una escena requiere el SHA entregado por A1. El cierre de instrucciones requiere P3. El cierre final requiere las revisiones visuales y sus regresiones, no solo la primera pasada.

## 4. Git, entregas y prevención de conflictos

### Preparación segura

Antes de cada edición: leer `git status`, inspeccionar el archivo actual y comprobar propietario. No hacer stash automático, reset destructivo, limpieza ni sobrescribir ramas existentes. Si aparecen cambios ajenos, preservarlos en el worktree original y crear otro worktree limpio.

En la ejecución, repetir:

```sh
git fetch origin
git status --short --branch
git log --oneline -10
git rev-list --left-right --count HEAD...origin/master
git branch -avv
git worktree list
git show-ref --verify refs/heads/fix/phone-controller-integration
git ls-remote --heads origin fix/phone-controller-integration
```

La ausencia de una rama en `show-ref` produce un código no cero esperado; no es un permiso para ignorar otros errores. Si la rama obligatoria ya existe local o remotamente, inspeccionar dueño, commits y trabajo pendiente. No sobrescribirla ni inventar otro nombre para cumplir el requisito: resolver con el equipo si se continúa la existente o se archiva de forma autorizada.

Solo después de comprobar la ausencia, crear el worktree A2 y cambiar su sesión a ese directorio:

```sh
git worktree add -b fix/phone-controller-integration ../ares-phone origin/master
```

El SHA resuelto de `origin/master` queda registrado como `BASE`. A1 y A3 usan worktrees separados y ramas derivadas de ese mismo SHA. Nunca ejecutar checkout de una rama ajena dentro del directorio de otro agente. Cada sesión debe apuntar explícitamente a su worktree; los subagentes de OpenCode no implican aislamiento automático de archivos.

### DAG propuesto

```text
BASE = origin/master al iniciar la implementación
  ├─ work/phone-shared-contract       A2: solo cambios comunes, sin escenas
  ├─ work/ares-scene-foundation       A1: sprites comunes aprobados
  │    └─ work/scene-<id>-r1          A1: revisión local de escena
  │         └─ fix/phone-<id>-r1      A2: diagnóstico y corrección telefónica
  ├─ fix/phone-controller-integration A2: auditoría compartida y agregación
  └─ integration/ares-scenes          A3: integración serial y regresiones
        └─ review/ares-multiagent-integration  propuesta de revisión humana
```

`<id>` identifica una unidad del inventario; `rN` identifica una revisión inmutable entregada. Una rama por escena puede contener sus prefabs relacionados, siempre enumerados. No se crean ramas separadas para cada nodo visual.

Reglas operativas:

1. A1 termina un cambio coherente, revisa su diff y crea un commit. Entrega nombre de rama, SHA, base, dependencias, archivos, pruebas, incidencias y recursos nuevos. No entrega modificaciones sin commit.
2. A2 crea su rama por escena desde el SHA de A1. Incorpora únicamente el checkpoint necesario de `work/phone-shared-contract`, con su SHA explícito. Esta rama nace de BASE y no recibe escenas. A2 incorpora sus commits también en `fix/phone-controller-integration`; nunca usa la punta del agregado como dependencia común de nuevas escenas.
3. A2 integra las ramas de escena aceptadas en `fix/phone-controller-integration` mediante merges trazables. Así la rama obligatoria nace de master y contiene también las dependencias A1 necesarias. Sus commits no se pierden en ramas auxiliares. No se mezclan copias por cherry-pick y merge de un mismo cambio.
4. A3 recibe la rama de escena de A2 y sus dependencias, y hace integración serial en su propio worktree. Cuando una dependencia común ya está integrada, Git conserva su identidad de commit. No rebase ni force-push de entregas compartidas.
5. A1 no toma como base un agregado que contenga escenas ajenas pendientes. Usa BASE, la base de sprites y dependencias aprobadas. Para corregir una escena ya revisada, crea `r2` desde el último SHA aceptado de esa escena, conservando las correcciones A2 y las resoluciones A3. A3 devuelve sus resoluciones al propietario para incorporarlas a la línea de esa escena antes de iniciar `r2`; si no pueden aislarse, registra explícitamente las dependencias adicionales del checkpoint integrado.
6. A3 comprueba el merge en un worktree de integración limpio. Si hay conflicto, documenta archivos, ambos SHAs, intención y pruebas afectadas. El propietario propone la resolución; A3 valida el diff y las pruebas antes de aceptarla. No usar “ours/theirs” en bloque.
7. A3 devuelve incidencias a A1. Si afectan mensaje, contexto, entradas, foco, pausa o UI telefónica, A2 participa de nuevo. A3 conserva el checkpoint anterior; una entrega fallida no se etiqueta como aprobada.
8. Al cerrar, A2 incorpora todas las correcciones finales del controlador y sus dependencias en su rama obligatoria; A3 incorpora ese SHA final. Ambos cotejan un manifiesto de commits aceptados y `git diff <SHA-A2> <SHA-A3>`; toda diferencia debe estar enumerada y justificada. Si los contenidos coinciden, registrar igualdad de árboles. La aceptación corresponde al SHA final publicado, incluidas resoluciones de merge.

El flujo de entrega efectivo por escena es `A1(SHA) → A2(SHA + contrato) → A3(SHA integrado y probado)`. “Validada por A2” no significa todavía “aceptada por A3”.

### Ownership de archivos comunes

- A1: escenas, sprites, diálogos, instrucciones y consumidores de gameplay; avisará a A2 antes de tocar handlers o ciclo de vida de un consumidor.
- A2: `controlers/`, `scripts/systems/input/`, configuración telefónica y cambios de input compartido.
- A3: integración y reportes; coordina cambios a `MissionState`, `project.godot`, `PauseManager`, transiciones, scoring y pruebas transversales.
- Cada cambio compartido tiene un único autor activo y un checkpoint. Los otros agentes envían solicitudes de parche y esperan ese checkpoint. Las pruebas tienen propietario por defecto del componente y revisión A3.
- A1 conserva ownership de `scripts/training/PracticeReturn.cs`, `PracticeSession.cs`, `scripts/mars/MinigameOverlay.cs` y `scripts/systems/minigames/`. A2 solicita cambios de estos consumidores o recibe una cesión explícita, limitada por ticket y checkpoint. A3 coordina `project.godot`; A2 no adquiere propiedad completa del archivo por editar opciones telefónicas.
- Cada worktree necesita importación/caché Godot propia. Los procesos usan puertos y salas diferentes. `user://controller.cfg` puede compartirse por nombre de aplicación aunque Git esté aislado: usar perfiles de ejecución separados o serializar cambios de configuración. Serializar inicialmente pruebas gráficas y archivar capturas entre ejecuciones; las rutas fijas bajo `/tmp/opencode/` pueden sobrescribirse.

### Publicación en la fase de implementación

Después de la regresión y revisión del diff, A2 debe publicar su rama:

```sh
git push -u origin fix/phone-controller-integration
git rev-parse HEAD
git ls-remote --heads origin fix/phone-controller-integration
```

Ejecutar estos comandos desde el worktree de esa rama. Comparar el SHA remoto con el local. Si hay autenticación GitHub, crear PR hacia `master` con diagnóstico, cambios, pruebas y límites; dejarlo abierto, sin merge. Publicar también las ramas de entrega necesarias y la candidata final acordada. Un fallo de autenticación es un bloqueo de publicación, no una entrega completada. Nunca hacer force-push ni modificar, fusionar o hacer push a `master`.

## 5. Bucle de diagnóstico, corrección y aceptación

Cada incidencia sigue **Teoría → Evidencia → Causa raíz → Corrección mínima → Verificación**.

1. Registrar síntoma, escena, SHA, condiciones, pasos, esperado según fuente y resultado observado.
2. Formular hipótesis concreta. Recoger código, logs y reproducción; separar sospechas de defectos confirmados.
3. Identificar causa raíz antes de editar. Si el comportamiento ya es correcto, no cambiarlo.
4. Aplicar la corrección localizada; añadir regresión focalizada si falta cobertura del fallo confirmado. No debilitar tests.
5. Repetir el caso original y las rutas vecinas con teclado/ratón y teléfono cuando corresponda.
6. A3 revisa la evidencia. Una nueva falla vuelve a A1 y, si afecta control telefónico, a A2.

### Narrativa: criterio de cierre observable

A1 mantiene una tabla por línea o secuencia: emisor, contexto de entrada, estado de misión requerido, afirmaciones, acción sugerida, efecto real y siguiente escena. Contrasta texto y cinemática con código, datos y reproducción.

Cada iteración registra contradicciones detectadas, corrección y nueva lectura completa del recorrido afectado. El modelo puede declarar la revisión satisfactoria solamente cuando no quedan contradicciones identificadas, las instrucciones describen acciones disponibles, las ramas éxito/fallo concuerdan con el estado de misión y cada afirmación dispone de evidencia. Otra pasada debe revisar las consecuencias del cambio, no solo la frase editada. Una ambigüedad de producto pasa al equipo humano; no se resuelve inventando reglas.

### Estados de validación

- `PENDIENTE`: todavía no se ha ejecutado.
- `BLOQUEADA`: faltan herramientas, dispositivo, acceso, decisión o una dependencia.
- `FALLA`: reproducción o test demuestra incumplimiento.
- `PASA-AUTO`: comando ejecutado, salida y código registrados.
- `PASA-MANUAL`: pasos, dispositivo y resultado observados registrados.
- `ACEPTADA`: A3 revisó todas las comprobaciones obligatorias de esa unidad.

Un build o test unitario no equivale a jugabilidad. La ausencia de incidencias registradas tampoco equivale a aceptación.

Para cerrar una escena deben estar verificadas entrada, interacción, objetivo, salida, fallo/cancelación cuando existan, persistencia de estado, narrativa, feedback visual y controles pertinentes. Para cerrar campaña se requiere el recorrido completo y las variantes terminales, sin usar atajos de desarrollo como prueba de éxito normal.

## 6. Validación telefónica de extremo a extremo

Por cada interfaz, registrar la cadena completa:

1. El navegador muestra el panel correcto para la escena y fase.
2. El gesto real produce el mensaje y valor esperado.
3. El relay Node real lo recibe y lo remite a la sala correcta.
4. Godot recibe y enruta el mensaje al consumidor activo.
5. El estado visible y de gameplay cambia como corresponde.
6. El contexto vuelve al teléfono y la pantalla/restauración coincide.
7. Soltar, cancelar, cambiar de escena, pausar o desconectar no deja entradas activas.

Matriz mínima:

- Menú, opciones, persistencia de enabled/URL/sala, QR y URL escaneada.
- Emparejamiento con teléfono antes/después del juego; recarga, conexión duplicada, pérdida de red y reconexión.
- Joystick y fuerza analógica; interacción; salto; hotbar 1–5.
- Apertura, inicio, reinicio, finalización, cancelación y retorno de cada práctica.
- Cableado, fusibles, frecuencia, casco y lanzamiento: efecto real hasta resolución.
- Rover: volante táctil, permiso de orientación, calibración, zona neutra, soltar y respuesta.
- Intro, PreLaunch, finales y overlays: avance/salida donde exista, foco y bloqueo de gameplay.
- Transiciones y retornos permitidos por el flujo actual; no añadir retornos prohibidos para satisfacer la prueba.
- Pausa/reanudación y cierre del juego; teclado/ratón independiente con relay apagado.
- Validación del protocolo en servidor, comandos inválidos, contextos obsoletos, orden de eventos y listeners duplicados.

Ejecutar relay Node y Godot reales simultáneamente. Usar automatización de navegador para layout, eventos táctiles, comandos, conexión y cambios de pantalla si está disponible. Registrar qué eventos son simulados. Una prueba sintética de orientación no valida sensores físicos: HTTPS, permiso, calibración y movimiento con teléfono físico requieren su propia evidencia. Si no se dispone de teléfono, marcar esa cobertura como no verificada y no afirmar compatibilidad completa.

## 7. Restricciones que acompañan cada entrega

- Nueva lógica de juego en C#; mantener arquitectura, namespaces y estilo local. No introducir dependencias, abstracciones ni refactors sin necesidad demostrada.
- Preservar `.cs.uid` y referencias `uid://`; mover o renombrar scripts mediante Godot, sin borrar sidecars a mano. No modificar generados salvo necesidad real.
- `MissionState` mantiene equipo, progreso, recursos y resultado entre escenas. No resetear al entrar ni añadir score por callbacks repetidos.
- Mantener scoring Training 10%, Launch 20%, Repairs 25%, Rover 20%, Resources 25%; aprobación ≥80%, reserva de retorno y `AllMissionsWon`. Verificar contra las fuentes y pruebas antes de tocar reglas.
- Lanzamiento real fallido conduce a resultados; práctica repetible no significa lanzamiento real repetible. PreLaunch continúa a Gateway mediante `SceneTransition`.
- Rover perdido continúa a MarsBase con Rover=0 y `RoverStageResolved`, sin repetir el rover. La reserva de rescate sin tanques no aumenta `TankCount` ni cargo. Agotar oxígeno termina en resultados.
- Oxígeno exterior Mars, capacidad base de 120 s por tanque y efecto del regulador según implementación; no rellenar al reentrar. No adjuntar `MarsBaseDebug` por suposición.
- Training conserva `PracticeSession` (`Prepare`/`Arrive`/`Begin`/`Finish`, `RequestLaunch`/`ConsumeLaunch`), tickets de un uso y restauración de pose. No sustituir por cambios directos de escena.
- Gateway no introduce otro locker. Gating `LaunchPadUnlocked` y aviso único `TakeTrainingCompletionNotification` se conservan.
- Atajos `Shift+ARES`/`Shift+ERASE` conservan salidas válidas; no convierten fallos normales en victorias.
- Audio usa `AudioManager`, `SoundIds`, banco y buses existentes; one-shots por `PlaySfx`/`PlaySfx2D`, loops con dueño y parada. `FeedbackSounds` limita audio, nunca gameplay. Pasos adecuados a interior/exterior, sin física bloqueada ni en el aire.
- Audio nuevo requiere licencia compatible y procedencia en `data/audio/sources.json` y `credits.md`. La procedencia parcial de audio previo es una limitación existente, no una licencia implícita.
- UI y diálogos permanecen en inglés. Reutilizar componentes basados en datos y bloqueo físico/salto de TrainingIntro para diálogo nuevo.
- Preservar rutas hardcodeadas de nodos, `MenuTheme.tres` y `MenuShell.tscn`, contraste y foco. Mantener la identidad de menú indicada: índigo, lavanda, púrpura/cian y acento dorado; Aerospace y `MISSION CONTROL`.
- `hardware/` es opcional y está excluido de Godot. Juego local y teclado deben funcionar sin servidor ni hardware.
- La guía proporcionada indica ausencia de formatter, linter y CI, e indentación mixta; seguir archivos vecinos y revisar configuración real antes de añadir herramientas. No presentar el README como implementación.

## 8. Formatos de reporte

### Reporte de escena

```markdown
# <ID> — <escena> — revisión <N>
Estado:
Agente / revisor:
Base / rama A1 / SHA A1:
Rama A2 / SHA A2 / checkpoint controlador:
Rama integración / SHA probado:
Dependencias y archivos:
Cambios y motivo:
Reglas de referencia (archivo:línea):
Sprites y narrativa:
Pruebas automatizadas (comando, versión, salida, exit code, log):
Pruebas manuales (pasos, esperado, observado, captura/vídeo, dispositivo):
Teléfono (panel, mensaje, recepción, efecto real, contexto de vuelta):
Teclado/ratón sin relay:
No realizado y motivo:
Incidencias y decisiones pendientes:
Criterios de aceptación cumplidos / pendientes:
Siguiente agente y entrega:
```

### Incidencia devuelta

```markdown
ID / severidad / escena / SHA:
Síntoma:
Pasos y condiciones de reproducción:
Esperado y fuente:
Observado y evidencia:
Hipótesis / causa confirmada:
Dueño A1 / participación A2 / revisor A3:
Corrección mínima propuesta:
Dependencias y archivos compartidos:
Prueba que debe cerrar la incidencia:
Estado / SHA corregido / SHA revalidado:
```

### Reporte final humano

Debe incluir baseline, inventario cubierto, qué funcionaba y qué fallaba con evidencia, documentación desactualizada, causas confirmadas, cambios por archivo, pruebas y resultados, flujos reales, limitaciones, incidencias abiertas y decisiones humanas. Adjuntar matriz por escena, relación de ramas/commits, SHA candidato exacto, URLs remotas, PR si existe y confirmación de que no se modificó master. Separar correcciones propias de fallas preexistentes. Si algo obligatorio está bloqueado, entregar candidato con reservas explícitas, no declaración de finalización completa.

## 9. Inventario técnico del controlador y diagnóstico estático

Referencias correspondientes al baseline `b796ab8`. Una ruta presente o un handler no prueban interacción real.

### Pipeline inspeccionado

| Tramo | Implementación y evidencia |
|---|---|
| Configuración y QR | `scripts/ui/Main_menu/ControlsMenu.cs:25-51,80-146`; `scripts/systems/input/ControllerQr.cs:12-31`. Toggle, dirección, sala, estado y enlace. |
| Persistencia y precedencia | `ControllerRelay.cs:192-210,417-443`: proyecto, `user://controller.cfg`, entorno y argumentos. `project.godot:18-22` deshabilita el controlador por defecto. |
| Navegador | `controlers/index.html:253-301,360-432`: URL, WebSocket, intensidades, comandos, punteros y orientación. |
| Relay Node | `controlers/relay.js:12-51,136-325`: HTTP y WS, roles, salas, validación, presencia, sustitución y contexto almacenado. |
| Recepción Godot | `scripts/systems/input/ControllerRelay.cs:67-150,330-407`: sondeo, reconexión, liberación y publicación. `ControllerMessage.cs:52-125`: parsing. |
| Entrada y comandos | `ControllerInputBridge.cs:19-66`: acciones y E sintética; `ControllerCommand.cs:155-192`: router por receptores registrados. |
| Camino inverso | `ControllerContext.cs:48-119`: propietario, revisión y serialización; Node conserva/repite contexto; `index.html:306-356,437-654` muestra panel y restaura vista. |

La lista Node contiene `move_*`, `interact`, `training_jump`, `hotbar_1`–`hotbar_5`, `minigame_action`, `minigame_fine`, `minigame_rotate_left`, `minigame_rotate_right` y `launch_hold`. Godot ya define `interact` en InputMap, aunque el puente inyecta E para satisfacer consumidores que exigen teclas. `hotbar_next` y `hotbar_prev` siguen sin mapeo.

### Receptores y fuentes de contexto

| Consumidor | Contrato encontrado | Validación necesaria |
|---|---|---|
| `WireMinigame.cs:401-466` | `wire_pick`, `wire_connect`; contexto `wires` | Seleccionar, conectar, resolver y volver |
| `FuseBoxMinigame.cs:216-254` | `fuse_press`; `fuses` | Pulsación individual/doble, bloqueo ocupado y resolución |
| `FrequencyBoxMinigame.cs:309-362` | `freq_select`, `freq_set`; `frequency` | Selección, ajuste, similitud/progreso y resolución |
| `HullRepairMinigame.cs:1064-1128` | `hull_select`, `hull_restart` y acciones sostenidas; `hull` | Colocación, giro, soldadura, calor y cierre |
| `scenes/RocketLaunch/RocketLaunch.cs:485-531` | `launch_start`, `launch_tap`, `launch_retry`, `launch_hold`; `launch` | Ignición, mantenimiento, fallo real y repetición solo de práctica |
| `scripts/ending/Ending.cs:407-447` | `ending_skip`, `ending_pause`, `ending_continue`; `ending` | Cinemática, pausa, resultado y salida |
| `scripts/training/PracticeReturn.cs:202-281` | `minigame_start`, `minigame_cancel`; wrapper | Tickets, instrucciones, retorno/reinicio y fallo de carga |
| `scripts/mars/MinigameOverlay.cs:269-307` | `minigame_cancel`, consumo de `minigame_start`; wrapper | Cierre, recuperación de jugador y contexto |

Los seis primeros son fuentes de contexto; los dos wrappers reciben comandos y coordinan fuentes, no son paneles adicionales independientes.

Otros consumidores reales:

- `scripts/player/Player.cs:56`, `scripts/training/TrainingMovement.cs:61-67`: movimiento y salto.
- `scripts/rover/Rover.cs:115-133`, `Rover_scence.cs:251-272,361-362,616-630`: carriles, aceleración, freno y tutorial.
- `scripts/systems/inventory/Hotbar.cs:46-103`: selección de equipo.
- `scripts/training/TrainingStations.cs:64-84`, `EquipmentPanel.cs:378-382`: interacción y cierre de equipamiento.
- `scripts/mission/OxygenPickup.cs:59-63`, `scripts/mars/Interactable.cs:76-85`: oxígeno e interactuables ElectricalBox, Generator, SignalTable y DoorSwitch.
- `scripts/stages/IntroSequence.cs:93-98`, `scripts/mars/EndingTrigger.cs:197-203`: omitir introducción y cerrar mensajes con E.
- `scripts/mission/MissionState.cs:420`: lectura indirecta de movimiento.

### Tickets iniciales: evidencia estática, reproducción pendiente

| ID / prioridad | Evidencia y alcance | Experimento y responsable |
|---|---|---|
| CTRL-01 / alta | Relay procesa en pausa; router no consulta pausa; frecuencia modifica valores con guardia que solo consulta PracticeReturn (`ControllerRelay.cs:70`, `ControllerCommand.cs:179-186`, `FrequencyBoxMinigame.cs:317-359`, `PracticeReturn.cs:24-41`) | A2 reproduce comandos durante pausa y comprueba estado. Preservar excepciones deliberadas de Ending; no bloquear todo el router indiscriminadamente. |
| CTRL-02 / alta | Sin contrato completo para TAKE/STORE y +/- de equipamiento; Q del monitor; Space/Enter/clic de PreLaunch; botones de Gateway; ruta, Enter y resultados de rover | A1 entrega cada escena; A2 demuestra el bloqueo concreto y añade control mínimo para completarla con teléfono. No introducir retry de rover real. |
| CTRL-03 / alta | `index.html:242,407-430` usa umbrales distintos del README; cero relativo satisface aceleración | A2 registra comportamiento simulado y físico. Equipo humano confirma postura y neutral deseados antes de ajustar sensibilidad. |
| CTRL-04 / media | Sustitución Node cierra el teléfono anterior, que vuelve a reconectar; handler de mensajes no comprueba explícitamente socket vigente (`relay.js:245-317`, `index.html:281`) | A2 reproduce dos pestañas/teléfonos, paquetes tardíos y alternancia. Registrar tiempos y definir sesión vigente. |
| CTRL-05 / media | Caché local de acciones puede actualizarse con WS cerrado; contexto puede ser temporalmente antiguo al sustituir juego | A2 prueba corte real, retorno, recarga y transición con controles sostenidos; distinguir mitigación por contexto `none` de garantía completa. |
| CTRL-06 / media | `freq_set`: Node admite índices 0–7, C# 0–2; `freq_select` admite 0–7, consumidor usa 0–2 (`relay.js:90-114`, `ControllerCommand.cs:97-125`) | A2 prepara tabla de contrato y casos límite; resolver según consumidor actual, sin ampliar reglas por accidente. |
| CTRL-07 / media | QR pierde subruta WS; borde visual QR de tres módulos frente a cuatro del helper (`ControllerRelay.cs:272-289`, `ControlsMenu.cs:186`) | A2 prueba raíz, proxy si está dentro del alcance y escaneo real. No declarar ilegible un QR por inspección. |
| CTRL-08 / media | Entrada sintética y física comparten acciones; limpieza depende del ciclo de vida; retorno fallido ofrece reintento local tras cerrar contexto | A2 comprueba entradas simultáneas, salida antigua después de contexto nuevo y seam `SceneLoader`. |

El objetivo operativo se interpreta como **jugar las escenas con el teléfono después del emparejamiento**, preservando juego opcional con teclado. Las brechas necesarias para completar escenas forman parte del trabajo; no se excluyen mediante una pregunta de alcance. La configuración inicial en PC y la navegación accesoria se documentan para acordar expectativas concretas.

Historial relevante inspeccionado: `48e4c61` (página/Node), `9b5db67` (puente y settings), `c708d22` (minijuegos), `b509d2e` (router/contexto), `f7d1d00` (overlays Marte), `c2f6d8b` (finales/rover), `fa3c38a` y `b796ab8` (cambios recientes). La rama Vercel no acredita que esta base tenga ese despliegue.

**Límite decisivo de las pruebas existentes:** `ControllerRelay.cs:72-76,212-221` evita abrir socket en headless y escenas `res://tests/`. Incluso `ControllerInputTest` aprobado no demuestra Node → autoload Godot real. La prueba viva debe arrancar una escena de juego con ventana y controlador habilitado.

## 10. Comandos y resultados ejecutados

### Línea base observada

- `dotnet build ares.sln`: exit 0, cero warnings y cero errores; DLL en `.godot/mono/temp/bin/Debug/ares.dll`.
- En `controlers/`, `npm ci`: exit 0, una dependencia instalada, cero vulnerabilidades informadas por npm.
- En `controlers/`, `npm test`: exit 0, **13/13 pruebas aprobadas**. Son pruebas Node de relay/protocolo; no equivalen a navegador y Godot conectados.
- Se ejecutaron las 16 escenas `tests/*.tscn`, secuencialmente, con timeout de 90 segundos por escena y logs separados.
- Primera pasada: 13 escenas emitieron PASS y exit 0; `AudioFeedbackTest`, `ProjectConsistencyTest` y `RocketLaunchTest` terminaron con exit 1. Todas reportaron recursos `.godot/imported/*.sample` ausentes para seis audios CC0.
- Se ejecutó `Godot --headless --editor --path . --import`: exit 0; importó los seis audios. También emitió tres errores `Condition "det == 0" is true.` en `affine_invert`. Esos errores no se consideran resueltos ni se ocultan por el exit 0.
- Se repitieron las 16 escenas tras importar. Todas emitieron su marcador PASS y exit 0. Los tres fallos iniciales desaparecieron sin cambiar código versionado; esto respalda un problema de preparación de recursos en aquella ejecución, no una corrección del juego.

| Escena en `tests/` | Aserciones del PASS posterior a importación | Observaciones |
|---|---:|---|
| AudioFeedbackTest.tscn | 41 | Objetos y recursos retenidos al salir |
| ControllerInputTest.tscn | 211 | Sin warnings/errors en el log observado |
| DevCheatsTest.tscn | 10 | CanvasItem y ObjectDB retenidos |
| EndingSequenceTest.tscn | 25 | Objetos y recursos retenidos al salir |
| GatewayMissionTest.tscn | 95 | Objetos y recursos retenidos al salir |
| GatewayWallsTest.tscn | 26 | Objetos y recursos retenidos al salir |
| IntroSequenceTest.tscn | 28 | Objetos y recursos retenidos al salir |
| LoadoutTest.tscn | 88 | Objetos y recursos retenidos al salir |
| MissionMonitorTest.tscn | 10 | Sin warnings/errors en el log observado |
| MissionOutcomeTest.tscn | 12 | ObjectDB retenido |
| OxygenSystemTest.tscn | 48 | Objetos y recursos retenidos al salir |
| PreLaunchTest.tscn | 5 | Sin warnings/errors en el log observado |
| ProjectConsistencyTest.tscn | 38 | Sin warnings/errors en el log observado |
| RocketLaunchTest.tscn | 130 | Objetos y recursos retenidos al salir |
| StaminaSystemTest.tscn | 5 | Sin warnings/errors en el log observado |
| TrainingPracticeTest.tscn | 320 | Excepción adicional antes del PASS; aceptación con incidencia abierta |

`TrainingPracticeTest` emitió `System.InvalidOperationException: Handle is not initialized.`. La traza pasa por `ScriptManagerBridge.SwapGCHandleForType`, `AnimatedSprite2D.SpriteFrames` y `scripts/player/PlayerAnimation.cs:27`. Se registra como **BASE-01**, causa y reproducibilidad pendientes; no se atribuye automáticamente al motor ni a sprites. El PASS del arnés no elimina esta excepción.

**BASE-02:** investigar errores `affine_invert` de importación. **BASE-03:** clasificar retenciones al salir con logs verbose si afectan aceptación. No confundir estos mensajes con los fallos iniciales de recursos ausentes.

Logs de la sesión:

```text
/private/var/folders/vk/nzpvj3_57n9c242q8j95v40c0000gn/T/opencode/ares-plan-baseline/
  results.json
  <Test>.log
  imported/results.json
  imported/<Test>.log
```

Son artefactos locales temporales. Antes de una entrega humana remota se deben adjuntar o copiar a su sistema de reportes, sin depender de que estas rutas existan en otra máquina.

### Ejecución reproducible durante el trabajo

Desde la raíz, usando el binario .NET disponible en esta máquina:

```sh
GODOT=/Applications/Godot_mono.app/Contents/MacOS/Godot
"$GODOT" --version
"$GODOT" --headless --editor --path . --import
dotnet build ares.sln
"$GODOT" --headless --path . res://tests/ControllerInputTest.tscn
"$GODOT" --headless --path . res://tests/TrainingPracticeTest.tscn
"$GODOT" --headless --path . res://tests/RocketLaunchTest.tscn
```

En cada worktree nuevo, preparar recursos antes de interpretar fallos de gameplay. `dotnet build --no-restore` es válido cuando ya existe restore correcto. No usar `dotnet run` ni `dotnet test`: este repositorio usa arneses Godot, no xUnit/NUnit. `tests/LaunchHoldModelTest.cs` se ejecuta dentro de RocketLaunchTest, no como escena.

Para la regresión completa, ejecutar cada nombre de la tabla con:

```sh
"$GODOT" --headless --path . res://tests/<nombre>.tscn
```

Registrar exit code, PASS y errores; no aceptar exclusivamente por código cero. Los afectados se ejecutan por entrega; A3 ejecuta la matriz completa sobre el candidato final. No repetir suites ya aprobadas sin nuevos cambios o una duda concreta.

En una terminal cuyo directorio sea `controlers/`:

```sh
npm ci
npm test
npm start
```

El servidor documentado escucha en 8000; `HOST` y `PORT` permiten aislamiento. En otra terminal desde la raíz:

```sh
"$GODOT" --path .
```

También se puede abrir `project.godot` en Godot 4.7 .NET y pulsar F5. Habilitar teléfono en Options → Controls & controller, aplicar dirección accesible y emparejar. `localhost` del teléfono no es el ordenador: usar IP LAN para pad o un endpoint HTTPS/WSS para sensores. El README propone `ngrok http 8000`; el túnel y su disponibilidad deben comprobarse, no se presupone despliegue.

Capturas del arnés de lanzamiento, cuando se ejecute esa validación visual:

```sh
"$GODOT" --path . res://tests/RocketLaunchTest.tscn -- --visual
```

El flag va después de `--`; el arnés guarda PNG en `/tmp/opencode/`. Archivar por SHA antes de otra ejecución.

### No realizado en esta planificación

No se ejecutó campaña manual completa, navegador automatizado, relay conectado al cliente Godot real, escaneo QR, pruebas físicas iOS/Android ni sensores por HTTPS. Tampoco se implementaron correcciones, se publicaron ramas o se abrió PR. La compilación y los PASS anteriores no certifican esas capacidades.

## 11. Riesgos, bloqueos y decisiones que requieren evidencia

| Riesgo o bloqueo | Acción antes de aceptar | Responsable |
|---|---|---|
| Excepción Training y errores de importación pese a exit 0 | Reproducir aisladamente, clasificar causa y adjuntar diagnóstico; no declarar baseline limpio | A1 con revisión A3 |
| Reglas descritas en README divergen del código | Registrar discrepancia; corregir instrucciones según implementación validada o elevar conflicto real de producto | A1 |
| Componentes reutilizados afectan varias escenas | Un solo propietario, checkpoint común y regresión de todos los anfitriones | A3 coordina A1/A2 |
| Rama obligatoria ya existente al empezar | Inspeccionar y acordar continuidad sin sobrescribir trabajo | A2/A3 |
| `origin/master` cambia durante el trabajo | No mover BASE silenciosamente; A3 integra actualización en checkpoint controlado y repite afectados | A3 |
| Cachés, salas, puertos, user data o capturas compartidos | Separar recursos o serializar ejecución; registrar configuración | Todos, A3 coordina |
| Dispositivo físico/HTTPS no disponible | Marcar sensores, permiso y escaneo pendientes; no certificar funcionamiento completo | A2 y equipo humano |
| Regresión por mover nodos o perder UID | Comparar referencias y cargar escenas reales; preservar sidecars y rutas | A1/A3 |
| Interfaces de confirmación no accesibles desde teléfono | Añadir controles mínimos tras reproducir; cerrar campaña sin teclado tras emparejar | A2 |
| Audio previo con procedencia parcial | No añadir ni transformar recursos sin licencia válida; mantener limitación documentada | A1/A3 |
| Publicación sin credenciales | Reportar bloqueo de push/PR y conservar commits; nunca marcar misión técnica publicada | A2/A3 |

Preguntas humanas que no se pueden responder mediante inspección:

1. ¿Qué postura neutral y sensibilidad se esperan del volante? El comportamiento actual y la documentación discrepan.
2. ¿Qué dispositivos físicos y navegadores puede aportar el equipo para la aceptación? Se propone al menos Safari iOS y Chrome Android.
3. ¿El despliegue debe soportar proxy por subruta o alojamiento separado, además del Node en raíz?
4. ¿Qué dirección artística define equivalencia de sprites entre Training y Gateway cuando representan objetos distintos? A1 propone primero equivalencias semánticas y no rediseña funciones por semejanza visual.
5. ¿Se confirma `review/ares-multiagent-integration` como nombre de la rama final? Es una propuesta, no una decisión previa.

Estas preguntas no bloquean inspección, reproducción ni las correcciones técnicas independientes. La ausencia de una decisión de dispositivo sí bloquea certificar esa plataforma. Las decisiones de protocolo, serialización, ownership, pausa y sustitución de conexiones corresponden a la auditoría técnica con evidencia, no se trasladan innecesariamente al usuario.

### Protocolo de auditoría entre los tres agentes

Para cada desacuerdo, crear un archivo temporal `audit-<ID>.md` con problema, alternativas, argumentos A1/A2/A3, evidencia, efectos sobre reglas y propuesta. Los tres aportan su posición; A3 registra la resolución técnica, condiciones, dueño y pruebas necesarias. Si la evidencia no permite decidir, A3 ordena el experimento o marca bloqueo. No transforma una incertidumbre en hecho.

Conservar en el reporte de escena/final un resumen duradero de la resolución y los SHAs asociados. Los archivos temporales son deliberación, no el único registro de decisiones. Una regla nueva, preferencia artística o expectativa de producto sigue requiriendo decisión humana; el arbitraje A3 no otorga permiso para inventarla.

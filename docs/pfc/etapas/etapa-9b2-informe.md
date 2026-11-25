# Etapa 9b-2 (caso a) — Sustitutos de ejercicios con un modelo de lenguaje local

Rama `cierre-pfc`. Commit de código: `8b7fcf0`. Evidencia: `docs/pfc/evidencia/etapa9b2-*.txt`. Sin migración.

> **PUNTO DE ESPERA 2.** La elección del modelo de generación depende de la prueba de hardware en el equipo del autor (`GymBro.IA/evaluacion/prueba_hardware_ollama.py`, Etapa 9b-1), que no se ejecutó porque Ollama no estaba en ejecución. El modelo queda configurable (`OLLAMA_MODELO_GENERACION`, sin valor por defecto). **No hay cifras de la medición** (porcentaje de salidas que respetan el conjunto y latencia): se obtienen con `GymBro.IA/evaluacion/medir_sustitutos.py` cuando exista el modelo elegido.
>
> **Parte detenida (regla 8 del pedido).** "Aplicar una sugerencia reemplaza el ejercicio de la línea, deja auditoría y pasa la rutina a `Editada`" choca con el modelo de revisión actual. Se implementó la sugerencia; la aplicación queda como propuesta (sección "Propuesta: aplicar una sugerencia").

## Alcance implementado

| Pedido | Estado | Dónde |
|---|---|---|
| Endpoint en `GymBro.IA` que recibe ejercicio, motivo y candidatos ya recuperados y pide al modelo elegir y justificar solo entre ellos, en JSON | Implementado; verificado con Ollama reemplazado por un doble | `GymBro.IA/sustitutos.py` (`POST /sugerir-sustitutos/`) |
| Modelo por configuración (`OLLAMA_MODELO_GENERACION`) | Implementado | idem; sin modelo configurado responde 503 |
| Verificación en .NET, por identificador y en código, de que cada elegido pertenece al conjunto recuperado; si no, se descarta la salida | Implementado y verificado | `SugerenciaSustitutosService` |
| Cada sustitución propuesta pasa por `IValidadorPrescripcionService` | Implementado y verificado | idem |
| Sugerencia visible al Personal Trainer o al Dueño en la pantalla de revisión, nunca al Socio | Implementado y verificado | `features/revisiones/sustitutos-panel.ts`; endpoint bajo `RevisionesController` (`PersonalGimnasio`) |
| Aplicar la sugerencia (reemplazo, auditoría, rutina `Editada`) | **Especificado, pero no implementado** (detenido; ver propuesta) | — |
| No se permite sobre líneas del protocolo `piernas-1.0` | Implementado y verificado (para la sugerencia) | idem (400) |
| Ollama o modelo no disponibles: respuesta clara y sin efectos | Implementado y verificado | 503 con mensaje propio |
| Medición: % de salidas que respetan el conjunto y latencia, de una ejecución guardada | Script implementado; **no ejecutado** (punto de espera 2) | `GymBro.IA/evaluacion/medir_sustitutos.py`; además, cada sugerencia queda en la auditoría con esos dos datos |
| Pruebas con generador falso (id inventado, salida mal formada, candidato no válido), autorización y auditoría | Implementado y verificado | `SugerenciaSustitutosTests`, `SugerenciaSustitutosInterpretarTests`, `test_sustitutos.py`, matriz |

## Texto propuesto para 4.5 (implementación)

La sugerencia de sustitutos combina recuperación y generación con verificación en código. Cuando el Dueño o el Personal Trainer asignado piden sustitutos para una línea de una rutina pendiente de revisión e indican el motivo, la API recupera hasta diez candidatos con la búsqueda semántica de la Etapa 9b-1 (filtros duros: activos, globales o del gimnasio del socio, con equipamiento disponible, de la misma zona), excluye el ejercicio actual y los que ya están en la rutina, y envía al microservicio de IA el ejercicio, el motivo y esa lista. El microservicio arma un pedido que lista los candidatos por identificador, prohíbe inventar ejercicios, exige una respuesta JSON con la forma `{"elegidos": [{"id": ..., "justificacion": ...}]}` y llama al modelo local configurado con temperatura 0; devuelve el texto tal cual, sin validarlo.

La API interpreta la respuesta de forma estricta: si no es JSON con esa forma, o si algún identificador no es un GUID, la salida se descarta completa ("salida mal formada"); si algún identificador elegido no pertenece al conjunto recuperado, aunque sea un ejercicio que existe, también se descarta completa ("candidato fuera del conjunto"). Cada candidato aceptado se valida con `IValidadorPrescripcionService` sobre la rutina que resultaría de la sustitución (por ejemplo, un ejercicio por tiempo puede dejar la línea fuera del rango de segundos); los que violan límites se informan aparte y no se sugieren. El resultado es una sugerencia que se muestra en la pantalla de revisión con la aclaración de que la generó un modelo local y no se aplica sola; no modifica la rutina y no tiene ningún camino hacia el Socio. Cada pedido queda en la auditoría con el modelo, la cantidad de candidatos y de sugerencias, si respetó el conjunto, el motivo del descarte y la latencia, sin el texto del motivo (puede contener datos de salud). No se permite sobre líneas del protocolo de piernas, porque rompería su lista cerrada de ejercicios y su trazabilidad. Si el microservicio, Ollama o el modelo no responden, la API devuelve 503 con un mensaje propio y no produce efectos.

## Decisiones técnicas

- **D-9b2-1. El microservicio no valida; valida la API.** Un único lugar con la regla de pertenencia y con acceso al catálogo y a los límites. El microservicio devuelve el texto crudo y la latencia.
- **D-9b2-2. Descarte completo** ante un solo id fuera del conjunto (pedido de la etapa): una salida que inventa un candidato no es confiable en ninguna de sus partes.
- **D-9b2-3. Solo rutinas `PendienteRevision`.** La sugerencia se muestra en la pantalla de revisión, que solo lista rutinas pendientes; el acceso sigue las reglas de la revisión (Dueño del gimnasio o Personal Trainer asignado; la plataforma no, porque la rutina es dato de salud).
- **D-9b2-4. Sin modelo por defecto.** El pedido deja la elección al punto de espera 2; un valor por defecto escondería esa decisión.
- **D-9b2-5. Tiempo máximo de la generación** configurable (`Ia:TiempoMaximoGeneracionSegundos`, 60 s por defecto), separado del de los embeddings.
- **D-9b2-6. Medición sin inventar.** El script de medición guarda una salida JSON por ejecución; además, la auditoría registra respeto del conjunto y latencia de cada uso real.

## Propuesta: aplicar una sugerencia (parte detenida)

**Choque con el modelo aprobado.** Hoy una rutina solo cambia de estado con `RevisionPrescripcionService.RevisarAsync`, que exige que esté `PendienteRevision` y toma la decisión completa (`Aprobada`, `Editada` o `Rechazada`) en un solo acto; los ajustes de esa decisión cambian series, repeticiones, RIR y descanso, pero no el ejercicio. No existe edición de una rutina ya aprobada. Un endpoint que reemplace el ejercicio y pase la rutina a `Editada` por sí solo aprobaría toda la rutina sin una decisión explícita del revisor (si estaba pendiente) o editaría una rutina vigente (si estaba aprobada), cosa que el modelo no admite.

**Opciones (requieren autorización del desarrollador y de Claude Web):**

1. **(Recomendada) La sustitución como un ajuste más de la decisión `Editada`.** Agregar a `AjusteEjercicioDto` un campo opcional `EjercicioId` (cambio aditivo de contrato). `RevisarAsync` lo aplica solo con la decisión `Editada`, después de verificar que el ejercicio es visible, activo, con equipamiento disponible, que la línea no es del protocolo y que la rutina resultante pasa el validador; marca `EsModificada` y la auditoría `REVISION_RUTINA` registra la sustitución. En la pantalla, el botón "Usar este sustituto" pone la tarjeta en modo edición con el ejercicio cambiado y el revisor confirma con "Guardar cambios". Respeta el modelo actual: un solo acto de revisión, humano y auditado.
2. Un endpoint propio que reemplace el ejercicio sin cambiar el estado (la rutina sigue `PendienteRevision`) y deje la decisión para después. No cumple "pasa la rutina a `Editada`" y obliga a una segunda acción.
3. Permitir editar rutinas vigentes (`Aprobada` → `Editada`). Cambia el modelo de estados aprobado; no se recomienda.

## Texto propuesto para 4.6 (verificación)

Pruebas de integración (`GymBro.Tests.Integration/SugerenciaSustitutosTests.cs`, contra PostgreSQL + pgvector descartable, con la IA de embeddings y el generador reemplazados por dobles): el caso válido devuelve solo candidatos recuperados, sin el ejercicio actual, sin los de la rutina y sin el de otro gimnasio, con modelo y latencia, sin modificar la rutina, y deja la auditoría con "respeta_conjunto=si" y sin el texto del motivo; un id inventado y un ejercicio existente que no fue recuperado descartan toda la salida; cinco salidas mal formadas se descartan; un candidato por tiempo que dejaría la línea en 3 a 5 segundos se rechaza por `LIM-SEGUNDOS` y el otro se sugiere; una línea del protocolo de piernas responde 400; con el modelo caído responde 503 sin efectos; autorización (Socio y plataforma 403, Personal Trainer no asignado 404, asignado 200, sin motivo 400, rutina inexistente 404). La matriz de autorización suma el endpoint para los cinco perfiles. Pruebas unitarias de la interpretación estricta (`SugerenciaSustitutosInterpretarTests`, 12). Pruebas de Python (`GymBro.IA/test_sustitutos.py`, 6): el pedido lista solo los candidatos recibidos, exige JSON y temperatura 0; sin modelo configurado o con Ollama caído responde 503; sin candidatos o sin motivo responde 422; la utilidad de medición detecta ids fuera del conjunto. Pruebas del cliente (`sustitutos-panel.spec.ts`, 3).

## Cifras de la verificación

Sobre el commit `8b7fcf0` (`etapa9b2-*.txt`):

| Verificación | Etapa anterior (9b-1) | Etapa 9b-2 |
|---|---|---|
| `dotnet build` | 0 errores, advertencia previa CS2008 | igual |
| Pruebas unitarias | 370 | 382 (+12) |
| Pruebas de integración | 558 | 575 (+17: sustitutos y celdas nuevas de la matriz) |
| `ng build` / pruebas del cliente | correcto / 250 | correcto / 253 (+3) |
| Pruebas de Python | 10 | 16 (+6 del endpoint de sustitutos) |

## Desvíos y limitaciones

- La aplicación de la sugerencia no se implementó (parte detenida; ver propuesta).
- No se midió con un modelo real (punto de espera 2). La calidad de las justificaciones no está evaluada.
- La pantalla de revisión no distingue las líneas del protocolo por línea (limitación L1 de la 7d): el panel se oculta para toda rutina con origen `PROTOCOLO_PIERNAS_DETERMINISTA`, y la API rechaza igualmente cualquier línea del protocolo.

## Pendientes

- **PUNTO DE ESPERA 2:** ejecutar la prueba de hardware, elegir el modelo, configurar `OLLAMA_MODELO_GENERACION` en el entorno de `GymBro.IA` y ejecutar `medir_sustitutos.py` (con `GYMBRO_API_TOKEN`) para obtener el porcentaje de salidas que respetan el conjunto y la latencia.
- Decisión sobre la propuesta de aplicación (opción 1 recomendada).

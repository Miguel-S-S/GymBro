# Etapa 9a-1 — Core, tipo de medida y reclasificación de `Abs`: texto para el informe y registro de decisiones

**Fecha:** 17/11/2026. **Rama:** `etapa9a1` (sin merge a `main` hasta la revisión de Claude Web).
**Evidencia:** `docs/pfc/evidencia/etapa9a1-*.txt`:
- estado previo de la base local;
- SQL de la migración y filas afectadas;
- embeddings de los ejercicios de Core (`etapa9a1-embeddings.txt`, agregada después del merge);
- build y pruebas de .NET y Angular, con fecha y commit.

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas; embeddings de los ejercicios nuevos generados y verificados en la base local (17/11/2026). Queda pendiente la prueba manual en pantalla.**

El catálogo de ejercicios del dataset no tenía ejercicios de la zona Core, por lo que la rutina baseline omitía siempre esa zona y quedaba con cuatro ejercicios. Se incorporó un catálogo propio del proyecto con ocho ejercicios de Core en español:

- plancha;
- crunch abdominal;
- elevación de piernas;
- bicicleta;
- mountain climber;
- tijera de piernas;
- bote;
- crunch en polea.

Los carga un seeder propio (`CatalogoProyectoSeeder`), independiente de los archivos CSV, que se ejecuta al arrancar en todos los entornos. Es idempotente: inserta solo lo que falta, reconociendo cada fila por un identificador fijo, y nunca sobrescribe ni reactiva un ejercicio editado o dado de baja.

Algunos ejercicios se miden en tiempo y no en repeticiones, como la plancha. Para representarlo se agregó un **tipo de medida** (`Repeticiones` o `Segundos`) al ejercicio y a la dosificación, y un **origen** (`Dataset`, `Proyecto` o `Gimnasio`, este último reservado para la Etapa 9a-2) al ejercicio. Ambos se guardan como texto, con una restricción `CHECK` en la base y la guarda que impide persistir valores no definidos. No se renombraron columnas: cuando el ejercicio se mide en segundos, los campos de "repeticiones" de la prescripción, la dosificación y la serie guardan segundos, y el cliente muestra la unidad según el tipo de medida.

La rutina baseline elige ahora la dosis según la zona **y** el tipo de medida del ejercicio elegido. Si no hay una dosis válida para ese tipo, usa una dosis por defecto (3 × 8–12 repeticiones o 3 × 20–40 segundos) y lo deja asentado en las observaciones. Al calcular el volumen de una sesión, los ejercicios por tiempo aportan 0 kg, porque no se multiplica el peso por los segundos. La comparación entre lo prescripto y lo realizado sigue igual, pero el mensaje usa la unidad correcta.

La migración `Etapa9a1_CatalogoCore` corrigió una anomalía de la dosificación del dataset. Las filas de abdominales (`Abs`) figuraban en la zona Piernas; pasaron a Core. Una de ellas, la plancha con 30–60 series de 1 repetición, se dio de baja lógica. Por eso se eliminó la exclusión que la búsqueda de la dosis de Piernas tenía para esas filas.

En la PWA, las pantallas de entrenamiento, registro y detalle de sesión, historial y revisión de la rutina muestran "20–40 s" y la etiqueta "Segundos" cuando corresponde. No se agregó un cronómetro.

**Advertencia:**
- Los ocho ejercicios, sus textos, la dificultad asignada y los rangos de dosificación son **borradores provisionales, no validados por un profesional**. Son decisiones de diseño, no criterios clínicos.
- Antes de usarlos con personas reales debe revisarlos un Personal Trainer. A la fecha de este informe, esa revisión no se hizo.
- Seis de los ocho ejercicios coinciden con el grupo "Core body" del estudio de `Abstract.pdf` (PMC9002367). Eso respalda que son habituales, no que la selección esté validada.

## Texto propuesto para 4.6 (verificación)

| Criterio (sección 6) | Resultado | Verificación |
|---|---|---|
| El baseline genera 5 ejercicios con las 5 zonas | Implementado y verificado | Unitaria (`ConLasCincoZonasEnElCatalogo_…`) e integración (`CatalogoCoreTests.BaselineConCincoZonas`) |
| Core ya no figura como zona omitida | Implementado y verificado | Integración: las observaciones no contienen "omitidas" |
| La dosis de un ejercicio por segundos sale de la fila de segundos, y la de uno por repeticiones, de la de repeticiones | Implementado y verificado | Unitarias de `PrescripcionBaselineService` (dos casos) |
| Sin fila de dosis para el tipo, usa la dosis por defecto del tipo y lo deja en las observaciones | Implementado y verificado | Unitarias (segundos: 3 × 20–40 s; repeticiones: 3 × 8–12) |
| Piernas no depende de la exclusión por `abs` ni se contamina | Implementado y verificado | Integración sobre la migración con filas `Abs` y `Legs` (`MigracionCatalogoCoreTests`) |
| Las filas de abdominales quedan en Core y la 30–60 × 1 queda dada de baja | Implementado y verificado | Integración con migración hacia atrás y hacia adelante; en `gymbro-db`, 1 fila dada de baja y 6 reclasificadas |
| El seeder es idempotente y no pisa una edición | Implementado y verificado | Integración: dos arranques con las mismas filas; una edición y una baja se conservan |
| El seeder corre en Production aunque `Datasets:SembrarAlIniciar` sea falso | Implementado y verificado | `SiembraDatasetsTests.EnProduccion` |
| (a) Base vacía, Core y después datasets: el dataset se carga igual | Implementado y verificado | `SiembraDatasetsTests.CoreAntesQueLosDatasets` (CSV sintéticos) |
| (b) Con los datasets cargados, el seeder de Core no duplica ni pisa nada | Implementado y verificado | `SiembraDatasetsTests.ConClaveVerdadera` |
| (c) Dos arranques dejan las mismas filas | Implementado y verificado | `SiembraDatasetsTests` (b) y `CatalogoCoreTests` |
| (d) En Production sin datasets, el baseline usa la dosis de Core del seeder | Implementado y verificado | `CatalogoCoreTests.BaselineEnProduccionSinDatasets`: 3 × 10–15, sin la observación de dosis por defecto |
| Ningún enum nuevo admite un valor no definido | Implementado y verificado | Guarda (`ReglaNegocioException`) y `CHECK` de la base (`CatalogoCoreTests`) |
| El volumen de un ejercicio por segundos es 0 | Implementado y verificado | Unitaria de `SesionEntrenamientoService` |
| `GET /api/Ejercicios/zonas` incluye Core; el catálogo expone `tipoMedida` y `origen` | Implementado y verificado | Integración con el Administrador de la Plataforma |
| La matriz de autorización no cambia de roles | Implementado y verificado | `MatrizAutorizacionTests` sin modificaciones, en verde |
| La PWA muestra segundos para plancha y bote, y repeticiones para el resto | Parcial | Specs de componente en verde; **prueba manual en pantalla, pendiente** |
| Build y pruebas sin errores nuevos | Implementado y verificado | 111 unitarias (antes 103), 222 de integración (antes 201) y 147 del cliente (antes 135); 0 errores |

## Registro de decisiones de la etapa

| # | Decisión | Motivo | Estado |
|---|---|---|---|
| T1 | En un ejercicio por segundos, los campos de repeticiones (prescripción, dosificación y serie) guardan segundos; no se renombran columnas | Especificación (sección 4.1); el cliente muestra la unidad según `tipoMedida` | Aprobada (especificación) |
| T2 | `CatalogoProyectoSeeder` se ejecuta después de `DataSeeder`. `DataSeeder` decide si carga mirando solo los ejercicios con `Origen = Dataset` y las dosificaciones cuyo Id no es del proyecto | Si no, en una base nueva el seeder de Core habría bloqueado la carga de los datasets | Aprobada (Claude Web, decisión A, 17/11/2026) |
| T3 | Las dosificaciones del proyecto se reconocen por su lista de Guid fijos; no se agregó una columna de origen | Decisión A | Aprobada (Claude Web) |
| T4 | Se suman `mi-historial.ts`, `sesion-detalle.ts`, `rutina-formato.ts` y `rutina-ejercicio-card.ts`; `TipoMedida` de la comparación sale del ejercicio | Decisión B | Aprobada (Claude Web) |
| T5 | Dosis de Core del seeder: 3–4 × 10–15 repeticiones y 2–3 × 20–40 s (subzona "General") | Propuesta de diseño de la especificación, no criterio clínico | Provisional |
| T6 | Dosis por defecto para segundos: 3 × 20–40 s | Especificación (sección 4.2) | Provisional |
| T7 | Al buscar la dosis de Core por repeticiones, la fila `Abs` reclasificada precede a la `Core` del seeder (orden parte, subzona, id: "Abs" < "Core"). Ambas son 3–4 × 10–15, así que el resultado es el mismo | Orden determinista existente | Aceptada (Claude Web) |
| T8 | El seeder no reinserta ni reactiva un ejercicio del proyecto dado de baja (busca también entre las bajas) | "Nunca sobrescribe lo que el Dueño o el autor hayan editado" | Decisión de implementación |
| T9 | El `Down` de la migración devuelve las filas `Abs` a `Legs`, reactiva la plancha 30–60 × 1 y elimina las filas de Core del proyecto. Conserva los ejercicios del proyecto que ya estén usados en prescripciones o sesiones | Reversibilidad sin romper claves foráneas | Decisión de implementación |
| T10 | El mensaje de validación de Entrenar dice "Indicá los segundos (1 o más)" para ejercicios por tiempo | Pantalla adicional encontrada al revisar (ver Diferencias) | Decisión de implementación |

## Diferencias y pendientes informados

- **Pantallas y archivos adicionales.**
  - `entrenamiento.ts`: el mensaje de validación de una serie decía "repeticiones" también para la plancha; ahora usa la unidad del ejercicio.
  - `EjercicioRepository`: proyecta el ejercicio campo por campo para no traer el embedding; hubo que agregarle `TipoMedida` y `Origen`. Sin eso, el catálogo devolvía `Origen = Dataset` para los ejercicios de Core; una prueba de integración lo detectó.
- **Pruebas existentes ajustadas.** Solo `SiembraDatasetsTests`. Esperaba 0 dosificaciones en Production y ahora existen las 2 de Core. Los conteos "del dataset" ahora excluyen las filas del proyecto, de modo que se sigue verificando lo mismo. Se agregaron un CSV sintético de ejercicios y los casos (a) y (b). Ninguna otra prueba se modificó.
- **Dosis de Piernas.** Con el orden determinista, la dosis de Piernas que toma el baseline en `gymbro-db` es Legs/Calves (3 × 15–20). Ya era así antes de esta etapa, porque la exclusión de `Abs` dejaba primero a Calves. No se cambió; queda informado para la Etapa 10.
- **Embeddings.** El seeder inserta los 8 ejercicios de Core con `embedding = NULL`; la búsqueda semántica no los encuentra hasta ejecutar `GymBro.IA/generador_embeddings.py`, que procesa los ejercicios sin embedding.
  - **Ejecutado el 17/11/2026, después del merge** (evidencia `etapa9a1-embeddings.txt`). El autor lo ejecutó en su terminal con la contraseña de la base definida solo para esa ejecución; la contraseña no figura en la evidencia.
  - Resultado en `gymbro-db`: los 8 ejercicios de Core tienen un vector de 768 dimensiones (`nomic-embed-text`), y ningún ejercicio del catálogo queda sin embedding (688 del dataset y 8 del proyecto). Ollama registró 8 solicitudes del script, una por ejercicio.
  - Control: la consulta "ejercicio para fortalecer los abdominales en el suelo", ordenada por distancia coseno, devuelve primero seis ejercicios de Core. Es una consulta suelta por SQL; no se probó el endpoint `busqueda-inteligente` ni se evaluó la calidad de la búsqueda.
  - Nota operativa: si se vuelve a sembrar una base, hay que ejecutar de nuevo el script; el embedding no se genera al arrancar.
- **Base local (`gymbro-db`).** La migración se aplicó con su script SQL, en una transacción: 1 fila dada de baja y 6 reclasificadas a Core (evidencia `etapa9a1-migracion.sql.txt`). Los ejercicios y las dosificaciones de Core se insertaron en el arranque posterior al merge (17/11/2026): el seeder informó 8 ejercicios y 2 dosificaciones, verificados por consulta (`etapa9a1-embeddings.txt`).
- **Build transitorio.** La primera `dotnet build` de la línea base informó 6 errores sin detalle guardado; al repetirla, 0. No volvió a ocurrir en esta etapa, y la causa no está verificada.
- **Sin verificar:**
  - la prueba manual en pantalla y en un celular (plancha y bote en segundos, el resto en repeticiones);
  - la revisión de los ejercicios y las dosis de Core por un Personal Trainer.

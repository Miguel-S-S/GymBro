# Etapa 8d — Indicadores de cumplimiento del entrenamiento: texto para el informe y registro de decisiones

**Fecha:** 25/11/2026. **Nombre de la etapa:** propuesto por Claude Web, **pendiente de confirmación** del autor.
**Rama:** `etapa8d`, creada desde `etapa7d` en `08e5dc2` y publicada en `origin/etapa8d`. **Sin merge:** la integración la decide el autor después de la revisión de Claude Web.
**Commits:** código y pruebas en `a17a7f2`; evidencia sobre ese commit y este informe en el commit siguiente.
**Evidencia:** `docs/pfc/evidencia/etapa8d-*.txt`. Se generó sobre el commit `a17a7f2`, sin cambios en archivos de código; cada encabezado lista los archivos del commit respecto de `08e5dc2`.
- `etapa8d-dotnet-build.txt`, `etapa8d-dotnet-test.txt`, `etapa8d-ng-build.txt`, `etapa8d-ng-test.txt`: build y pruebas;
- `etapa8d-regresion-ng.txt`: las pruebas del detalle corridas sin la Etapa 8d (fallan) y con ella (pasan);
- `etapa8d-criterio1-sesion-real.txt`: datos reales de la sesión del 25/11/2026 a las 11:50 (Prensa de piernas) e indicadores calculados con la regla nueva.

Sin migración y sin cambios en las reglas de acceso.

## Decisión que guía la etapa (aprobada por el desarrollador, 25/11/2026)

En el detalle de una sesión del Socio, la etiqueta única "Cumple objetivo" se reemplaza por **dos indicadores independientes por ejercicio, sin veredicto global**:
1. **Series:** "Series: N de M". N son las series efectivas registradas (sin calentamiento) y M las del objetivo. Más series que el objetivo no es incumplimiento.
2. **Repeticiones:** "Repeticiones en rango: K de N series". K son las series efectivas con `RepeticionesMinimas <= reps <= RepeticionesMaximas`, extremos incluidos.
3. **RIR:** se sigue mostrando por serie y no participa en ningún indicador.

Caso que originó la decisión: la Prensa de piernas del 25/11/2026 a las 11:50 (objetivo 3 × 8–12; series 58×8, 59×8 y 75×5) figuraba como "Cumple objetivo" con la tercera serie marcada.

## Inventario de usos de la regla anterior (antes de modificarla)

| # | Capa | Ubicación | Uso | Qué se hizo |
|---|---|---|---|---|
| 1 | Aplicación | `GymBro.Application/Services/SesionEntrenamientoService.cs`, `GetComparacionAsync` (líneas 300 a 312 en la base) | Calcula `ComparacionSeriesDto`: `Prescritas`, `RealizadasEfectivas`, `Diferencia` y `Mensaje` ("Cumple objetivo" si son iguales; "Faltaron N series" o "Excedieron N series" si no). | Sin cambio de lógica. `Mensaje` se sigue llenando, entre `#pragma warning disable/restore CS0618`. |
| 2 | Aplicación | mismo método, líneas 269 a 293 | Por serie: `RepeticionesComparacion.CumpleObjetivo` (rango con extremos incluidos) y `RIRComparacion.CumpleObjetivo` (RIR exacto). | Sin cambio. Son los íconos por serie, que la decisión mantiene. |
| 3 | DTO (contrato) | `GymBro.Application/DTOs/Comparacion/ComparacionSesionResponseDto.cs`, `ComparacionSeriesDto.Mensaje` | El veredicto único que devuelve la API. | **Se mantiene en el contrato y se marca `[Obsolete]`**, con un comentario XML. El JSON no cambia. |
| 4 | DTO (contrato) | mismo archivo, `ComparacionMetricaDto.CumpleObjetivo` | Resultado por serie (punto 2). | Sin cambio: no es el veredicto global. |
| 5 | API | `GET /api/Sesiones/{id}/comparacion` (`SesionesController`) | Expone la comparación. | Sin cambio. |
| 6 | Cliente (modelo) | `GymBro.Web/src/app/core/models/sesion.models.ts`, `comparacion.series.mensaje` | Tipo del campo. | Marcado `@deprecated` en JSDoc. |
| 7 | Cliente (Socio) | `GymBro.Web/src/app/features/mi/mi-historial.ts`, detalle `MiSesionDetalle` (línea 99 en la base) | Mostraba `e.comparacion.series.mensaje` en verde si `diferencia === 0` y en rojo si no. | **Reemplazado** por los dos indicadores. |
| 8 | Cliente (Dueño y Personal Trainer) | `GymBro.Web/src/app/features/sesiones/sesion-detalle.ts`, línea 60 (ruta `/sesiones/:id`, solo `AdministradorGimnasio` y `Entrenador`) | Muestra `ej.comparacion.series.mensaje`; las líneas 89, 96 y 123 muestran los íconos por serie de repeticiones y de RIR. | **Sin cambio** (fuera del alcance "detalle del Socio"). Sigue mostrando "Cumple objetivo" o "Excedieron N series". Ver pendientes. |
| 9 | Pruebas | `GymBro.Tests/SesionEntrenamientoServiceTests.cs` (líneas 178 a 197): comparación por serie; `GymBro.Tests.Integration/ProtocoloPiernasIntegracionTests.cs:227`: lee `realizadasEfectivas`; `mi-historial.spec.ts` y `sesion-detalle.spec.ts`: datos simulados con `mensaje` | Verifican la comparación por serie y el conteo. | Sin cambio; siguen pasando. |
| 10 | Analítica | `GymBro.IA` | Ningún script ni notebook consume la comparación. Las únicas coincidencias de texto ("cumple", "comparaciones") están en `01_Analisis_y_Depuracion_Datasets.ipynb` y se refieren al catálogo de ejercicios. | Sin cambio. |
| 11 | Reglas de seguridad | `ReglasSeguridadService` / `ReglasSeguridad.cs` (R-RIR-1) | Usa el RIR de las series por su cuenta, no el veredicto. | Sin cambio. |

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas (specs del cliente). El criterio 1 se verificó con los datos reales de la base y con una prueba que reproduce esos valores; la pantalla no se abrió en el navegador.**

1. **Detalle de sesión del Socio (`/mi/historial/:id`).** Cada ejercicio con prescripción muestra "Series: N de M" y, si hay al menos una serie efectiva, "Repeticiones en rango: K de N series". Los dos indicadores se muestran con el mismo estilo neutro: no hay color de acierto ni de error ni veredicto. Se mantienen los íconos por serie (✔ / "!") con su criterio anterior y el RIR por serie.
2. **Cálculo en el cliente, sin cambiar el contrato.** `indicadoresCumplimiento(e)` (`mi-historial.ts`) usa datos que la API ya devolvía: `prescripcion.seriesObjetivo`, `repeticionesMinimas`, `repeticionesMaximas` y `ejecucion.detallesSeries`, filtradas por `esCalentamiento`.
3. **Veredicto anterior conservado como obsoleto.** `ComparacionSeriesDto.Mensaje` sigue en la respuesta, marcado `[Obsolete]` en C# y `@deprecated` en TypeScript, porque la vista del Personal Trainer todavía lo usa.

## Decisiones técnicas

- **D1. Cálculo en el cliente.** La especificación pide no cambiar el contrato sin aprobación. Los datos necesarios ya estaban en la respuesta, así que no se agregaron campos a la API. Si más adelante otras vistas o la analítica necesitan los indicadores, se pueden mover al servidor como campos nuevos, con aprobación.
- **D2. Rango calculado con la prescripción, no con `RepeticionesComparacion.CumpleObjetivo`.** El resultado es el mismo (extremos incluidos), pero el indicador no depende de un campo por serie que podría cambiar.
- **D3. Ejercicio sin series registradas.** Se muestra "Series: 0 de M", se omite el indicador de repeticiones (sería "0 de 0") y se mantiene "No registraste series de este ejercicio."
- **D4. Ejercicios medidos en tiempo (Etapa 9a-1).** El segundo indicador dice "Segundos en rango: K de N series" en lugar de "Repeticiones en rango", igual que el resto de la interfaz, que adapta la unidad al tipo de medida. Es un agregado a la especificación (ver desvíos).
- **D5. Ejercicios sin prescripción** (sesión libre o ejercicio fuera de la rutina): no muestran indicadores, porque no hay objetivo. Es el mismo comportamiento que antes.
- **D6. `[Obsolete]` con supresión local.** Para no sumar advertencias al build, el único lugar que todavía asigna `Mensaje` desactiva CS0618 solo en ese bloque.

## Texto propuesto para 4.6 (verificación)

| Criterio o prueba requerida | Prueba | Resultado |
|---|---|---|
| 3 de 3 series con 8, 8 y 5 (rango 8–12): "Series: 3 de 3" y "Repeticiones en rango: 2 de 3"; sin "Cumple objetivo" | `mi-historial.spec.ts`, Etapa 8d, caso 1. El dato simulado de la API trae `mensaje: 'Cumple objetivo'` a propósito, para comprobar que no se muestra. | Correcta |
| 4 series con objetivo 3: "Series: 4 de 3", sin marcarlo como incumplimiento | caso 2 (no aparece "Excedieron", "No cumple" ni "Cumple objetivo") | Correcta |
| 2 series con objetivo 3: "Series: 2 de 3" | caso 3 | Correcta |
| Ejercicio sin series registradas | caso 4: "Series: 0 de 3", sin indicador de repeticiones | Correcta |
| Calentamiento excluido de ambos indicadores | caso 5: 2 de calentamiento (una dentro y otra fuera de rango) y 2 efectivas; "Series: 2 de 3" y "Repeticiones en rango: 1 de 2 series" | Correcta |
| Ejercicio por tiempo (D4) | caso 6: "Segundos en rango: 1 de 2 series" | Correcta |
| Las pruebas detectan la regla anterior | Con `mi-historial.ts` de `08e5dc2`, los 6 casos fallan (el texto muestra "Cumple objetivo"); con la Etapa 8d pasan (`etapa8d-regresion-ng.txt`) | Correcta |
| Autocompletado con sesión previa: trae peso y repeticiones | `mi-entrenar.spec.ts`, "autocompletado con la sesión anterior": la pantalla Entrenar recibe el listado como lo devuelve la API (con una serie de calentamiento). La columna "Anterior" y los campos muestran 40 × 10 y 42,5 × 9; la tercera serie, sin dato previo, queda vacía y con el mínimo. | Correcta |
| Autocompletado sin sesión previa: peso vacío y mínimo de repeticiones | misma suite: las 3 series con "Anterior" en "–", peso vacío y 8 repeticiones | Correcta |
| **Criterio 1:** la Prensa de la sesión del 25/11/2026 11:50 muestra "Series: 3 de 3" y "Repeticiones en rango: 2 de 3" | `etapa8d-criterio1-sesion-real.txt`: en la base hay 3 series efectivas (58×8, 59×8, 75×5) con objetivo 3 × 8–12, y la consulta con la regla nueva da 3 efectivas, objetivo 3 y 2 en rango. Esos mismos valores son el caso 1 de la prueba. | **Verificado con datos y prueba; no verificado en el navegador** |
| **Criterio 2:** ninguna aparición de "Cumple objetivo" en la interfaz del Socio | Búsqueda en `GymBro.Web/src`: el único componente que todavía muestra el campo es `sesiones/sesion-detalle.ts`, cuya ruta admite solo `AdministradorGimnasio` y `Entrenador` (`app.routes.ts`, líneas 84 a 89). El área `/mi` ya no lo muestra (pruebas 1 a 3). | Correcta |
| **Criterio 3:** build y pruebas sin errores | Evidencia `etapa8d-*` | Ver cifras |

**Cifras (etiquetadas).**

| Suite | Base (Etapa 8, evidencia `etapa8-historial-*`, commit `5b9de14`) | Etapa 8d | Diferencia |
|---|---|---|---|
| `dotnet build` | 0 errores, 1 advertencia (CS2008 de `GymBro.IA`) | 0 errores, 1 advertencia (la misma CS2008, ya existente); el atributo `[Obsolete]` no agrega advertencias | — |
| Unitarias .NET (`GymBro.Tests`) | 342 | **342** | 0 |
| Integración .NET (`GymBro.Tests.Integration`) | 469 | **469** | 0 |
| Angular (`ng test`) | 212 | **220** | +8 (6 de indicadores y 2 de autocompletado) |
| `ng build` | sin errores | sin errores | — |

Los commits entre `5b9de14` y la base `08e5dc2` solo agregan documentación, así que esas cifras valen como base. No se tomó una línea base nueva.

## Desvíos respecto del pedido

- **D4 (segundos).** La especificación dice "Repeticiones en rango". En los ejercicios por tiempo se muestra "Segundos en rango", siguiendo el criterio de unidades de la Etapa 9a-1. Si se prefiere el texto literal en todos los casos, es un cambio de una línea.
- **Pruebas de backend.** No se agregaron pruebas .NET: el backend no cambió de comportamiento (solo se marcó el atributo obsoleto) y todas las pruebas requeridas son de interfaz.
- **Carpeta de salida alternativa.** `GymBro.Api` estaba en ejecución desde Visual Studio (PID 11560) y bloqueaba `GymBro.Api/bin`. Build y pruebas .NET se corrieron con `--artifacts-path .tmp-artifacts`, dentro del repositorio porque algunas pruebas buscan `GymBro.slnx` subiendo carpetas. La carpeta se borró al terminar: **no quedó en el árbol, no figura en `git status` y no se commiteó.** `.gitignore` no la cubre, así que borrarla es necesario.
## Qué quedó sin verificar

- **La pantalla real en el navegador** (escritorio y móvil), incluida la sesión del 25/11/2026 11:50. El criterio 1 está respaldado por los datos de la base y por la prueba con los mismos valores, no por una captura de la pantalla.
- **La respuesta real de `GET /api/Sesiones/{id}/comparacion`** para esa sesión. No se llamó a la API en ejecución porque hacía falta un token del socio.

## Pendientes y limitaciones

- **Vista del Dueño y del Personal Trainer** (`sesiones/sesion-detalle.ts`): sigue mostrando el veredicto anterior ("Cumple objetivo" / "Excedieron N series"), que contradice la decisión de que más series no es incumplimiento. Hay que decidir si se aplican los mismos indicadores. Estado: *Especificado, pero no implementado*.
- **Retiro de `ComparacionSeriesDto.Mensaje`:** obsoleto pero todavía en uso por la vista del punto anterior. Se puede retirar cuando esa vista migre. Estado: *Trabajo futuro*.
- **Confirmar el nombre de la etapa** ("8d").
- **Integración a `main`** de `etapa7d` y `etapa8d`: la decide el autor.

# Etapa 8 — Corrección de los conteos del Historial del Socio: texto para el informe y registro de decisiones

**Fecha:** 25/11/2026. **Rama:** `etapa7d` (la corrección se hizo sobre la rama de trabajo vigente, a continuación de `c11022b`; no se creó una rama propia). **Sin merge a `main`:** la integración la decide el autor después de la revisión de Claude Web.
**Commits (publicados en `origin/etapa7d`):** código y pruebas en `5b9de14`; evidencia sobre ese commit en `2cd8453`; este informe en el commit siguiente.
**Evidencia:** `docs/pfc/evidencia/etapa8-historial-*.txt`:
- `etapa8-historial-dotnet-build.txt`, `etapa8-historial-dotnet-test.txt`, `etapa8-historial-ng-build.txt`, `etapa8-historial-ng-test.txt`: build y pruebas sobre el commit `5b9de14`, con fecha;
- `etapa8-historial-regresion.txt`: la prueba nueva corrida sin la corrección (falla) y con la corrección (pasa).

Sin migración: la corrección no cambia el modelo de datos ni el contrato de la API.

## Problema reportado

Las series se persisten correctamente (verificado por el autor con un CSV de la base y con la pantalla de detalle de la sesión), pero el listado de `/mi/historial` mostraba "0 ejercicios · 0 series" para sesiones que sí tienen datos.

## Causa

- **Principal:** `GymBro.Infrastructure/Repositories/SesionEntrenamientoRepository.cs`, método `GetAllBySocioIdAsync` (líneas 38 a 44 antes de la corrección). La consulta del listado traía las sesiones sin `Include` de `EjerciciosEjecutados` ni de `SeriesRealizadas`, así que la colección llegaba vacía y `MapToResponseDto` (`SesionEntrenamientoService.cs`) devolvía `ejercicios: []`. El detalle no tenía el problema porque `GetByIdAsync` sí incluye ejercicios y series. El DTO y el mapeo del cliente eran correctos.
- **Secundaria (cliente):** `GymBro.Web/src/app/features/mi/mi-historial.ts`, línea 30 antes de la corrección. `s.ejercicios.length` cuenta posiciones de la sesión, no ejercicios distintos: un mismo ejercicio registrado en dos posiciones se contaba dos veces.
- **Alcance del defecto:** el mismo endpoint (`GET /api/Sesiones?socioId=`) alimenta también el autocompletado de "últimas series" al iniciar un entrenamiento (`mi-entrenar.ts`, `ultimasSeries(historial)`) y el panel de sesiones del Personal Trainer (`sesiones-panel.ts`). Ambos recibían la lista vacía y quedan corregidos por la misma causa. No se agregaron pruebas específicas para esas dos pantallas.

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas (API real contra PostgreSQL descartable, y specs del cliente). Pendiente: la prueba manual en pantalla.**

1. **Repositorio.** `GetAllBySocioIdAsync` incluye `EjerciciosEjecutados` y `SeriesRealizadas` (`Include` / `ThenInclude`). EF lo resuelve en una sola consulta con JOIN, sin una consulta adicional por sesión. No se incluye la navegación `Ejercicio`: se respeta la decisión de la Etapa 9a-2 (`EjerciciosDeHistorial`), porque esa navegación obligatoria con el filtro por gimnasio puede hacer desaparecer filas del historial. El listado no necesita el nombre del ejercicio.
2. **Cliente.** El listado muestra los **ejercicios distintos** de la sesión (`ejerciciosDistintos`, por `ejercicioId`) y las **series efectivas registradas** (sin calentamiento). El criterio de series no cambió: es el que ya tenía el cliente y coincide con el de la comparación contra la prescripción.
3. **Privacidad.** La corrección no amplía el acceso: el endpoint mantiene la validación de acceso al socio (`AsegurarAccesoAsync`) y el filtro de tenant, y solo devuelve ejercicios y series de sesiones que ya eran visibles para quien consulta. El Administrador de la Plataforma sigue sin acceso a sesiones.

## Texto propuesto para 4.6 (verificación)

| Criterio | Prueba | Resultado |
|---|---|---|
| El listado de sesiones devuelve los ejercicios y las series de cada sesión | `SesionesAislamientoTests.ListadoDeSesiones_IncluyeEjerciciosYSeriesRegistradas`: registra por la API una sesión con dos posiciones del mismo ejercicio y 4 series (1 de calentamiento); el listado devuelve 2 posiciones, 1 ejercicio distinto, 4 series en total y 3 efectivas | Correcta |
| La prueba detecta el defecto | La misma prueba con el repositorio de `c11022b` (sin `Include`) falla con `Expected: 2, Actual: 0`; con la corrección pasa (evidencia `etapa8-historial-regresion.txt`) | Correcta |
| El listado muestra ejercicios distintos y series efectivas | `mi-historial.spec.ts`, "MiHistorial (Socio): conteos del listado": un ejercicio repetido y una serie de calentamiento; se muestra "2 ejercicios · 4 series" | Correcta |
| Sin una consulta adicional por sesión | Revisión de código: un único `Include` / `ThenInclude` en la consulta del listado | Correcta (no medida con un registro de SQL) |
| Build y pruebas sin errores nuevos (piso de la Etapa 7d: 342 / 468 / 211) | Evidencia `etapa8-historial-*` | **342 / 469 / 212**, 0 errores (más la advertencia CS2008 ya existente de `GymBro.IA`) |

**Cifras antes y después.** Unitarias: 342 → 342. Integración: 468 → 469 (+1, la prueba del listado). Cliente: 211 → 212 (+1, la prueba del listado). Las cifras "antes" son las de la evidencia de la Etapa 7d (`etapa7d-dotnet-test.txt`, `etapa7d-ng-test.txt`, commit `0cdbe67`); no se tomó una línea base nueva antes de esta corrección.

## Regla de "Cumple objetivo" (informe; la regla no se modificó)

**Estado: Implementado y verificado** (comportamiento vigente en `GymBro.Application/Services/SesionEntrenamientoService.cs`, `GetComparacionAsync`).

| Nivel | Regla vigente | Ubicación |
|---|---|---|
| Ejercicio (mensaje "Cumple objetivo") | Las series efectivas (sin calentamiento) deben ser **exactamente iguales** a `SeriesObjetivo`. Menos series da "Faltaron N series"; más series da "Excedieron N series". Ninguno de los dos casos cumple. | líneas 300 a 310 |
| Serie (ícono del detalle) | `RepeticionesMinimas <= repeticiones <= RepeticionesMaximas`, extremos incluidos. Solo se evalúan series efectivas de un ejercicio con prescripción. En ejercicios medidos en segundos (Etapa 9a-1) el valor se interpreta en segundos. | líneas 269 a 281 |
| Serie (RIR) | Igualdad exacta con `RIRObjetivo`, solo si la prescripción y la serie tienen RIR. Se calcula, pero el detalle del Socio no lo muestra. | líneas 283 a 293 |

**Una serie fuera de rango no impide hoy "Cumple objetivo".** El mensaje por ejercicio solo compara la cantidad de series. Ejemplo: 3 series efectivas con objetivo de 3 × 8–12, una de ellas con 6 repeticiones, se informa "Cumple objetivo" y esa serie se marca con el ícono de error.

Si la serie fuera de rango debe impedir "Cumple objetivo" es una decisión del autor y de Claude Web (*Hipótesis/Propuesta*, sin implementar). Alternativas posibles, sin evaluar:
- mantener la regla actual (la cantidad de series define el mensaje y cada serie se marca por separado);
- exigir además que todas las series efectivas estén dentro del rango;
- informar dos indicadores separados (series y repeticiones).

## Desvíos respecto del pedido

- **Rama.** La corrección se hizo y se publicó en `etapa7d`, la rama de trabajo vigente, y no en una rama propia de la Etapa 8. Si el autor lo prefiere, los commits `5b9de14` y `2cd8453` se pueden llevar a otra rama con `cherry-pick`.
- **Compilación con carpeta de salida alternativa.** Durante la verificación, `GymBro.Api` estaba en ejecución desde Visual Studio y bloqueaba `GymBro.Api/bin` (el `dotnet build` habitual falló con MSB3027). No se detuvo ese proceso: build y pruebas se corrieron con `--artifacts-path .tmp-artifacts`, una carpeta temporal dentro del repositorio que se borró al terminar. Tiene que estar dentro del repositorio porque algunas pruebas de integración ubican `GymBro.slnx` subiendo desde la carpeta del binario: con la carpeta fuera del repositorio, 21 pruebas fallaron por ese motivo, no por el cambio.
- **`angular.json`.** Durante las corridas, git lo marcaba como modificado, pero `git diff` estaba vacío (contenido idéntico al commit). No forma parte de ningún commit de esta corrección.

## Pendientes y limitaciones

- **Prueba manual en pantalla (escritorio y móvil):** no realizada desde Claude Code. Estado: *Parcialmente implementado* en cuanto a verificación de interfaz (specs sí, navegador no).
- **Series de calentamiento en el contador del listado:** hoy no se cuentan. Decidir si "series registradas" debe incluirlas (cambio de una línea en el cliente).
- **Regla de "Cumple objetivo" con series fuera de rango:** decisión pendiente (ver la sección anterior).
- **Autocompletado de "últimas series" y panel del Personal Trainer:** quedan corregidos por la misma causa, pero no tienen pruebas específicas.
- **Build habitual:** conviene repetir `dotnet build` sin `--artifacts-path` cuando la API esté detenida.

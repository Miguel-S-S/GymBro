# Etapa 10a — Reglas deterministas de seguridad (RIR, progresión de cargas y FC de reposo): texto para el informe y registro de decisiones

**Fecha:** 19/11/2026. **Rama:** `etapa10a`, creada desde `main` en `5503cb1`. **Sin merge ni push a `main`:** la integración la decide el autor después de la revisión de Claude Web.
**Commits:** código, migración y pruebas en `69fd0ae`; corrección de 5 advertencias nuevas que generaban las pruebas (xUnit2031 y EF1002) en `7ac55c2`; evidencia (generada sobre `7ac55c2`) y este informe en el commit siguiente.
**Evidencia:** `docs/pfc/evidencia/etapa10a-*.txt`:
- `etapa10a-base-*.txt`: línea base sobre `main` (`5503cb1`), antes de cualquier cambio;
- `etapa10a-migracion.sql.txt`: SQL de la migración (Up y Down), generado sin conexión a ninguna base;
- `etapa10a-dotnet-build.txt`, `etapa10a-dotnet-test.txt`, `etapa10a-ng-build.txt`, `etapa10a-ng-test.txt`: build y pruebas sobre el commit de código, con fecha.

**Sobre "FC de reposo":** en este proyecto "FCR" significa frecuencia cardíaca **en reposo** (decisión D1). No se implementó ninguna regla con frecuencia cardíaca de reserva ni con FC máxima estimada.

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas (reglas puras, API real contra PostgreSQL descartable y specs del cliente). Pendiente: la prueba manual en pantalla y en celular.**

GymBro registra series con RIR, rutinas con RIR objetivo y un registro diario con la frecuencia cardíaca en reposo, pero no los usaba para advertir sobrecargas. En esta etapa se agregó un **motor de reglas deterministas de seguridad**, sin aprendizaje automático: funciones puras en `GymBro.Domain/Rules`, con umbrales configurables, que generan **alertas** y **sugerencias de carga** auditables.

1. **R-RIR-1, esfuerzo mayor al planificado.** Por ejercicio de una sesión ligada a una rutina, se toma la mediana del RIR de las series de trabajo (sin calentamiento) con dato, con al menos 2 series. Si el RIR objetivo menos esa mediana es 2 o más, se genera una advertencia ("llegaste mucho más cerca del fallo que lo planificado").
2. **R-PROG-1, sugerencia de carga (doble progresión simple).** Solo para ejercicios medidos en repeticiones, con carga mayor que 0 kg e igual en todas las series de trabajo, con todos los RIR registrados y sin R-RIR-1 en el ejercicio.
   - **Subir** si se hicieron al menos las series prescritas, todas llegaron al máximo de repeticiones y la mediana del RIR es mayor o igual al objetivo.
   - **Bajar** si al menos la mitad de las series no llegó al mínimo.
   - La carga sugerida se redondea a 0,5 kg y respeta un cambio mínimo de 0,5 kg y un tope del 10 %, que gana sobre el mínimo.
   - Es **informativa**: nunca modifica la rutina (D5).
3. **R-FC-1, FC de reposo elevada respecto de la propia línea base.** La línea base es la mediana de la FC de reposo de los 14 días anteriores, con al menos 5 registros; los días sin dato no cuentan.
   - Se genera una advertencia si el valor del día la supera en al menos 10 % **y** al menos 5 lpm. La alerta informa la variación porcentual.
   - Corregir un día reevalúa también los días posteriores con registro dentro de la ventana, porque su línea base cambia.
4. **R-LIM, límites duros de una prescripción.** El validador puro lo usan dos procesos y queda expuesto como servicio para la 9b y la 7c:
   - **Generación del baseline:** debe cumplirlo siempre. Si no, la generación se rechaza con un mensaje claro de catálogo incompleto y el log registra solo los códigos.
   - **Revisión humana:** las violaciones se muestran al revisor y quedan registradas en la revisión, sin bloquear (D3).

Cada alerta guarda en `detalle` (jsonb) los valores de entrada, los umbrales aplicados, el resultado y la versión de las reglas (`10a-1`). Las alertas son **idempotentes**: una fila por socio, regla y origen. Reevaluar actualiza la fila; si la condición deja de cumplirse, la retira con baja lógica, y una alerta ya vista no vuelve a pendiente si el resultado no cambió. La evaluación es síncrona al guardar la sesión o el bienestar y **tolerante a fallos**: un error se registra sin datos de salud y el guardado sigue (D6).

Las alertas son **datos de salud**:
- **Quién las ve:** el propio socio (también si está vinculado a un gimnasio, D2), el Dueño de su gimnasio y el Personal Trainer asignado. El Administrador de la Plataforma no accede y recibe 403.
- **Sin consentimiento de datos de salud:** no se generan alertas de sesión, aunque la sesión se guarda igual.
- **Marcar una alerta como vista:** queda auditado sin copiar valores de salud.

**Pantallas:**
- **Socio:** tarjeta "Avisos" en "Hoy", con las alertas en lenguaje simple y "Próxima vez: 42,5 kg" para las sugerencias.
- **Personal:** pestaña "Alertas" en la ficha del socio, con la regla, los valores y el botón "Marcar vista".
- **Revisión:** panel "Advertencias de límites", que solo aparece si hay violaciones.

**Advertencias:**
- **Umbrales:** son **decisiones de diseño provisionales, sin fuente bibliográfica**. Las reglas **no están validadas clínicamente** y se revisan con la Personal Trainer antes de usarse con personas reales (D4).
- **Alcance:** las reglas orientan; no diagnostican ni reemplazan el criterio profesional.
- **Socio independiente:** como no tiene revisor, estas reglas son su única guarda.

## Tabla de umbrales (sección `ReglasSeguridad` de la configuración)

| Regla | Parámetro | Valor | Origen |
|---|---|---|---|
| R-RIR-1 | Diferencia mínima (RIR objetivo − mediana realizada) | 2 | Decisión de diseño provisional |
| R-RIR-1 | Series de trabajo con RIR registrado | ≥ 2 | Decisión de diseño provisional |
| R-PROG-1 | Incremento al subir | 2,5 % | Decisión de diseño provisional |
| R-PROG-1 | Decremento al bajar | 5 % | Decisión de diseño provisional |
| R-PROG-1 | Redondeo | 0,5 kg, al más cercano; los puntos medios hacia arriba | Decisión de diseño provisional |
| R-PROG-1 | Cambio mínimo | 0,5 kg | Decisión de diseño provisional |
| R-PROG-1 | Tope del cambio (gana sobre el mínimo) | ±10 % | Decisión de diseño provisional |
| R-FC-1 | Días de la línea base | 14 anteriores | Decisión de diseño provisional |
| R-FC-1 | Registros mínimos en la línea base | 5 | Decisión de diseño provisional |
| R-FC-1 | Umbral relativo y absoluto (los dos) | ≥ 10 % y ≥ 5 lpm | Decisión de diseño provisional |
| R-LIM | Series por ejercicio | 1 a 6 | Decisión de diseño provisional |
| R-LIM | Repeticiones / segundos | 1 a 30 / 5 a 180 | Decisión de diseño provisional |
| R-LIM | Descanso | 30 a 600 s | Decisión de diseño provisional |
| R-LIM | RIR objetivo | 0 a 5; principiante ≥ 2 | Decisión de diseño provisional |
| R-LIM | Ejercicios por rutina / series totales | 1 a 12 / hasta 30 | Decisión de diseño provisional |
| R-LIM | Ejercicio repetido | No admitido | Decisión de diseño provisional |

**Efecto de los topes en cargas chicas (P2):** con los valores por defecto, **por debajo de 5 kg no hay sugerencias**: el cambio mínimo de 0,5 kg supera el 10 %. Con **exactamente 5 kg** el cambio de ±0,5 kg es justo el 10 % y el tope lo admite (5 → 5,5 kg o 5 → 4,5 kg). Si el autor prefiere que en 5 kg tampoco haya sugerencia, basta con cambiar la comparación del tope a estricta.

## Texto propuesto para 4.6 (verificación)

| Criterio | Prueba | Resultado |
|---|---|---|
| Cada regla con casos límite: en el umbral, antes y después; sin dato; calentamiento excluido; RIR nulo; una sola serie | `ReglasSeguridadTests` (unitarias) | Verificado |
| R-RIR-1 usa la mediana y exige ≥ 2 series con dato | `RIR_alerta_segun_la_mediana`, `RIR_exige_al_menos_dos_series...`, `RIR_con_cantidad_par...` | Verificado |
| R-PROG-1: subir, mantener y bajar; redondeo con puntos medios; mínimo +0,5 kg y topes ±10 %; sin sugerencia en segundos, peso corporal, cargas distintas, datos faltantes ni con R-RIR-1 | `PROG_*` y `Redondeo_a_medio_kilo...` (5 casos de redondeo) | Verificado |
| R-FC-1: 4 registros no evalúa y 5 sí; umbral de 10 % y de 5 lpm por separado; la mediana ignora los días sin dato | `FC_*` | Verificado |
| El validador marca cada límite | `ValidadorPrescripcionTests` (borde y fuera de borde de cada límite) | Verificado |
| El baseline pasa el validador en todas las combinaciones de nivel y objetivo, y la configuración por defecto también | Unitaria (9 combinaciones × 2 catálogos) y de integración (9 combinaciones por la API); `La_configuracion_por_defecto...` | Verificado |
| Un baseline fuera de límites no se genera: mensaje claro de catálogo incompleto y solo códigos en el log | Unitaria (2 casos) e integración `Un_baseline_fuera_de_limites_por_configuracion...` | Verificado |
| Registrar una sesión genera alertas y sugerencias; reevaluar no duplica; corregir el dato retira la alerta; una vista sigue vista | `ReglasSeguridadIntegracionTests` (sesión y bienestar) | Verificado |
| Corregir ayer reevalúa los días posteriores con registro (P5) | `Corregir_ayer_reevalua_los_dias_posteriores_con_registro` | Verificado |
| Sin consentimiento, la sesión se guarda y no se evalúa (P6) | `Sin_consentimiento_la_sesion_se_guarda_y_no_se_evalua` | Verificado |
| Un fallo del servicio de reglas no impide registrar la sesión ni guardar el bienestar | `Un_fallo_de_las_reglas_no_impide...` (servicio que falla) | Verificado |
| Aislamiento: otro gimnasio 404; Personal Trainer no asignado 404; Administrador de la Plataforma 403; el socio vinculado ve las suyas | `Aislamiento_de_las_alertas_por_rol`, `MatrizAutorizacionTests` ampliada | Verificado |
| La alerta guarda entradas, umbrales y versión de las reglas | Asserts sobre `detalle` en las pruebas de sesión y de FC | Verificado |
| Los umbrales salen de la configuración | `ConfiguracionEjemploTests` (2 pruebas nuevas) | Verificado |
| Las violaciones del validador quedan registradas en la revisión y no bloquean; un valor imposible sigue rechazado | `La_revision_muestra_las_violaciones...` | Verificado |
| Marcar vista queda auditado sin valores de salud | `Marcar_vista_queda_auditado_sin_valores_de_salud` | Verificado |
| Migración reversible; CHECK de los enums; clave única aunque la fila esté dada de baja | `MigracionReglasSeguridadTests` (6 pruebas) | Verificado |
| Pantallas: el Socio ve sus avisos; el personal marca vista; el panel de límites aparece solo con violaciones | `mis-avisos.spec.ts`, `alertas-panel.spec.ts`, `advertencias-limites.spec.ts`, `mi-hoy.spec.ts` | Verificado |
| Build y pruebas sin errores nuevos (piso: 170 / 353 / 183) | Evidencia `etapa10a-*` | **250 / 391 / 191**, 0 errores (más la advertencia CS2008 ya existente) |

**Sin verificar:** la prueba manual en pantalla y en celular.

## Registro de decisiones de la etapa

| # | Decisión | Estado |
|---|---|---|
| D1 a D6, O-1 | Las del pedido (FC de reposo; el socio ve sus alertas; límites advierten y se registran; umbrales a la Personal Trainer; sugerencias informativas; evaluación síncrona y tolerante a fallos; 9a-2c fuera de la etapa) | Confirmadas por el autor |
| P1 | R-PROG-1 sugiere solo si todas las series de trabajo tienen el mismo peso | Aceptada por el autor |
| P2 | Gana el tope sobre el cambio mínimo; mínimo ±0,5 kg; redondeo al más cercano con puntos medios hacia arriba | Aceptada por el autor |
| P3 | Para subir hacen falta al menos `SeriesObjetivo` series de trabajo | Aceptada por el autor |
| P4 | Un baseline que viola R-LIM no se genera: mensaje claro de catálogo incompleto y códigos en el log | Aceptada por el autor |
| P5 | Corregir un día reevalúa los días con registro dentro de los 14 posteriores | Aceptada por el autor |
| P6 | Sin consentimiento, la sesión se guarda y no se evalúa | Aceptada por el autor |
| X1 | La sesión y la rutina se relacionan por `EjercicioId` (no hay vínculo directo); un ejercicio fuera de la rutina no se evalúa | Decisión de diseño |
| X2 | Columna `clave_origen` (id del ejercicio de la sesión o del registro de bienestar) para el índice único `(socio_id, regla_codigo, clave_origen)`. No es parcial: la fila retirada se reactiva | Decisión de diseño |
| X3 | El filtro de las alertas usa el gimnasio **actual** del socio, como el de las sesiones; `gimnasio_id` queda como dato informativo. Un socio que deja el gimnasio deja de ser visible para ese gimnasio | Decisión de diseño |
| X4 | `alertas_seguridad` tiene FK solo al socio. Sesión, ejercicio de la sesión y registro de bienestar se guardan sin FK, como la asociación sesión-bienestar de la 8b | Decisión de diseño |
| X5 | Comparación del resultado para la idempotencia: mensaje y `detalle` canónico (claves ordenadas), porque jsonb reordena las claves | Decisión de diseño |
| X6 | Mediana con cantidad par: promedio de los dos centrales (puede ser 1,5) | Decisión de diseño |
| X7 | R-LIM no evalúa un RIR objetivo nulo ("sin objetivo") | Decisión de diseño |
| X8 | Registro técnico por una abstracción `IRegistroEventos` (implementada con `ILogger` en la API), para no sumar una dependencia de logging a la capa de aplicación | Decisión de diseño |
| X9 | `ManejadorExcepciones`: `CatalogosIncompletosException` sigue respondiendo 500, pero con su propio mensaje y el título "Catálogo incompleto". Antes cualquier 500 decía "Ocurrió un error inesperado". El cambio alcanza también a los casos anteriores de catálogo incompleto | Decisión de diseño (pedido de P4) |
| X10 | La sugerencia mantiene la carga en múltiplos de 0,5 kg también cuando aplica el cambio mínimo (por ejemplo, 5,2 kg no sugiere 5,7 kg) | Decisión de diseño |

## Diferencias y pendientes informados

- **El registro de sesiones no exige consentimiento de datos de salud** (P6). La sesión se guarda y solo se omite la evaluación de reglas. Que el registro exija consentimiento queda como pendiente para decidir.
- **Dosificación del dataset:** el `series.csv` versionado trae una fila 30–60 x 1 y otras con rangos amplios. Hoy el seeder no lo carga; si se cargara, el baseline podría violar R-LIM y la generación se rechazaría con el mensaje de catálogo incompleto, que es lo esperado.
- **Configuración del baseline:** con `Baseline:RirPorDefecto` menor que 2, un principiante viola R-LIM y su baseline se rechaza. Lo verifica una prueba.
- **Sesiones históricas:** las reglas se evalúan al registrar. Las sesiones anteriores a la etapa no tienen alertas; no se hizo un proceso de reevaluación masiva.
- **Advertencias del build:** el primer build completo después del commit `69fd0ae` mostró 5 advertencias nuevas, todas de las pruebas: 4 xUnit2031 y 1 EF1002 en un SQL de prueba. Se corrigieron en `7ac55c2`; queda solo la CS2008 de `GymBro.IA.csproj`, que ya existía.
- **Base local:** la migración no se aplicó desde Claude Code; se aplica al levantar la API.
- **Sin verificar:** la prueba manual en pantalla y en celular.

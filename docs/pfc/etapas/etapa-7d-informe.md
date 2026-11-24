# Etapa 7d — Protocolo de piernas: generación determinista, trazabilidad y KPIs (texto para el informe y registro de decisiones)

**Fecha:** 24/11/2026. **Rama de trabajo:** `etapa7d`, creada desde `main` en `c29890f`. **Commits:** código, migración, pruebas, evidencia y este informe en un único commit local de la rama `etapa7d`, autorizado por el autor el 24/11/2026 (sin push). La evidencia final se generó sobre el árbol de trabajo con esos mismos cambios, inmediatamente antes del commit.
**Especificación:** v3 del 23/11/2026 (Claude Web), con P1, P2 y P3 confirmadas por el autor el 24/11/2026 antes de empezar.
**Evidencia:** `docs/pfc/evidencia/`:
- `etapa7d-base-*.txt`: línea base sobre `main` (`c29890f`) antes de cualquier cambio: build sin advertencias; 250 + 406 pruebas .NET; 204 pruebas del cliente; `ng build` correcto.
- `etapa7d-catalogo-despues.txt` (aportado por el autor): los 10 ejercicios con `origen = Proyecto`, `gimnasio_id` nulo, Prensa y Hack con `MAQUINA_DISCOS`, embedding presente. `etapa7d-catalogo-antes.txt` **no está** en el repositorio (ver Pendientes).
- `etapa7d-c4-factibilidad.txt`: verificación C4/B13 con el socio de validación (consulta de solo lectura sobre `gymbro_db`).
- `etapa7d-migracion.sql.txt`: SQL de la migración (Up y Down, idempotente), generado sin conexión a ninguna base.
- `etapa7d-origen-datos-verificacion.txt`: clasificación por origen de datos después del paso 2 de `clasificar-origen-datos.sql` (consultas de solo lectura).
- `etapa7d-siembra-verificacion.txt`: verificación en `gymbro_db` después de que el autor aplicó la migración (24/11/2026), con consultas de solo lectura. Resultado: las 10 filas de `piernas-1.0` coinciden con la tabla 4.2 y sus ejercicios están activos, son globales y tienen origen Proyecto. Los 748 socios quedaron en `DatasetExterno` y `SinLimitacion`; las 12 prescripciones, en `DatasetExterno`, sin marcas; ninguna sesión anterior tiene vínculo.
- `etapa7d-dotnet-build.txt`, `etapa7d-dotnet-test.txt`, `etapa7d-ng-build.txt`, `etapa7d-ng-test.txt`: build y pruebas finales, con fecha.

**Advertencia que debe acompañar cualquier uso de este texto:** la dosificación (series, repeticiones, RIR, descanso, tempo), la composición de la rutina y las exclusiones por limitación articular son **criterios de diseño provisionales**, sin fuente bibliográfica ni validación clínica (decisión D5). La Personal Trainer los revisa antes de cualquier uso con personas reales. Los KPIs son **descriptivos**; con datos sintéticos verifican funcionamiento y trazabilidad, no resultados de entrenamiento. Este informe no contiene resultados de entrenamiento.

## Paso previo: condiciones de arranque

| Condición | Resultado |
|---|---|
| Catálogo corregido por el autor (`corregir-catalogo-piernas.sql`) | **Cumple.** `SELECT` de solo lectura sobre `gymbro_db`: los 10 ids con `origen = 'Proyecto'` y `gimnasio_id` nulo. |
| C4/B13, factibilidad con el socio de validación (`a0ebb7ab-…`) | **Cumple.** Socio del gimnasio `f8667ba5-…`, Principiante, 43 años, con consentimiento, 1 medición y sin rutinas. Los 10 ejercicios son factibles con el equipamiento marcado (criterio de `GetTiposEquipamientoFactiblesAsync`). Ver `etapa7d-c4-factibilidad.txt`. |
| P1, P2 y P3 | Confirmadas por el autor (24/11/2026): se implementan como están escritas. |

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas (funciones puras, servicios con dobles, API real contra PostgreSQL descartable y specs del cliente). Pendiente: aplicar la migración en la base local (lo confirma el autor), ejecutar `clasificar-origen-datos.sql`, la prueba manual en pantalla y la revisión de los parámetros por la Personal Trainer.**

Se agregó una generación de rutina de **extremidad inferior** que usa solo los 10 ejercicios curados del protocolo (lista blanca en la tabla `ejercicios_protocolo`), dosifica con el perfil del socio, su historial y sus alertas vigentes, y deja la prescripción trazable para compararla con las series realizadas mediante cuatro KPIs. No usa IA, embeddings ni pgvector.

1. **Catálogo del protocolo.** Tabla global `ejercicios_protocolo` (código `PIERNAS`, versión `piernas-1.0`) con grupo (Cuádriceps, Isquiotibiales, Pantorrillas), rol (Referencia o Variable), clase (Multiarticular o Aislamiento), umbral de exclusión por limitación de rodilla y lumbar, y orden de rotación. La migración la siembra con `INSERT … SELECT` sobre los UUID del catálogo del autor: en otra base inserta 0 filas y el generador falla indicando el grupo faltante; el arranque registra una advertencia.
2. **Datos del socio.** Limitación de rodilla y lumbar (`SinLimitacion`, `Moderada`, `Alta`; dato de salud con las mismas reglas de acceso y consentimiento que las mediciones) y origen de datos (`Sintetico`, `ValidacionFuncional`, `DatasetExterno`, `Real`). Los socios existentes quedan en `DatasetExterno` (valor conservador); el alta por la aplicación asigna `Real` y `DataSeeder`, `DatasetExterno`.
3. **Planificador (función pura).** Por cada una de las 6 posiciones de la composición (Cuádriceps-Referencia 1 y 2, Cuádriceps-Variable, Isquiotibiales-Referencia, Isquiotibiales-Variable, Pantorrillas-Variable):
   - elegibles = factibles por equipamiento y no excluidos por limitación (limitación del socio igual o mayor al umbral);
   - una referencia no elegible se **omite** sin sustituirse; una variable rota con índice `(número de generación − 1) mod n` sobre los elegibles ordenados;
   - series base 3 (multiarticular), 2 (aislamiento), 3 (pantorrilla); +1 con adherencia al rango ≥ 80 % en el último registro, sin alerta y nivel distinto de Principiante, hasta el máximo por ejercicio y el tope de 18 series por sesión en el orden de la composición;
   - con alerta vigente (tipo Alerta, sin baja, creada en los últimos 14 días) las series bajan 30 % con piso 2;
   - repeticiones 8-12 / 10-15 / 15-20; descanso 150 s (180 s con 40 años o más o alguna limitación moderada o alta) y 75 s; tempo excéntrico 3 s (4 s con el mismo ajuste);
   - RIR 2, que baja a 1 solo con historial y buena adherencia, sin alerta y nivel distinto de Principiante; **nunca 0**;
   - carga sugerida **informativa**: la de R-PROG-1 sobre el último registro, o la carga actual si es uniforme, o ninguna sin historial;
   - validación de salida: al menos un ejercicio por grupo (si no, error que nombra el grupo), sin RIR 0, sin repetidos, total dentro del tope.
4. **Servicio y API.** `POST api/Socio/{socioId}/prescripciones/piernas` (Dueño, Personal Trainer asignado, Socio). Mismas precondiciones que el baseline (consentimiento, medición, sin rutina pendiente). La rutina pasa por R-LIM (si no lo cumple, no se persiste), por la política de revisión del gimnasio y por la auditoría `GENERACION_PIERNAS`. `GET` y `PUT api/Socio/{socioId}/limitaciones-articulares` (el `PUT` exige consentimiento y audita `LIMITACIONES_ARTICULARES` sin los valores).
5. **Trazabilidad.** La prescripción guarda código y versión del protocolo, número de generación (no se reutiliza aunque la rutina se rechace), origen de datos copiado del socio, marca `excluida_estudio`, marca `es_modificada` y un `entrada_snapshot` (versión, generación, edad, nivel, limitaciones, medición usada y su FC de reposo, alertas vigentes, resumen del historial y SHA-256 de la configuración; sin nombre, DNI, email ni texto de condiciones médicas). Cada línea guarda rol, grupo, tempo, carga sugerida y el motivo de la selección (candidatos, índice de rotación, exclusiones y reglas aplicadas).
6. **Vínculo sesión → línea.** `sesiones_ejercicios.prescripcion_ejercicio_id`. Al registrar una sesión con prescripción se resuelve la línea (explícita, validada, o por ejercicio y orden). La comparación de la sesión y las reglas de seguridad prefieren el vínculo directo y conservan el criterio anterior cuando es nulo.
7. **Rutina modificada.** Una revisión "Editada" marca `es_modificada` solo si un ajuste cambia realmente la dosis.
8. **KPIs (funciones puras) y lectura.** `GET api/Socio/{socioId}/kpis-progresion` calcula K1 a K4 solo para socios `Sintetico` (y `ValidacionFuncional` si se pide expresamente, para verlo en pantalla, nunca para el estudio) y excluye las rutinas con `excluida_estudio`.
9. **Cliente.** En la pestaña Rutinas se conserva la tarjeta del baseline y se agrega "Protocolo de piernas" con las dos limitaciones, su guardado y el botón "Generar rutina de piernas"; la tarjeta de ejercicio muestra rol, tempo excéntrico y, si existe, la carga sugerida con la leyenda "sugerencia informativa". Sin pantalla de KPIs (Etapa 12).

### Tabla de parámetros (sección `ProtocoloPiernas`, valores por defecto en `ProtocoloPiernasConfig`)

| Parámetro | Valor | Fuente |
|---|---|---|
| Series base: multiarticular / aislamiento / pantorrilla | 3 / 2 / 3 | Criterio de diseño provisional (D5) |
| Series máximas por ejercicio: multiarticular / aislamiento | 4 / 3 | Idem |
| Series totales máximas por sesión | 18 | Idem |
| Repeticiones: multiarticular / aislamiento / pantorrilla | 8-12 / 10-15 / 15-20 | Idem |
| RIR objetivo / mínimo | 2 / 1 (nunca 0) | Idem |
| Descanso: multiarticular (ajustado) / aislamiento | 150 (180) / 75 s | Idem |
| Tempo excéntrico (ajustado) | 3 (4) s | Idem |
| Reducción por alerta / piso / ventana | 30 % / 2 series / 14 días | Idem |
| Adherencia mínima para +1 serie o RIR 1 | 80 % | Idem |
| Edad de ajuste | 40 años | Idem |
| Versión | `piernas-1.0` | Se incrementa al cambiar cualquier valor o regla |

### Definición de los KPIs (D6)

Criterio común: sesión efectiva = al menos una serie sin calentamiento; solo cuentan series efectivas; las filas con baja lógica no cuentan; orden por inicio de la sesión y luego por id.

| KPI | Definición implementada |
|---|---|
| K1 Delta de carga máxima | Por ejercicio: carga máxima de las series efectivas con carga > 0 y tipo Repeticiones (máximo si el ejercicio se repite en la sesión); delta contra la sesión anterior en la que el ejercicio tuvo carga; null en la primera aparición |
| K2 Volumen efectivo | Por sesión y grupo del protocolo: Σ peso × repeticiones y cantidad de series efectivas; segundos aportan 0 kg; ejercicios fuera del protocolo solo al total |
| K3 Adherencia y esfuerzo | Con la línea vinculada: % de series en rango; desvío RIR medio (rir − objetivo, positivo = más reserva) y desvío absoluto medio, solo con RIR cargado; n de cada parte; sin línea, null |
| K4 Adaptación y seguridad | Sesiones limpias / efectivas. Limpia = sin alerta vigente asociada (a la sesión o, R-FC-1, al bienestar del mismo día local), sin sugerencia R-PROG-1 de bajar carga y, con prescripción, series efectivas ≥ prescritas en todas sus líneas. Se informa la cantidad de generaciones con reducción por alerta |

## Texto propuesto para 4.6 (verificación)

**Estado: Implementado y verificado con pruebas automáticas.** Cifras de `etapa7d-dotnet-test.txt` y `etapa7d-ng-test.txt`: GymBro.Tests 342 pruebas (250 anteriores + 92 nuevas), GymBro.Tests.Integration 468 (406 anteriores + 62 nuevas), cliente 211 (204 + 7), todas en verde; build .NET con 0 advertencias y `ng build` correcto. Ninguna prueba existente se modificó: en `MatrizAutorizacionTests`, `EnumsNoDefinidosTests` y `ConfiguracionEjemploTests` solo se agregaron casos.

- **Planificador (unitarias):** determinismo (dos corridas idénticas e independencia del orden de entrada); rotación en las generaciones 1 a 6 con 1, 2 y 3 candidatos; las 9 combinaciones de limitación de rodilla y lumbar (ningún ejercicio excluido se prescribe y todas las exclusiones quedan en el motivo; rodilla Alta ⇒ error que nombra Cuádriceps); exclusión por equipamiento; omisión de referencia sin sustitución; edad ≥ 40 y limitación moderada (tempo 4, descanso 180); alerta (−30 %, piso 2, 4 → 3); RIR nunca 0 aun con configuración 0; Principiante con RIR ≥ 2; tope de 18 series; grupo faltante ⇒ error; las tres vías de la carga sugerida, más segundos y cargas no uniformes.
- **Servicio (dobles):** consentimiento, medición, pendiente de revisión, socio inexistente; número de generación = máximo + 1 aunque la anterior se haya rechazado; copia del origen de datos; snapshot y observaciones sin nombre, email, DNI ni condición médica; violación de R-LIM ⇒ no persiste ni audita; auditoría `GENERACION_PIERNAS`; huella de la configuración.
- **KPIs (unitarias):** +2,5 kg semanales recuperados por K1; estancamiento (delta 0); calentamiento más pesado excluido; segundos y peso corporal excluidos; K2 por grupo y fuera del protocolo; K3 con series fuera de rango y RIR nulos (valores exactos); K4 con alerta, sugerencia de bajar y series faltantes (índice 0,5); sesión solo de calentamiento no efectiva; el servicio excluye `Real`, `DatasetExterno` y `ValidacionFuncional` (salvo pedido expreso), excluye rutinas `excluida_estudio` y asocia R-FC-1 por día local.
- **Integración (PostgreSQL descartable):** migración (Down quita tabla y 16 columnas; Up sin los ejercicios siembra 0 filas y con ellos 10; valores por defecto y datos previos intactos; 11 CHECK; índice único parcial de generación); flujo por la API (6 ejercicios del protocolo y ninguno del dataset; rotación en la segunda generación; trazabilidad persistida; limitaciones que excluyen ejercicios; rodilla Alta ⇒ 400; consentimiento y valores inválidos ⇒ 400; auditoría sin valores); vínculo sesión → línea y uso en la comparación; sesión anterior sin vínculo comparada por ejercicio y orden; `es_modificada` solo con cambio real; número rechazado no reutilizado; KPIs solo con origen sintético; matriz de autorización: el Administrador de la Plataforma recibe 403 en limitaciones, generación y KPIs; el Personal Trainer no asignado y el Socio ajeno, 404; el Dueño de otro gimnasio, 404.
- **Regresión:** `PrescripcionBaselineServiceTests`, `ValidadorPrescripcionTests`, `ReglasSeguridadTests` y `SesionEntrenamientoServiceTests` sin cambios y en verde.

## Archivos

**Nuevos:**
- `GymBro.Domain/Enums/ProtocoloPiernasEnums.cs`, `GymBro.Domain/Entities/EjercicioProtocolo.cs`
- `GymBro.Domain/Rules/ProtocoloPiernasConfig.cs`, `GymBro.Domain/Rules/PlanificadorPiernas.cs`, `GymBro.Domain/Rules/Kpis/KpisProgresion.cs`
- `GymBro.Domain/Repositories/IProtocoloPiernasRepository.cs`, `GymBro.Domain/Repositories/IKpiProgresionRepository.cs`
- `GymBro.Application/DTOs/ProtocoloPiernasDtos.cs`
- `GymBro.Application/Services/PrescripcionPiernasService.cs`, `LimitacionesArticularesService.cs`, `KpiProgresionService.cs`
- `GymBro.Infrastructure/Repositories/ProtocoloPiernasRepository.cs`, `KpiProgresionRepository.cs`
- `GymBro.Infrastructure/Persistence/VerificacionProtocoloPiernas.cs`
- `GymBro.Infrastructure/Migrations/20261124133430_Etapa7d_ProtocoloPiernas.cs` (y `.Designer.cs`)
- `GymBro.Api/Controllers/LimitacionesArticularesController.cs`, `KpisProgresionController.cs`
- `GymBro.Tests/PlanificadorPiernasTests.cs`, `PrescripcionPiernasServiceTests.cs`, `KpisProgresionTests.cs`, `VinculoSesionLineaTests.cs`
- `GymBro.Tests.Integration/MigracionProtocoloPiernasTests.cs`, `ProtocoloPiernasIntegracionTests.cs`
- `GymBro.Web/src/app/features/rutinas/rutinas-panel.spec.ts`
- `docs/pfc/scripts/clasificar-origen-datos.sql` (copiado tal cual, sin ejecutar)

**Modificados:**
- Dominio: `Entities/Socio.cs`, `PrescripcionEntrenamiento.cs`, `PrescripcionEjercicio.cs`, `SesionEjercicio.cs` (propiedades nuevas).
- Infraestructura: `Persistence/GymBroDbContext.cs`, `Migrations/GymBroDbContextModelSnapshot.cs`, `Persistence/Seeders/DataSeeder.cs` (solo `OrigenDatos = DatasetExterno`).
- Aplicación: `DTOs/Acceso/AccesoDtos.cs`, `DTOs/RegistrarSesionEntrenamientoRequest.cs`, `DTOs/SesionEntrenamientoResponseDto.cs`, `DTOs/Comparacion/ComparacionSesionResponseDto.cs` (campos aditivos); `Services/SesionEntrenamientoService.cs` y `ReglasSeguridadService.cs` (6.3); `RevisionPrescripcionService.cs` (6.4); `SocioService.cs` y `AuthService.cs` (`OrigenDatos = Real` explícito).
- API: `Controllers/PrescripcionesController.cs`, `Program.cs`, `appsettings.Development.example.json`.
- Pruebas (solo casos agregados): `MatrizAutorizacionTests.cs`, `EnumsNoDefinidosTests.cs`, `ConfiguracionEjemploTests.cs`, `rutina-ejercicio-card.spec.ts`.
- Cliente: `core/api/prescripciones.api.ts`, `core/models/acceso.models.ts`, `features/rutinas/rutina-ejercicio-card.ts`, `features/rutinas/rutinas-panel.ts`.

**Sin cambios (verificado con `git status`):** `PrescripcionBaselineService.cs` (incluye `BaselineConfig`), `IBaselineRepository.cs`, `BaselineRepository.cs`, `PrescripcionBaselineServiceTests.cs`, `DemoSeeder.cs`, `CatalogoProyectoSeeder.cs`.

## Registro de decisiones técnicas de la etapa

| # | Decisión | Motivo |
|---|---|---|
| T1 | Se declara en el modelo el índice existente `ix_prescripciones_entrenamiento_socio_id` | EF quería eliminarlo por considerarlo cubierto por el índice nuevo, pero el nuevo es parcial (`protocolo_codigo IS NOT NULL`) y no cubre las demás filas. Así el Up queda sin `DROP` |
| T2 | El máximo de `numero_generacion` se toma por socio y **código** de protocolo (la especificación dice "socio y versión") | Coincide con el índice único `(socio_id, protocolo_codigo, numero_generacion)`; con "versión" un cambio de versión reiniciaría el número y chocaría con el índice. Incluye rutinas dadas de baja |
| T3 | Reducción por alerta: `min(series, max(piso, round(series × 0,7)))` | Con los valores por defecto da lo mismo que la fórmula de la especificación; solo evita que una configuración con menos de 2 series base termine **subiendo** las series |
| T4 | Las referencias se identifican por `(grupo, rol Referencia, orden_rotacion)` y no por UUID en la configuración | Evita escribir UUID de la base del autor en `appsettings` |
| T5 | El `GET api/Socio/{id}/prescripciones` lo sirve `PrescripcionPiernasService.ListarDeSocioAsync` | No se podía tocar `PrescripcionBaselineService`; la consulta y el mapeo son los mismos más los campos aditivos. `ListarDeSocioAsync` del baseline queda intacto y probado |
| T6 | El id explícito de línea al registrar una sesión debe ser de la misma prescripción **y del mismo ejercicio**; indicar una línea sin prescripción da 400 | Una línea de otro ejercicio volvería sin sentido la comparación y los KPIs |
| T7 | `es_modificada` considera solo los cinco valores de la dosis (series, repeticiones mín. y máx., RIR, descanso) | Un cambio de observaciones no altera la prescripción que miden los KPIs |
| T8 | "Último registro" de un ejercicio: la sesión más reciente (inicio y id); si el ejercicio se repite en ella, la ocurrencia de menor orden | Determinismo |
| T9 | Valores iniciales en C#: `Socio.OrigenDatos = Real` y `PrescripcionEntrenamiento.OrigenDatos = Real` | El enum no tiene 0 (P7) y el valor debe ser conservador (fuera del estudio). Las rutinas del baseline no copian el origen del socio (no se tocó ese servicio); los KPIs filtran además por el origen del socio |
| T10 | Los `DEFAULT` de base (`SinLimitacion`, `DatasetExterno`) quedan en las columnas después de rellenar | Mismo patrón que `socios.estado` (Etapa 9a-2b). La aplicación siempre envía el valor explícito |
| T11 | Advertencia de arranque `VerificacionProtocoloPiernas` | Especificación 4.2.1: si faltan filas o grupos, se registra una advertencia; no siembra nada |
| T12 | La respuesta de KPIs devuelve los registros de dominio (K1 a K4) dentro de `KpiProgresionDto` | Sin pantalla en esta etapa; evita duplicar tipos |

## Desvíos y limitaciones informados

1. **La dosis no sale de la tabla `dosificaciones`.** Sus filas de Legs tienen valores dudosos (Paso 0: series máximas 15, repeticiones 15-21). Los parámetros son criterios de diseño **sin fuente bibliográfica ni validación clínica**.
2. **Composición P3** (6 posiciones, 16 series base) es una propuesta de Claude Web confirmada por el autor, no un estándar.
3. **Con limitación de rodilla Alta no hay rutina de piernas:** todos los ejercicios de cuádriceps del protocolo se excluyen desde Alta (tabla 4.2) y la generación devuelve 400 indicando el grupo. Es consecuencia de la tabla provisional; se informa para la revisión de la Personal Trainer.
4. **Reproducibilidad del catálogo:** los UUID de los 10 ejercicios existen solo en la base del autor (`DataSeeder` usa `Guid.NewGuid()`). En una base nueva `ejercicios_protocolo` queda vacía, el arranque advierte y el generador falla indicando el grupo. La reproducibilidad del piloto se apoya en el script SQL versionado y en la evidencia del catálogo, no en el arranque. Las pruebas de integración usan ejercicios sintéticos con la misma forma de la siembra.
5. **Alerta vigente** se mide por `fecha_alta` (creación): una alerta reactivada después de 14 días de creada no cuenta como vigente.
6. **K1** no considera como aparición una sesión en la que el ejercicio no tuvo series efectivas con carga; los KPIs del período no miran sesiones anteriores al rango; las sesiones sin prescripción se incluyen (no tienen rutina que marcar como excluida). Las alertas con baja lógica no cuentan, lo que puede subestimar problemas de K4 en períodos pasados.
7. **Concurrencia:** dos generaciones simultáneas del mismo socio chocan con el índice único y la segunda falla (500). No se agregó reintento.
8. **Reglas de seguridad (6.3):** preferir el vínculo directo no cambia hoy ningún resultado observable, porque el vínculo siempre es del mismo ejercicio (T6) y R-LIM no admite ejercicios repetidos; el cambio deja preparada la 7c.
9. **Listado de rutinas pendientes de revisión:** `RevisionPrescripcionService` solo admitía el cambio de 6.4, así que la pantalla de revisión no muestra todavía rol, tempo ni carga sugerida (sí el historial del socio).
10. **Socios de demostración** (`DemoSeeder`, no modificado) quedan con origen `Real`.

## Pendientes

| # | Pendiente | Responsable |
|---|---|---|
| 1 | ~~Aplicar la migración en `gymbro-db`~~ Hecho por el autor el 24/11/2026; siembra verificada (`etapa7d-siembra-verificacion.txt`) | Autor |
| 2 | ~~Ejecutar el paso 2 de `clasificar-origen-datos.sql`~~ Hecho por el autor el 24/11/2026. El perfil de validación quedó en `ValidacionFuncional`; los 747 socios restantes, en `DatasetExterno`; las 12 prescripciones anteriores, en `DatasetExterno`, porque el origen se copia solo al generar. Ver `etapa7d-origen-datos-verificacion.txt`. Se ejecutó el `UPDATE` directo porque la primera corrida conservaba el texto `REEMPLAZAR_POR_TU_ID` y no cambió nada; ese `UPDATE` no actualiza `fecha_modificacion`. El paso 3 (marcar socios `Sintetico`) **no** se ejecutó: hoy hay 14 socios sin la firma del dataset (el Paso 0b indicaba 13) y su naturaleza está sin confirmar | Autor |
| 3 | Copiar al repositorio `docs/pfc/scripts/corregir-catalogo-piernas.sql` (ya ejecutado, pero su texto no llegó a Claude Code) y aportar `etapa7d-catalogo-antes.txt` | Autor / Claude Web |
| 4 | Prueba manual en pantalla (tarjeta de piernas, limitaciones y tarjeta de ejercicio) con el perfil de validación | Autor |
| 5 | Revisión de parámetros y contraindicaciones por la Personal Trainer; incluye la consecuencia del desvío 3 | Autor |
| 6 | Literatura revisada por pares para volumen, tempo excéntrico y proximidad al fallo | Claude Web |
| 7 | Protocolo del estudio piloto v0.3 | Claude Web |
| 8 | Etapa 7e (datos sintéticos, regla de una generación por semana, validar edad: hay dos socios con 96 años) | Claude Web |
| 9 | Etapa 7c (armado y edición de ejercicios, regla A3) y pantallas de KPIs (Etapa 12) | Claude Web |
| 10 | Revisión de Claude Web, push e integración a `main` (sin autorización todavía) | Autor |

## Trazabilidad objetivo → requisito → componente → prueba → evidencia

| Objetivo | Requisito | Componente | Prueba | Evidencia |
|---|---|---|---|---|
| Rutina solo con los 10 curados | CA1 | `ejercicios_protocolo`, `ProtocoloPiernasRepository.GetFilasProtocoloAsync`, `PlanificadorPiernas` | `Genera_seis_ejercicios_del_protocolo…` (excluye el ejercicio Legs del dataset) | `etapa7d-dotnet-test.txt` |
| Determinismo y rotación | CA2 | `PlanificadorPiernas` (sin reloj ni azar) | `Dos_corridas…`, `El_orden_de_los_candidatos…`, 3 teorías de rotación, `Dos_generaciones_con_las_mismas_entradas…` | idem |
| Exclusión por limitación con motivo | CA3 | `Exclusiones`, `MotivoSeleccion`, `LimitacionesArticularesService` | teoría de 9 combinaciones, `Rodilla_moderada…`, `Lumbar_…`, `Las_limitaciones_se_guardan_y_excluyen…` | idem |
| Reducción por alerta | CA4 | paso 5 del planificador, `GetAlertasVigentesAsync` | `Con_alerta_vigente…`, `La_reduccion_por_alerta_redondea…`, snapshot con alerta | idem |
| Sin RIR 0 y R-LIM | CA5 | paso 7 y 9b, `IValidadorPrescripcionService` | `Con_buena_adherencia…_nunca_a_0`, `Un_principiante…`, `Una_violacion_de_R_LIM…` | idem |
| Trazabilidad persistida | CA6 | columnas nuevas, `PrescripcionPiernasService` | `Copia_el_origen…`, `El_snapshot…`, prueba de integración de persistencia | idem y `etapa7d-migracion.sql.txt` |
| Vínculo sesión → línea | CA7 | `SesionEntrenamientoService.ResolverLinea`, `GetComparacionAsync` | `VinculoSesionLineaTests`, `La_sesion_guarda_el_vinculo…`, `Una_sesion_anterior_sin_vinculo…` | idem |
| KPIs con verdad conocida y filtro sintético | CA8 | `KpisProgresion`, `KpiProgresionService` | `KpisProgresionTests`, `Los_KPIs_solo_incluyen_socios_sinteticos` | idem |
| Regresión | CA9 | baseline y demás sin cambios | suites existentes completas | `etapa7d-base-dotnet-test.txt` y `etapa7d-dotnet-test.txt` |
| Ley 25.326 y aislamiento | CA10 | `[Authorize]` sin SuperAdministrador, `SocioAccessService`, filtros de tenant | filas nuevas de `MatrizAutorizacionTests` | `etapa7d-dotnet-test.txt` |
| Informe y evidencia | CA11 | este documento | — | `docs/pfc/evidencia/etapa7d-*` |

## Operaciones sobre la base local `gymbro-db`

Solo lecturas: dos `SELECT` de verificación (catálogo y C4/B13) y una lectura del historial de migraciones que hizo `dotnet ef migrations remove` al intentar descartar una migración vacía generada por error (la herramienta se negó y no modificó nada; los archivos se borraron a mano). No se ejecutó ningún `INSERT`, `UPDATE`, `DELETE` ni migración, y no se levantó la API contra esa base.

# Etapa 8b — Bienestar diario del Socio (especificación para Claude Code)

**Fecha:** 17/11/2026. **Estado:** aprobada por el autor (D1–D6); sin implementar. Este documento es autocontenido y **prevalece** sobre cualquier versión anterior (en particular: estrés y fatiga son **1–7**, no 1–10).
**Depende de:** Etapa 8 (PWA del Socio), 8c y 8c-bis (en `main`). **Habilita:** datos de entrada de la Etapa 10 y de la Etapa 12.

## 1. Por qué (trazabilidad con el Anexo II)
- Módulo PWA: el socio registra horas de sueño de la noche anterior, estrés y fatiga autopercibidos, peso actual e información nutricional.
- Módulo de Análisis Estadístico: estadística descriptiva e inferencial (Pearson, ANOVA) sobre la influencia de variables biológicas diarias en el rendimiento y el RIR.
- Resultado esperado: PWA que capture los registros diarios, incluidos factores biológicos y nutricionales.
- Respaldo bibliográfico del valor de registrar estrés y bienestar: Franco Jiménez et al. (2011), estudio cualitativo con 22 entrevistas; es fundamentación, no evidencia de eficacia.

## 2. Decisión D2 (cerrada el 17/11/2026): escala 1–7 estilo Hooper
- Estrés y fatiga: entero de **1 a 7** (1 = mejor, 7 = peor). Las anclas de texto en español son diseño propio.
- Fuente verificada: Hooper SL, MacKinnon LT, Howard A, Gordon RD, Bachmann AW. "Markers for monitoring overtraining and recovery". *Med Sci Sports Exerc* 1995;27(1):106-112 (PubMed 7898325). Califica sueño, fatiga, estrés y dolor muscular en 1–7.
- Cautela: el estudio es de 14 nadadores de élite. Se cita como antecedente de la escala, no como validación para socios de gimnasio.

## 3. Estado actual del código (informado el 17/11/2026; **verificar**)
| Variable | Estado |
|---|---|
| Sueño | `MedicionFisica.HorasSuenoUltimaNoche` (decimal), sin captura en la PWA |
| Estrés | `MedicionFisica.NivelEstres` (int, comentario 1–10), sin validación ni captura |
| Fatiga | No existe |
| Peso | Capturado en la Etapa 8 como medición física con altura e IMC, sin clave por día |
| Nutrición | No existe (solo `IngestaAguaLitros` en `MedicionFisica`, sin captura) |
| FC en reposo | `MedicionFisica.FrecuenciaCardiacaReposo`, sin captura en la PWA |
Reutilizar `MedicionFisica` mezcla datos esporádicos con diarios, no tiene unicidad por socio y día, y `0` no distingue "sin dato" de un valor real.

## 4. Alcance
**Entra:** entidad nueva de registro diario, API de alta/edición y consulta, una pantalla en `/mi` (menos de 30 segundos), pruebas, evidencia.
**No entra:** gráficos de evolución, recordatorios, uso de las variables para modificar la prescripción (Etapa 10), análisis del texto libre, wearables, edición por Personal Trainer o Dueño. **No tocar** `MedicionFisica` (sus columnas `NivelEstres` y `HorasSuenoUltimaNoche` quedan como legado sin uso), prescripción, baseline ni reglas.

## 5. Modelo de datos
Tabla `registros_bienestar_diario` (hereda `EntidadAuditable`; snake_case).
| Campo | Tipo | Regla |
|---|---|---|
| `id` | guid | — |
| `socio_id` | guid | FK a `socios`; filtro global igual al de `MedicionFisica` |
| `fecha` | date | Día local del socio (`America/Argentina/Buenos_Aires`), calculado en el servidor. Único por `(socio_id, fecha)` entre filas no dadas de baja: índice parcial `WHERE fecha_baja IS NULL` |
| `horas_sueno` | decimal(3,1) nullable | 0–24, paso 0,5 |
| `estres` | smallint nullable | 1–7 (`CHECK` en la base y validación en el servicio) |
| `fatiga` | smallint nullable | 1–7, ídem |
| `peso_kg` | decimal(5,1) nullable | mismo rango que "Mi medición" |
| `nutricion_categoria` | smallint nullable | Baja / Habitual / Alta respecto de lo habitual |
| `ingesta_agua_litros` | decimal(3,1) nullable | 0–10, opcional |
| `nutricion_notas` | varchar(500) nullable | texto libre; no se analiza |
| `frecuencia_cardiaca_reposo` | smallint nullable | opcional; **proponer un rango fisiológico justificado** y señalarlo como decisión de diseño |
Todas las variables son opcionales, pero se exige al menos una.
**Vínculo con la sesión:** sin clave foránea; se asocia por `(socio_id, fecha local de SesionEntrenamiento.Inicio)`. Registrar una sesión nunca queda bloqueado por no haber cargado el bienestar.

## 6. API
- `PUT /api/bienestar/{fecha}` (`fecha` en formato `yyyy-MM-dd`): alta o actualización del registro de ese día (idempotente). Solo se aceptan **hoy o ayer según el día local calculado en el servidor**; cualquier otra fecha, 400. Solo rol `Socio`, sobre su propio `socio_id` (del JWT, no del cuerpo). *(Decisión A, 17/11/2026: reemplaza a `PUT /api/bienestar/hoy`, que no permitía corregir el día anterior.)*
- `GET /api/bienestar/hoy`: solo rol `Socio`. Responde **200** con `{ fechaLocal, registro }`, donde `fechaLocal` es el día local del servidor y `registro` es `null` si todavía no cargó el de hoy (no 204). La PWA usa `fechaLocal` y no depende del reloj del celular. *(Decisión A.)*
- `GET /api/bienestar?socioId&desde&hasta`: historial. Socio propio; Personal Trainer y Dueño solo de sus socios, vía `ISocioAccessService`. **El Administrador de la Plataforma no accede** (datos de salud; decisión aprobada).
- Consentimiento (`ConsentimientoDatosSaludFecha`) *(Decisión C, 17/11/2026)*:
  - `PUT`: 400 si el socio no tiene consentimiento.
  - `GET` del propio Socio sin consentimiento: 200 con lista vacía (o `registro` nulo en `/hoy`).
  - `GET` de Personal Trainer o Dueño sobre un socio sin consentimiento vigente: 400.
  - Criterio de error copiado de `PrescripcionBaselineService.GenerarAsync` (`ReglaNegocioException`, 400), el único control de consentimiento existente. **Diferencia informada:** hoy las lecturas y altas de `MedicionFisica` y de sesiones no controlan el consentimiento; queda fuera del alcance de esta etapa.
- Ventana de edición: hoy y ayer. Fuera de ella, 400.
- Contrato de rechazo (8c-bis T7): recurso no visible para quien llama = 404; 403 si es visible y el rol no puede realizar la acción.
- Los errores se traducen en el manejador global, sin devolver `ex.Message`.
- Agregar las celdas de los dos endpoints a la matriz de autorización (rol × endpoint).

## 7. PWA
Tarjeta "¿Cómo estás hoy?" en la pantalla Hoy (`/mi`) que abre una pantalla única en **`/mi/bienestar`** (solo rol `Socio`) *(Decisión B, 17/11/2026: en el código la pantalla Hoy es `/mi`; no existe `/mi/hoy`)*: sueño (paso 0,5 h), estrés y fatiga (7 botones con anclas escritas en ambos extremos), peso (opcional), ingesta (3 opciones), agua, FC en reposo y nota opcionales. Si ya existe el registro del día, se muestra para editarlo. Uso con una mano, sin teclado salvo peso, FC y nota. Sin conexión se conserva el borrador local como en `borrador-sesion.ts`; el envío requiere red. Interfaz simple y no cargada (preferencia del autor).

## 8. Condiciones iniciales: verificar y reportar antes de escribir código
1. `main` en `e411e3e`, única rama; `git status` limpio salvo archivos sin versionar del autor (`Nuevo Documento de texto.txt` no debe entrar).
2. Línea base: `dotnet build`, `dotnet test`, `ng build`, `ng test`; registrar cifras. Última informada: 46 unitarias, 149 de integración, 109 del cliente.
3. Leer: `MedicionFisica`, `ISocioAccessService` / `SocioAccessService`, `SesionesAislamientoTests`, el manejador global de errores, `borrador-sesion.ts`, `mi-mediciones`, `MatrizAutorizacionTests`.
4. **Confirmar cómo se guarda `SesionEntrenamiento.Inicio`** (UTC o local, cómo lo envía el cliente) y qué abstracción de reloj existe (`TimeProvider` u otra). Si no hay, introducir `TimeProvider` solo para esta etapa.
5. Si algo contradice este documento, detenerse y proponer (regla 19.2). No decidir unilateralmente.

## 9. Orden de trabajo
1. Dominio + migración + configuración EF (filtro global, índice parcial, `CHECK`).
2. Servicio de aplicación con reloj inyectado + pruebas unitarias.
3. Controlador y manejo de errores + pruebas de integración (aislamiento entre dos gimnasios, unicidad contra PostgreSQL, consentimiento).
4. Matriz de autorización ampliada.
5. Pantalla `/mi/hoy`, servicio del cliente, borrador local y specs.
6. Evidencia en `docs/pfc/evidencia/etapa8b-*.txt` (salidas de build y pruebas con fecha y commit), como en 8c.
7. Texto para el informe (4.5 y 4.6) y registro de decisiones.

## 10. Criterios de aceptación
| Criterio | Verificación |
|---|---|
| Un Socio no lee ni escribe registros de otro | Integración (2 socios, 2 gimnasios), como `SesionesAislamientoTests` |
| Sin consentimiento no se guarda (400) | Unitaria e integración |
| Dos `PUT` el mismo día actualizan, no duplican | Integración contra PostgreSQL |
| Rangos (estrés y fatiga 1–7) y "al menos una variable" | Unitarias del servicio |
| 23:30 local es el día local, no el UTC siguiente | Unitaria con reloj simulado |
| Ventana hoy/ayer | Unitaria |
| El Administrador de la Plataforma no accede (403 o 404 según T7) | Matriz de autorización |
| La pantalla guarda, recarga y edita el registro del día | Specs de componente y prueba manual |
| Ningún endpoint devuelve `ex.Message` | Revisión + prueba |
| `dotnet build`, `dotnet test`, `ng build`, `ng test` sin errores nuevos | Evidencia versionada |

## 11. Informe esperado
Archivos modificados; migración generada (nombre y SQL relevante); decisiones tomadas (reloj, rango de FC); cifras de build y pruebas antes y después; desvíos; qué no se pudo verificar (por ejemplo, prueba manual en pantalla). Rama `etapa8b`, **sin merge ni push a `main`** hasta la revisión de Claude Web.

## 12. Notas para la Etapa 12 (no implementar ahora)
- Series dentro de sesiones dentro de socios no son observaciones independientes: agregar por sesión y declarar supuestos.
- RIR es ordinal y autopercibido: complementar Pearson con Spearman; correlación no es causalidad.
- Con 2 sesiones de prueba no hay muestra; todo dato generado o importado se rotula como tal.
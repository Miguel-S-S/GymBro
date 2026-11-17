# Etapa 8b — Bienestar diario del Socio: texto para el informe y registro de decisiones

**Fecha:** 17/11/2026. **Rama:** `etapa8b` (sin merge a `main` hasta la revisión). **Especificación:** `docs/pfc/etapas/etapa-8b-bienestar-diario.md` (con las decisiones A, B y C).
**Evidencia:** `docs/pfc/evidencia/etapa8b-*.txt` (build, pruebas y SQL de la migración, con fecha y commit).

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas; prueba manual en un dispositivo, pendiente.**

Se incorporó el registro diario de bienestar del socio como una entidad propia, `RegistroBienestarDiario` (tabla `registros_bienestar_diario`), separada de `MedicionFisica`. Esta última mezclaba datos esporádicos con diarios, no tenía unicidad por socio y día y no distinguía "sin dato" de un valor real. En el registro nuevo todas las variables son opcionales y anulables, pero se exige al menos una:

- horas de sueño de la noche anterior (0–24, de a media hora);
- estrés y fatiga autopercibidos en una escala de 1 a 7 al estilo Hooper (1 = mejor, 7 = peor);
- peso (1–400 kg);
- ingesta del día respecto de lo habitual (baja, habitual o alta);
- agua (0–10 L);
- frecuencia cardíaca en reposo (30–120 lpm);
- una nota libre que no se analiza.

La base garantiza un registro por socio y día mediante un índice único parcial (`WHERE fecha_baja IS NULL`) y restricciones `CHECK` para cada rango. El aislamiento entre gimnasios usa el mismo filtro global que `MedicionFisica`.

El día del registro lo calcula el servidor en la zona `America/Argentina/Buenos_Aires`, con un reloj inyectable (`TimeProvider`) que permite probar casos de borde como las 23:30 locales, que en UTC ya son el día siguiente. La API expone tres operaciones:

- `GET /api/bienestar/hoy`: devuelve el día local del servidor y el registro de ese día.
- `PUT /api/bienestar/{fecha}`: alta o actualización idempotente, solo para hoy o ayer.
- `GET /api/bienestar?socioId&desde&hasta`: historial.

El socio solo escribe sobre su propio registro: el identificador sale del token, no del cuerpo. El Personal Trainer y el Dueño leen el historial de sus socios mediante `ISocioAccessService`. El Administrador de la Plataforma no accede, por tratarse de datos de salud (Ley 25.326).

En la PWA, la pantalla Hoy (`/mi`) muestra la tarjeta "¿Cómo estás hoy?", que abre `/mi/bienestar`. Esa pantalla está pensada para completarse en menos de 30 segundos con una mano: botones − / + para sueño y agua, siete botones con anclas escritas para estrés y fatiga, y tres opciones para la ingesta. Solo usan teclado el peso, el pulso y la nota. Si el registro del día ya existe, se muestra para editarlo. Sin conexión, lo cargado se conserva como borrador local del día, igual que la sesión en curso, pero el envío requiere red. El borrador se elimina al guardar y al cerrar sesión.

La asociación con las sesiones de entrenamiento no usa clave foránea: se hace por socio y por día local de `SesionEntrenamiento.Inicio`, que se guarda como `timestamptz` en UTC (`DiaLocal.DeUtc`). Registrar una sesión nunca depende de haber cargado el bienestar.

El uso de estas variables para ajustar la prescripción (Etapa 10) y su análisis estadístico (Etapa 12) quedan como **trabajo futuro**.

## Texto propuesto para 4.6 (verificación)

| Criterio (sección 10) | Resultado | Verificación |
|---|---|---|
| Un Socio no lee ni escribe registros de otro | Implementado y verificado | Integración con dos gimnasios (`BienestarTests`): lectura de otro gimnasio → 404; un `socioId` colado en el cuerpo se ignora |
| Sin consentimiento no se guarda (400) | Implementado y verificado | Unitarias e integración; además, lectura del propio socio → 200 vacío y lectura del Dueño → 400 (decisión C) |
| Dos `PUT` del mismo día actualizan, no duplican | Implementado y verificado | Integración contra PostgreSQL: misma fila; el índice único parcial rechaza un duplicado vigente y admite uno dado de baja |
| Rangos (estrés y fatiga 1–7) y "al menos una variable" | Implementado y verificado | Unitarias del servicio (17 casos inválidos y los extremos) y `CHECK` de la base |
| 23:30 local es el día local, no el UTC siguiente | Implementado y verificado | Unitaria con reloj simulado; cambio de día a las 00:00 locales (03:00 UTC) |
| Ventana hoy/ayer | Implementado y verificado | Unitaria (hoy y ayer sí; anteayer y mañana, 400) e integración |
| El Administrador de la Plataforma no accede | Implementado y verificado | Matriz de autorización: 403 en los tres endpoints (`[Authorize]`), coherente con T7 |
| La pantalla guarda, recarga y edita el registro del día | Parcial | Specs de componente en verde; **prueba manual en un dispositivo, pendiente** |
| Ningún endpoint devuelve `ex.Message` interno | Implementado y verificado | Errores por el manejador global; la carrera de dos altas se traduce a un 400 sin el detalle de PostgreSQL (prueba) |
| Build y pruebas sin errores nuevos | Implementado y verificado | Evidencia versionada: 83 unitarias, 183 de integración y 135 del cliente; 0 errores |

## Registro de decisiones de la etapa

| # | Decisión | Motivo | Estado |
|---|---|---|---|
| D2 | Estrés y fatiga en escala 1–7 estilo Hooper (1 = mejor). Anclas propias: "Sin estrés" / "Estrés máximo" y "Sin fatiga" / "Fatiga máxima" | Especificación (Hooper et al., 1995, como antecedente; no validada en socios de gimnasio) | Aprobada por el autor |
| A | `PUT /api/bienestar/{fecha}` (hoy o ayer) y `GET /api/bienestar/hoy` → 200 `{ fechaLocal, registro }` | El `PUT …/hoy` original no permitía corregir el día anterior | Aprobada (Claude Web, 17/11/2026) |
| B | Pantalla en `/mi/bienestar`; tarjeta en Hoy (`/mi`) | En el código la pantalla Hoy es `/mi`; `/mi/hoy` no existe | Aprobada (Claude Web, 17/11/2026) |
| C | Consentimiento: `PUT` → 400; `GET` del propio socio → 200 vacío; `GET` del personal → 400 | Mismo criterio de error que `PrescripcionBaselineService` (único control de consentimiento existente) | Aprobada (Claude Web, 17/11/2026) |
| R1 | Reloj: `TimeProvider` inyectado (registrado como `TimeProvider.System`) solo para esta etapa; zona `America/Argentina/Buenos_Aires` | No existía una abstracción de reloj (el código usa `DateTime.UtcNow`) | Decisión de implementación |
| R2 | Frecuencia cardíaca en reposo: 30–120 lpm | **Decisión de diseño, no un criterio clínico:** admite la bradicardia de deportistas entrenados y valores por encima del rango habitual en reposo, y rechaza errores evidentes de tipeo (0, 9, 600). Revisable | Propuesta, a validar por el autor |
| R3 | El `PUT` reemplaza el registro completo: un dato que no se envía queda como "sin dato" | Idempotencia; la pantalla siempre envía el formulario entero | Decisión de implementación |
| R4 | Historial por defecto de 30 días y máximo de 366 por consulta | Limitar el volumen de una consulta | Decisión de implementación |
| R5 | Dos altas simultáneas del mismo día → 400 con mensaje propio | El índice único lo impide en la base; se traduce sin exponer el error de PostgreSQL | Decisión de implementación |
| R6 | El borrador local se guarda con su fecha; cerrar sesión lo borra junto con el de la sesión de entrenamiento | No aplicar el borrador de ayer hoy; son datos de salud en el dispositivo | Decisión de implementación |

## Diferencias y pendientes informados

- **Consentimiento en datos existentes:** las lecturas y altas de `MedicionFisica` y de sesiones no controlan el consentimiento. Queda fuera del alcance de esta etapa.
- **Peso en "Mi medición":** solo se valida en el cliente (1–400); `MedicionCreateDto` no valida rangos. Para el bienestar, el rango se valida en el servidor y en la base.
- **Migración en la base local:** `20261117203130_Etapa8b_BienestarDiario` se aplica al iniciar la API (`MigrateAsync`). Solo se ejecutó contra los contenedores de prueba, no contra `gymbro-db`.
- **Sin verificar:**
  - prueba manual en pantalla y en un celular (uso con una mano, tiempo menor a 30 segundos, sin conexión real);
  - la PWA instalada.

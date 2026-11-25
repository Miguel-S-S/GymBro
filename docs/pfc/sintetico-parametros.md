# Parámetros del generador sintético (Etapa 7e)

> **Datos sintéticos (`Sintetico`).** Todos los valores de este documento son **decisiones de diseño del autor, sin fuente bibliográfica ni validación clínica**. Sirven para producir datos con una verdad conocida y verificar el procedimiento (KPIs, analítica, modelos), no para describir personas. Ninguna cifra obtenida con estos datos dice algo sobre el desempeño con personas reales.

Código: `GymBro.Application/Sintetico/ParametrosSinteticos.cs` (valores por defecto) y `GymBro.Application/Sintetico/AzarDeterminista.cs` (`ModeloSintetico`, funciones del modelo). Generador: `GymBro.Infrastructure/Sintetico/GeneradorSintetico.cs`. Si se cambia un valor por defecto en el código, hay que actualizar esta tabla.

## Uso

Solo en el entorno `Development` y con una opción explícita; en ese modo la API no atiende pedidos HTTP, aplica las migraciones a la base configurada, genera o elimina y termina. **Usar una base descartable o la base de desarrollo del autor, nunca una base con datos de personas.**

```
dotnet run --project GymBro.Api -- --generar-sintetico [--semilla 42] [--socios 60] [--semanas 12] [--inicio 2026-01-05]
dotnet run --project GymBro.Api -- --eliminar-sintetico
```

Fuera de `Development` el proceso termina con código 2 antes de leer la configuración de la base (verificado: `docs/pfc/evidencia/etapa7e-produccion-rechazo.txt`).

## Qué crea y con qué servicios

| Dato | Cómo se crea |
|---|---|
| Gimnasio (código `SINT` + semilla en hexadecimal, sin revisión humana de rutinas) y su Dueño | Alta directa con ids derivados de la semilla (`IAlmacenSintetico`) |
| Socios (`origen_datos = Sintetico`, con consentimiento) y sus cuentas (sin contraseña utilizable) | Alta directa con ids derivados de la semilla |
| Medición inicial | `IMedicionService` |
| Limitaciones articulares | `ILimitacionesArticularesService` |
| Rutina de piernas (una generación por semana, cada lunes) | `IPrescripcionPiernasService` (planificador real, validador R-LIM, política de revisión) |
| Sesión y series | `ISesionEntrenamientoService` (dispara R-RIR-1 y R-PROG-1) |
| Bienestar diario con FC de reposo | `IBienestarService` (dispara R-FC-1) |
| Asistencia (método `Manual`, registrada por el Dueño) | `IAsistenciaService` |
| Cuotas del mes y pagos | `ICuotaService` |

El generador asume la identidad del Dueño (o del Socio para su bienestar) con un `IUsuarioActual` simulado y fija un reloj simulado (`TimeProvider`) en cada paso, porque los servicios calculan "hoy" con el reloj. Los ids que crean internamente los servicios (sesiones, series, rutinas, alertas) son aleatorios: el resumen de reproducibilidad (SHA-256) se calcula sobre el contenido, sin esos ids (ver desvíos en el informe de la etapa).

## Calendario

| Parámetro | Valor por defecto |
|---|---|
| Semilla | 42 |
| Socios | 60 |
| Semanas | 12 |
| Sesiones programadas por semana | 2 (días fijos por socio, elegidos al azar entre lunes y sábado) |
| Inicio | lunes 05/01/2026 (fijo para que la misma semilla dé los mismos datos) |

## Perfil de los socios

| Parámetro | Valor |
|---|---|
| Edad | uniforme entre 18 y 60 años |
| Nivel (Principiante, Intermedio, Avanzado) | 0,4 / 0,4 / 0,2 |
| Género | 0,5 / 0,5 |
| Limitación de rodilla moderada | 0,15 (no se generan limitaciones altas: con rodilla alta el protocolo no tiene cuádriceps) |
| Limitación lumbar moderada | 0,15 |
| Antigüedad en el gimnasio (fecha de alta) | uniforme entre 0 y 720 días antes del inicio |
| Peso y altura iniciales | N(62; 9) kg y N(163; 7) cm en mujeres; N(78; 9) kg y N(176; 7) cm en varones |

## Carga

| Parámetro | Valor |
|---|---|
| Carga inicial de un multiarticular por nivel | 40 / 70 / 100 kg |
| Factor de aislamiento / pantorrillas | 0,5 / 0,7 |
| Factor individual | uniforme entre 0,8 y 1,2 |
| **Ganancia semanal de carga por nivel** | **0,020 / 0,010 / 0,005** (compuesta: carga = inicial × (1 + g)^semanas) |
| Ruido de la carga por serie | multiplicativo, desvío 0,03 |
| Redondeo de la carga registrada | 0,5 kg |
| Calentamiento | una serie en el primer ejercicio, 50 % de la carga, 10 repeticiones, sin RIR |

El socio sintético **no sigue** la carga sugerida por el planificador ni las sugerencias de R-PROG-1: su carga es la del modelo. Así K1 mide el parámetro conocido y no la regla.

## Repeticiones, RIR y series

| Parámetro | Valor |
|---|---|
| Repeticiones por serie | redondeo de N(centro del rango prescrito + 0; 2), entre 1 y máximo + 5 |
| **Sesgo del RIR respecto del objetivo** | **+0,5** (más reserva que la planificada) |
| Desvío del RIR | 1,0 (redondeado y recortado a 0–5) |
| Efecto de la fatiga del día sobre el RIR | +0,2 por punto por encima de 4 (escala 1 a 7) |
| Efecto de la ingesta del día sobre el RIR (Baja / Habitual / Alta) | +0,5 / 0 / −0,25 |
| **Probabilidad de completar todas las series prescritas** | **0,85** (si no, falta una serie de un ejercicio al azar) |

## Adherencia y abandono

| Parámetro | Valor |
|---|---|
| Adherencia individual (probabilidad de asistir a cada sesión programada) | uniforme entre 0,5 y 1,0 |
| Riesgo semanal de abandono (cada domingo) | logística(−5 + 4 × (1 − adherencia realizada de las últimas 4 semanas) + 1,5 × moroso) |
| Efecto del abandono | no hay más sesiones, asistencias, bienestar ni pagos; el estado administrativo sigue `Activo` (las cuotas se siguen generando) |

## Cuotas

| Parámetro | Valor |
|---|---|
| Monto | 15 000 |
| Vencimiento | día 10 de cada mes |
| Pago puntual | 0,75 (un día al azar entre la generación y el vencimiento) |
| Si no paga a tiempo, pago tardío | 0,6 (entre 1 y 20 días después del vencimiento); si no, no paga |
| Moroso (para el abandono) | tiene una cuota con vencimiento anterior al día y sin pagar |

## Bienestar

| Parámetro | Valor |
|---|---|
| Probabilidad de cargar el registro cada día | 0,6 |
| FC de reposo | media por socio N(62; 6); diaria N(media del socio; 3), entre 40 y 100 lpm |
| Fatiga del día (latente, se usa aunque no se registre) | redondeo de N(4; 1,2), entre 1 y 7 |
| Ingesta del día (Baja / Habitual / Alta) | 0,2 / 0,6 / 0,2 |
| Sueño, estrés, agua, peso | N(7; 1) h de a media hora; N(3,5; 1,2) en 1–7; N(2; 0,5) l; peso inicial + N(0; 0,3) kg |

## Horarios

Franja de inicio de la sesión por socio (mañana 7 a 9, mediodía 12 a 14, tarde 18 a 21): 0,3 / 0,2 / 0,5; el minuto se elige al azar de a 5 minutos. La asistencia se registra 5 minutos antes del inicio.

## Recuperación de parámetros con K1–K4 y tolerancias

Declaradas **antes** de ejecutar las pruebas (`GymBro.Tests.Integration/GeneradorSinteticoTests.cs`). Los KPIs se calculan con el servicio real (`IKpiProgresionService`) sobre los datos generados, y se comparan con el valor esperado que se deduce de los parámetros (`ModeloSintetico`, verificado a su vez contra simulaciones de 200 000 casos en `GymBro.Tests/GeneradorSinteticoModeloTests.cs`).

| KPI | Qué se compara | Tolerancia |
|---|---|---|
| K1 | Mediana, por nivel, de exp(pendiente de ln(carga máxima) contra semanas) − 1, por socio y ejercicio con al menos 4 apariciones, contra la ganancia semanal del nivel | ± 0,005 |
| K2 | Volumen total de K2 contra la suma de peso × repeticiones de las series efectivas en la base | igualdad exacta |
| K3 | Porcentaje de series en rango (ponderado por series) contra P(redondeo de N(centro; 2) dentro del rango) | ± 0,05 |
| K3 | Desvío RIR medio (ponderado) contra la esperanza del desvío con fatiga e ingesta latentes, redondeo y recorte | ± 0,15 |
| K4 | Proporción de sesiones con todas las series prescritas contra 0,85 | ± 0,08 |

El índice de seguridad de K4 (sesiones limpias) depende además de las alertas que emiten las reglas reales; no tiene un parámetro único que recuperar y se informa solo como valor descriptivo.

# Requisitos y trazabilidad de GymBro

> Generado desde el código, las pruebas y la evidencia de la rama **`cierre-pfc`**. Los requisitos se reconstruyen a partir de lo implementado y de los pedidos de cada etapa (`docs/pfc/etapas/*-informe.md`); no se agregan requisitos que no estén en el código. Pruebas en `GymBro.Tests` (unitarias), `GymBro.Tests.Integration` (integración), `GymBro.Web/src/**/*.spec.ts` (cliente) y `GymBro.IA/**/test_*.py`, `GymBro.Analytics/tests` (Python).

## Requisitos funcionales (RF) y no funcionales (RNF)

| Id | Requisito |
|---|---|
| RF-01 | Autenticación con JWT y cuatro roles; registro de socios e invitaciones de un solo uso |
| RF-02 | Multigimnasio: cada usuario de gimnasio ve solo su gimnasio; el socio, solo lo suyo |
| RF-03 | Generar rutinas (baseline y protocolo de piernas determinista) con revisión humana opcional según el gimnasio |
| RF-04 | Registrar sesiones y series solo bajo una rutina vigente (Aprobada o Editada) del mismo socio, con RIR |
| RF-05 | Comparar lo prescrito con lo ejecutado mediante indicadores por ejercicio, sin veredicto global |
| RF-06 | Registro diario de bienestar (con FC de reposo), uno por día local |
| RF-07 | Reglas deterministas de seguridad (R-RIR-1, R-PROG-1, R-FC-1) y límites de prescripción (R-LIM) |
| RF-08 | Catálogo con ejercicios globales y propios, equipamiento del gimnasio y búsqueda semántica con filtros duros |
| RF-09 | Sugerir sustitutos con un modelo local solo entre candidatos recuperados, verificados en código |
| RF-10 | Asistencia con QR firmado de corta duración y registro manual, una por socio y día |
| RF-11 | Cuotas mensuales, pago manual y morosidad derivada |
| RF-12 | KPIs del estudio (K1 a K4) |
| RF-13 | Generador sintético reproducible que usa los servicios reales |
| RF-14 | Modelo de abandono con separación temporal y métricas con intervalos |
| RF-15 | Aplicación de análisis (descriptiva, correlación, ANOVA, tableros) |
| RNF-01 | Ley 25.326: el Administrador de la Plataforma no accede a datos de salud; consentimiento para tratarlos |
| RNF-02 | Sin secretos en el repositorio |
| RNF-03 | Datos sintéticos rotulados; la analítica solo usa datos `Sintetico` |
| RNF-04 | PWA instalable con service worker |
| RNF-05 | Contenedores, CI y manual de despliegue |

## Tabla de trazabilidad

| Objetivo | Requisito | Componente | Prueba | Evidencia |
|---|---|---|---|---|
| Acceso seguro y por rol | RF-01 | `AuthController`, `AuthService`, `GeneradorTokenJwt`, `InvitacionService` | `MatrizAutorizacionTests`, pruebas de invitación del cliente (`activar-invitacion.spec.ts`) | `etapa13-dotnet-test.txt` |
| Aislamiento entre gimnasios | RF-02 | filtros de `GymBroDbContext`, `SocioAccessService`, `GimnasioDestino` | `MatrizAutorizacionTests`, `SesionesAislamientoTests`, `AsistenciaCuotasTests` (aislamiento), `BusquedaSemanticaTests` | idem |
| Rutinas trazables | RF-03 | `PrescripcionBaselineService`, `PrescripcionPiernasService`, `PlanificadorPiernas`, `RevisionPrescripcionService` | `PlanificadorPiernasTests`, `ProtocoloPiernasIntegracionTests`, `RevisionDecisionTests` | `etapa7d-dotnet-test.txt` |
| Sesiones bajo rutina vigente | RF-04 | `SesionEntrenamientoService`, `PrescripcionEntrenamiento.EsVigente` | `SesionEntrenamientoServiceTests`, `SesionesAislamientoTests` | `etapa13-dotnet-test.txt` |
| Cumplimiento sin veredicto | RF-05 | `core/util/cumplimiento.ts`, `mi-historial.ts`, `sesion-detalle.ts` | `cumplimiento.spec.ts`, `sesion-detalle.spec.ts`, `mi-historial.spec.ts` | `etapa8e-ng-test.txt` |
| Bienestar diario | RF-06 | `BienestarService`, `DiaLocal` | `BienestarServiceTests`, `BienestarTests` | `etapa8b-*` |
| Seguridad del entrenamiento | RF-07 | `ReglasSeguridadService`, `ValidadorPrescripcion` | `ReglasSeguridadIntegracionTests`, `ValidadorPrescripcionTests` | `etapa10a-*` |
| Recuperación con filtros duros | RF-08 | `BuscadorSemanticoService`, `EquipamientoService`, `GymBro.IA/main.py` | `BusquedaSemanticaTests`, `CatalogoGimnasioTests`, `GymBro.IA/test_main.py` | `etapa9b1-*` |
| Medición de la recuperación | RF-08 | `GymBro.IA/evaluacion/evaluar_recuperacion.py`, `metricas.py` | `test_metricas.py` | `etapa9b1-python-test.txt` (sin cifras: PUNTO DE ESPERA 1) |
| Sustitutos verificables | RF-09 | `SugerenciaSustitutosService`, `GeneradorSustitutosIa`, `GymBro.IA/sustitutos.py`, `sustitutos-panel.ts` | `SugerenciaSustitutosTests`, `SugerenciaSustitutosInterpretarTests`, `test_sustitutos.py`, `sustitutos-panel.spec.ts` | `etapa9b2-*` (sin medición: PUNTO DE ESPERA 2) |
| Asistencia | RF-10 | `AsistenciaService`, `TokenAsistencia`, `mi-asistencia.ts`, `gestion/asistencia.ts` | `AsistenciaCuotasTests` (unitarias e integración), `qr.spec.ts`, `asistencia-cuotas.spec.ts` | `etapa11-*` |
| Cuotas y morosidad | RF-11 | `CuotaService`, `CuotaSocio.EstaVencida`, `gestion/cuotas.ts` | `AsistenciaCuotasTests`, `asistencia-cuotas.spec.ts` | `etapa11-*` |
| Indicadores del estudio | RF-12 | `KpisProgresion`, `KpiProgresionService` | `KpisProgresionTests`, `GeneradorSinteticoTests` (recuperación de parámetros) | `etapa7e-corrida-60x12.txt` |
| Datos con verdad conocida | RF-13 | `GeneradorSintetico`, `ModeloSintetico`, `AlmacenSintetico` | `GeneradorSinteticoTests`, `GeneradorSinteticoModeloTests` | `etapa7e-*` |
| Abandono | RF-14 | `GymBro.Analytics/gymbro_analytics/*`, `entrenar_abandono.py`, `puntuar_abandono.py`, `prediccion_abandono` | `test_abandono.py`, `RolAnaliticaTests`, `MigracionPrediccionAbandonoTests` | `etapa10b1-*`, `docs/pfc/analitica/reporte-abandono-20261125-213201.md` |
| Análisis estadístico | RF-15 | `GymBro.Analytics/app/principal.py`, `estadistica.py` | `test_estadistica.py`, `test_app.py` | `etapa12-*` |
| Privacidad de datos de salud | RNF-01 | `SocioAccessService`, filtros, consentimiento en `BienestarService` y `LimitacionesArticularesService` | `MatrizAutorizacionTests` (SA 403 en datos de salud) | `etapa13-dotnet-test.txt` |
| Secretos fuera del repositorio | RNF-02 | `appsettings.Development.example.json`, user-secrets, variables de entorno | `ConfiguracionEjemploTests` | idem |
| Datos sintéticos rotulados | RNF-03 | `OrigenDatos`, vistas `analitica.*`, `ENCABEZADO_SINTETICO` | `RolAnaliticaTests`, `GeneradorSinteticoTests` | `etapa10b1-ejecucion.txt` |
| PWA | RNF-04 | `ngsw-config.json`, `GymBro.Web/nginx.conf` | `ngsw-config.spec.ts` | `etapa13-ng-build.txt` |
| Despliegue reproducible | RNF-05 | Dockerfiles, `docker-compose.full.yml`, `.github/workflows/ci.yml`, `docs/despliegue.md` | `docker compose config`, validación de la CI | `etapa13-compose-config.txt`, `etapa13-ci-validacion.txt`, `etapa13-docker-build.txt` |

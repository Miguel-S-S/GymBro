# Estado de implementación de GymBro

> Generado desde el código, las pruebas y la evidencia de la rama **`cierre-pfc`** (Etapa 14). Categorías: **Implementado y verificado** (código y pruebas o ejecución guardada), **Parcial**, **Especificado** (pedido, no implementado), **Pendiente** (implementado, falta una acción externa), **Desviación** (difiere del pedido, con motivo) y **Trabajo futuro**.

## Implementado y verificado

| Funcionalidad | Dónde | Verificación |
|---|---|---|
| Autenticación JWT, cuatro roles, invitaciones, vinculación | `AuthController`, `InvitacionService`, `VinculacionService` | `MatrizAutorizacionTests`, `VinculacionesPlataformaTests` |
| Aislamiento multigimnasio y Ley 25.326 (la plataforma no accede a datos de salud) | `GymBroDbContext`, `SocioAccessService` | `MatrizAutorizacionTests` |
| Rutina baseline y protocolo de piernas determinista con revisión humana opcional | `PrescripcionBaselineService`, `PrescripcionPiernasService`, `RevisionPrescripcionService` | `PlanificadorPiernasTests`, `ProtocoloPiernasIntegracionTests`, `RevisionDecisionTests` |
| Sesiones solo con rutina vigente; RIR; vínculo con la línea prescrita | `SesionEntrenamientoService` | `SesionEntrenamientoServiceTests`, `VinculoSesionLineaTests` |
| Indicadores de cumplimiento por ejercicio (Socio, Dueño y Personal Trainer) | `core/util/cumplimiento.ts` | `cumplimiento.spec.ts`, `sesion-detalle.spec.ts` (Etapas 8d y 8e) |
| Bienestar diario con FC de reposo | `BienestarService` | `BienestarTests` |
| Reglas de seguridad R-RIR-1, R-PROG-1, R-FC-1 y límites R-LIM | `ReglasSeguridadService`, `ValidadorPrescripcion` | `ReglasSeguridadIntegracionTests` |
| Catálogo global y propio, equipamiento | `EjercicioGimnasioService`, `EquipamientoService` | `CatalogoGimnasioTests`, `AdministracionCatalogoTests` |
| Búsqueda semántica con filtros duros y consulta por el cuerpo | `BuscadorSemanticoService`, `GymBro.IA/main.py` | `BusquedaSemanticaTests`, `test_main.py` (Etapa 9b-1) |
| Sugerencia de sustitutos verificada en código (con generador falso) | `SugerenciaSustitutosService`, `sustitutos.py` | `SugerenciaSustitutosTests`, `test_sustitutos.py` (Etapa 9b-2) |
| Asistencia con QR firmado (2 minutos) y manual, idempotente | `AsistenciaService`, `TokenAsistencia` | `AsistenciaCuotasTests` (Etapa 11) |
| Cuotas, pago manual y morosidad derivada; plataforma en solo lectura | `CuotaService`, `CuotaSocio` | `AsistenciaCuotasTests` |
| KPIs K1 a K4 | `KpisProgresion` | `KpisProgresionTests`; recuperación de parámetros en `GeneradorSinteticoTests` |
| Generador sintético reproducible con los servicios reales; eliminación acotada | `GeneradorSintetico`, `AlmacenSintetico` | `GeneradorSinteticoTests`, `etapa7e-corrida-60x12.txt`, `etapa7e-reproducibilidad.txt` |
| Esquema `analitica` y roles de solo lectura y de puntuación | `rol-analitica.sql` | `RolAnaliticaTests` |
| Modelo de abandono (separación temporal, línea base, regresión logística, bosque aleatorio, intervalos) y puntuación por lotes | `GymBro.Analytics` | `test_abandono.py`, ejecución real (`etapa10b1-ejecucion.txt`) |
| Aplicación de análisis y tableros | `GymBro.Analytics/app/principal.py`, `estadistica.py` | `test_estadistica.py`, `test_app.py`, `etapa12-aplicacion.txt` |
| Comprobación de salud `/salud` | `Program.cs` | `MatrizAutorizacionTests` |
| CI sintácticamente válida | `.github/workflows/ci.yml` | `etapa13-ci-validacion.txt` (no se ejecutó en GitHub) |
| Configuración del compose válida | `docker-compose.full.yml` | `etapa13-compose-config.txt` |

## Parcial

| Funcionalidad | Qué falta | Referencia |
|---|---|---|
| Lectura del QR con la cámara | Solo con `BarcodeDetector`; no se probó en dispositivos reales | `etapa-11-informe.md` (DV-16) |
| Ids derivados de la semilla en el generador | Las filas que crean los servicios usan ids aleatorios; el resumen compara contenido | `etapa-7e-informe.md` (desvío 1) |
| Despliegue con contenedores | Las cuatro imágenes se construyen y la de la API pasó una prueba de humo; el sistema completo no se levantó con `docker compose up` ni se observó la CI en GitHub | `etapa13-docker-build.txt`, `etapa13-humo-api.txt` |

## Especificado, pero no implementado

| Funcionalidad | Motivo | Referencia |
|---|---|---|
| Aplicar una sugerencia de sustituto (reemplazo y rutina `Editada`) | Choca con el modelo de revisión aprobado; se detuvo y se propusieron tres opciones | `etapa-9b2-informe.md` |
| Retiro de `ComparacionSeriesDto.Mensaje` | Cambio de contrato público en una etapa sin cambios de API | `etapa-8e-informe.md` (P-8e-1) |

## Pendiente (acciones externas)

| Pendiente | Responsable |
|---|---|
| PUNTO DE ESPERA 1: etiquetar `docs/pfc/evaluacion/banco-consultas.csv` y ejecutar la evaluación (recall@k, MRR) | Autores y Personal Trainer |
| PUNTO DE ESPERA 2: prueba de hardware de Ollama, elección de `OLLAMA_MODELO_GENERACION` y medición de sustitutos | Autor |
| Aplicar en `gymbro_db` las migraciones `Etapa11_AsistenciaYCuotas` y `Etapa10b1_PrediccionAbandono` | Desarrollador |
| Ejecutar `rol-analitica.sql` con contraseñas reales; configurar `Asistencia:ClaveQr` | Desarrollador |
| Ejecutar el generador sintético en la base de desarrollo del autor | Desarrollador |
| Revisión e integración de `cierre-pfc` en `main` | Claude Web y desarrollador |

## Desviación

| Desviación | Motivo | Referencia |
|---|---|---|
| DV-14: aplicación de análisis sin SSO con el JWT | Fuera de alcance; usuario y contraseña de configuración | `etapa-12-informe.md` |
| DV-16: cámara solo con `BarcodeDetector`; código de 60 caracteres incómodo de tipear | Compatibilidad de navegadores; alternativa manual | `etapa-11-informe.md` |
| La plataforma ve nombre y apellido del socio en asistencia y cuotas | Regla 3 del pedido (todo lo que no sea dato de salud); proyección explícita sin datos de salud | `etapa-11-informe.md` (D-11-1) |
| Ejecuciones de analítica sobre base descartable y no sobre `gymbro_db` | Regla de no migrar bases no descartables | `etapa-10b1-informe.md` |
| Plantilla de variables del despliegue en `docker-compose.full.env.example` y no en `.env.example` | El archivo `.env.example` no se pudo leer por permisos del entorno, y no se modifica a ciegas | `etapa-13-informe.md` |

## Trabajo futuro

- Análisis dentro de cada socio (medidas repetidas o modelos mixtos) en la aplicación de análisis.
- Analítica con datos reales, tras una revisión legal y ética (consentimiento, Ley 25.326).
- Flujo de despliegue en Azure (no ejecutado; correspondencia en `docs/despliegue.md`).
- Edición de líneas del protocolo de piernas por línea en la pantalla de revisión (limitación L1 de la 7d).

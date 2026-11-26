# Arquitectura de GymBro

> Documento generado desde el código de la rama **`cierre-pfc`** (Etapa 14). Solo describe lo verificado en el código; cada afirmación cita el archivo. Estado de cada funcionalidad: `estado-implementacion.md`.

## Vista general

```mermaid
flowchart LR
  subgraph Cliente
    PWA["GymBro.Web<br/>Angular 22 + service worker<br/>(Socio en /mi, personal y plataforma en el shell)"]
  end
  subgraph Servidor
    API["GymBro.Api<br/>ASP.NET Core (.NET 10)<br/>JWT + roles"]
    APP["GymBro.Application<br/>casos de uso"]
    DOM["GymBro.Domain<br/>entidades y reglas"]
    INF["GymBro.Infrastructure<br/>EF Core + Npgsql + pgvector"]
    IA["GymBro.IA<br/>FastAPI"]
    OLL["Ollama<br/>nomic-embed-text y modelo de generación"]
    AN["GymBro.Analytics<br/>Streamlit + scikit-learn"]
  end
  DB[("PostgreSQL 16<br/>+ pgvector")]
  PWA -- "HTTP /api (JWT)" --> API
  API --> APP --> DOM
  APP --> INF
  INF --> DB
  INF -- "embeddings y sustitutos (HTTP)" --> IA --> OLL
  AN -- "rol de solo lectura,<br/>vistas analitica.* (solo Sintetico)" --> DB
```

## Capas (.NET)

| Proyecto | Responsabilidad | Ejemplos |
|---|---|---|
| `GymBro.Domain` | Entidades, enums, reglas puras e invariantes; interfaces de repositorio | `Entities/PrescripcionEntrenamiento.cs` (`EsVigente`, `Revisar`), `Rules/PlanificadorPiernas.cs`, `Rules/ValidadorPrescripcion.cs`, `Rules/Kpis/KpisProgresion.cs`, `Entities/CuotaSocio.cs` (`EstaVencida`) |
| `GymBro.Application` | Casos de uso y servicios de aplicación; DTOs | `Services/SesionEntrenamientoService.cs`, `PrescripcionPiernasService.cs`, `ReglasSeguridadService.cs`, `AsistenciaService.cs`, `CuotaService.cs`, `SugerenciaSustitutosService.cs`, `Sintetico/*` |
| `GymBro.Infrastructure` | Persistencia (EF Core, snake_case, pgvector), repositorios, clientes HTTP, generador sintético | `Persistence/GymBroDbContext.cs`, `Repositories/*`, `Services/BuscadorSemanticoService.cs`, `ExternalServices/IaService.cs`, `ExternalServices/GeneradorSustitutosIa.cs`, `Sintetico/GeneradorSintetico.cs` |
| `GymBro.Api` | Controladores, autenticación JWT, autorización por roles, manejo de errores, arranque | `Program.cs`, `Controllers/*`, `Security/HttpUsuarioActual.cs`, `Infrastructure/ManejadorExcepciones.cs` |

Otros componentes: `GymBro.Web` (Angular, `src/app/features/*`, `core/*`), `GymBro.IA` (`main.py`, `sustitutos.py`, `evaluacion/*`), `GymBro.Analytics` (`gymbro_analytics/*`, `app/principal.py`, `entrenar_abandono.py`, `puntuar_abandono.py`). Pruebas: `GymBro.Tests` (unitarias) y `GymBro.Tests.Integration` (API real contra PostgreSQL descartable con Testcontainers, `GymBroApiFactory.cs`).

## Seguridad y aislamiento

- **Autenticación:** JWT HS256 con claims `sub`, `role`, `gimnasio_id`, `socio_id` (`Security/GeneradorTokenJwt.cs`); política por defecto que exige usuario autenticado (`Program.cs`, `FallbackPolicy`).
- **Roles:** `SuperAdministrador`, `AdministradorGimnasio`, `Entrenador`, `Socio` (`Domain/Enums/RolUsuario.cs`), aplicados con `[Authorize(Roles = …)]` en cada controlador.
- **Multigimnasio:** filtros globales de consulta en `GymBroDbContext.cs` según `IUsuarioActual` (gimnasio del JWT para el personal, ficha propia para el Socio). Un recurso no visible responde 404 (`SocioAccessService`, `GimnasioDestino`).
- **Ley 25.326:** el Administrador de la Plataforma no entra en los filtros de socios, mediciones, sesiones, prescripciones, bienestar ni alertas, y `SocioAccessService` le responde 403; sí lee lo que no es dato de salud (gimnasios, catálogo, equipamiento, asistencia y cuotas). Verificado en `MatrizAutorizacionTests.cs`.
- **Analítica:** solo vistas `analitica.*` que exponen datos `Sintetico`, con roles propios (`docs/pfc/scripts/rol-analitica.sql`).

## Flujos principales

### Rutina de piernas y sesión

```mermaid
sequenceDiagram
  participant P as Personal o Socio (PWA)
  participant A as GymBro.Api
  participant S as PrescripcionPiernasService
  participant V as ValidadorPrescripcion (R-LIM)
  participant R as RevisionPrescripcionService
  participant DB as PostgreSQL
  P->>A: POST /api/Socio/{id}/prescripciones/piernas
  A->>S: GenerarAsync (consentimiento, medición, sin pendiente)
  S->>S: PlanificadorPiernas.Planificar (determinista)
  S->>V: Validar líneas (si viola, no se persiste)
  S->>R: AplicarPolitica (PendienteRevision o Aprobada)
  S->>DB: rutina + líneas + snapshot
  P->>A: POST /api/Sesiones (solo con rutina vigente: Aprobada o Editada)
  A->>DB: sesión, ejercicios y series
  A->>A: ReglasSeguridadService (R-RIR-1, R-PROG-1)
```

### Búsqueda semántica y sustitutos

```mermaid
sequenceDiagram
  participant PT as Personal Trainer (revisión)
  participant A as GymBro.Api
  participant B as BuscadorSemanticoService
  participant IA as GymBro.IA
  participant O as Ollama
  PT->>A: POST /api/revisiones/{id}/lineas/{linea}/sustitutos {motivo}
  A->>B: BuscarAsync (filtros duros: activo, gimnasio, equipamiento, zona)
  B->>IA: POST /generar-embedding/ {texto}
  IA->>O: embeddings
  B-->>A: candidatos (pgvector, distancia coseno)
  A->>IA: POST /sugerir-sustitutos/ (solo los candidatos)
  IA->>O: chat JSON, temperatura 0
  A->>A: Interpretar JSON, ids dentro del conjunto, Validador R-LIM
  A-->>PT: sugerencia (no aplicada, auditada)
```

### Asistencia con QR

```mermaid
sequenceDiagram
  participant S as Socio (PWA)
  participant A as GymBro.Api
  participant P as Personal (PWA)
  S->>A: POST /api/asistencia/qr
  A-->>S: token HMAC de 2 minutos (Base32)
  S->>S: dibuja el QR en el cliente
  P->>P: BarcodeDetector (si existe) o tipeo del código
  P->>A: POST /api/asistencia/qr/validar {token}
  A->>A: firma, vencimiento, socio del mismo gimnasio
  A-->>P: asistencia del día (idempotente)
```

## Despliegue

`docker-compose.full.yml` y `docs/despliegue.md` (Etapa 13); integración continua en `.github/workflows/ci.yml` (sin despliegue).

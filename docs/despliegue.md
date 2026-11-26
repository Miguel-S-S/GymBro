# Manual de despliegue de GymBro

Etapa 13 del cierre del PFC. Describe cómo levantar el sistema completo con contenedores. **No hay despliegue automático**: la CI (`.github/workflows/ci.yml`) solo compila y prueba. Clasificación según la guía del proyecto: los archivos de contenedores y la CI están *implementados*; lo verificado en esta etapa se detalla en `docs/pfc/etapas/etapa-13-informe.md` (incluye qué no se pudo verificar).

## Componentes

| Servicio (compose) | Imagen / Dockerfile | Puerto interno | Publicado | Función |
|---|---|---|---|---|
| `db` | `pgvector/pgvector:pg16` | 5432 | no | PostgreSQL 16 con pgvector |
| `api` | `GymBro.Api/Dockerfile` (multi-etapa, SDK 10 → ASP.NET 10) | 8080 | no (lo expone `web` bajo `/api/`) | API REST; aplica las migraciones al arrancar |
| `ia` | `GymBro.IA/Dockerfile` (Python 3.14) | 8000 | no | Embeddings y sustitutos (FastAPI + cliente de Ollama) |
| `web` | `GymBro.Web/Dockerfile` (Node 24 → nginx 1.27) | 80 | `PUERTO_WEB` (8080) | PWA Angular; reenvía `/api/` a `api:8080` |
| `analitica` | `GymBro.Analytics/Dockerfile` (Python 3.14) | 8501 | `PUERTO_ANALITICA` (8501) | Aplicación Streamlit de análisis (solo datos `Sintetico`) |
| `ollama` (perfil `ollama`) | `ollama/ollama` | 11434 | no | Modelos locales (embeddings y generación) |

Comprobaciones de salud: `db` con `pg_isready`; `api` con `GET /salud` (anónimo, sin datos); `ia` con `GET /`; `web` con `index.html`; `analitica` con `/_stcore/health`; `ollama` con `ollama list`. `web` espera a que `api` esté sana, y `api` a la base.

## Pasos

1. **Requisitos:** Docker con Compose v2. Para la PWA en celulares: un dominio con **HTTPS** (ver más abajo).
2. **Variables:** copiar `docker-compose.full.env.example` a `docker-compose.full.env` (no se versiona) y completar `POSTGRES_PASSWORD`, `JWT_KEY` (32 caracteres o más), opcionalmente `ASISTENCIA_CLAVE_QR` (32 o más; vacía = QR deshabilitado), `SUPERADMIN_EMAIL` y `SUPERADMIN_PASSWORD` (crea el Administrador de la Plataforma), `OLLAMA_MODELO_GENERACION` (punto de espera 2 de la 9b-2) y las de analítica.
3. **Validar la configuración:** `docker compose -f docker-compose.full.yml --env-file docker-compose.full.env config`.
4. **Construir y levantar:** `docker compose -f docker-compose.full.yml --env-file docker-compose.full.env up -d --build` (con Ollama en contenedor, agregar `--profile ollama`). Al arrancar, la API aplica las migraciones pendientes (`Program.cs`, `MigrateAsync`).
5. **Modelos de Ollama** (si se usa el perfil): `docker compose -f docker-compose.full.yml exec ollama ollama pull nomic-embed-text` y el modelo de generación elegido. Los embeddings del catálogo se generan con `GymBro.IA/generador_embeddings.py` (variables `POSTGRES_*`).
6. **Roles de analítica:** con el usuario administrador de la base, ejecutar una copia local de `docs/pfc/scripts/rol-analitica.sql` con las dos contraseñas completadas (por ejemplo, `docker compose ... exec -T db psql -U postgres -d gymbro_db < rol-analitica.local.sql`). Luego poner la contraseña del rol de lectura en `ANALITICA_PG_CLAVE` y reiniciar `analitica`.
7. **Datos sintéticos (opcional, solo fuera de producción):** el generador de la Etapa 7e es un modo de desarrollo de la API que **solo corre en `Development`**; no se ejecuta desde la imagen de producción. Ver `docs/pfc/sintetico-parametros.md`.
8. **Modelo de abandono (opcional):** dentro del contenedor `analitica`, `python entrenar_abandono.py --salida /tmp` y luego `puntuar_abandono.py` con el rol de puntuación (`ANALITICA_PG_USUARIO=gymbro_analitica_puntuacion` y su clave).

## HTTPS es obligatorio para la PWA

El service worker (instalación y funcionamiento sin conexión) y la cámara del lector de QR (`getUserMedia`) solo funcionan en un contexto seguro: `https://` o `http://localhost`. En cualquier otro despliegue hay que terminar TLS delante de `web` (un proxy inverso como nginx o Caddy con un certificado válido, o el servicio de la nube). El contenedor `web` escucha HTTP en el puerto 80 y espera estar detrás de ese proxy. `nginx.conf` sirve `ngsw-worker.js`, `ngsw.json`, `index.html` y el manifiesto sin caché agresiva (`no-cache`), para que una versión nueva llegue en la siguiente visita, y los archivos con huella en el nombre con caché larga.

## Variables de entorno

| Variable | Servicio | Uso |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | api | Cadena de PostgreSQL (la arma el compose con `POSTGRES_PASSWORD`) |
| `Jwt__Key` | api | Clave de firma de los JWT (32 bytes o más; la API no arranca sin ella) |
| `Asistencia__ClaveQr` | api | Clave del QR de asistencia (Etapa 11) |
| `SuperAdmin__Email`, `SuperAdmin__Password` | api | Alta inicial del Administrador de la Plataforma |
| `Ia__UrlBase`, `Ia__TiempoMaximoGeneracionSegundos` | api | Microservicio de IA y tiempo máximo de la generación (60 s) |
| `OLLAMA_HOST`, `OLLAMA_MODELO_EMBEDDINGS`, `OLLAMA_MODELO_GENERACION` | ia | Servidor de Ollama, modelo de embeddings (`nomic-embed-text`) y de generación (sin valor por defecto) |
| `ANALITICA_PG_HOST`, `_PORT`, `_DB`, `_USUARIO`, `_CLAVE` | analitica | Conexión con el rol de solo lectura |
| `ANALITICA_APP_USUARIO`, `ANALITICA_APP_CLAVE` | analitica | Acceso a la aplicación de análisis (sin SSO con el JWT, DV-14) |

Ningún secreto está en el repositorio ni en las imágenes; en desarrollo se usan `dotnet user-secrets` (ver `docs/configuracion-secretos.md`).

## Azure (no ejecutado)

Correspondencia propuesta, **no probada** en esta etapa:

| Componente | Servicio de Azure | Notas |
|---|---|---|
| `db` | Azure Database for PostgreSQL – Flexible Server, con la extensión `vector` habilitada | Alta disponibilidad y copias de seguridad administradas |
| `api` | Azure Container Apps (o App Service para contenedores) | Ingreso interno; secretos en Key Vault referenciados como variables |
| `web` | Azure Static Web Apps (solo el `dist/`) o Container Apps con la imagen nginx | Static Web Apps da HTTPS y dominio; habría que trasladar el reenvío de `/api/` a su configuración de rutas |
| `ia` y `ollama` | Container Apps con perfil de carga de trabajo con GPU, o una máquina virtual con GPU | La latencia de la generación depende del hardware (punto de espera 2) |
| `analitica` | Container Apps con autenticación integrada (Entra ID) | Reemplazaría el usuario y contraseña de configuración (DV-14) |
| Secretos | Azure Key Vault | `Jwt__Key`, `Asistencia__ClaveQr`, contraseñas de la base y de los roles |
| Imágenes | Azure Container Registry | Construidas por un flujo de CI separado (no incluido) |
| Registros | Log Analytics / Application Insights | Los mensajes de la API ya evitan datos de salud |

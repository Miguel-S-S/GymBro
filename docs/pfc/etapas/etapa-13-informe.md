# Etapa 13 — Contenedores, CI y manual de despliegue

Rama `cierre-pfc`. Commit de código: `fbc918b`. Evidencia: `docs/pfc/evidencia/etapa13-*.txt`. Manual: `docs/despliegue.md`. Sin migración.

## Alcance implementado

| Pedido | Estado | Dónde |
|---|---|---|
| `Dockerfile` de la API (multi-etapa) | Implementado; ver construcción | `GymBro.Api/Dockerfile` (SDK 10 → ASP.NET 10, usuario `app`, puerto 8080) |
| `Dockerfile` del servicio de IA | Implementado; ver construcción | `GymBro.IA/Dockerfile`, `requirements-servicio.txt` |
| `Dockerfile` del cliente con redirección a `index.html` y service worker sin caché agresiva | Implementado; ver construcción | `GymBro.Web/Dockerfile`, `GymBro.Web/nginx.conf` |
| `Dockerfile` de `GymBro.Analytics` | Implementado; ver construcción | `GymBro.Analytics/Dockerfile` |
| `docker-compose.full.yml` con base pgvector, API, IA, cliente, analítica y Ollama como perfil opcional | Implementado; configuración verificada | `docker-compose.full.yml` |
| Plantilla de variables | Implementado (ver desvío 1) | `docker-compose.full.env.example` |
| Comprobaciones de salud | Implementado | `HEALTHCHECK` en cada imagen; `GET /salud` anónimo en la API |
| CI: .NET con Testcontainers, `ng build`, `ng test`, Python; sin despliegue | Implementado; sintaxis verificada; **no ejecutada en GitHub** | `.github/workflows/ci.yml` |
| `docs/despliegue.md` con pasos, HTTPS, variables y Azure (no ejecutado) | Implementado | `docs/despliegue.md` |

## Texto propuesto para 4.5 (implementación)

Cada componente tiene su imagen de contenedor. La de la API se construye en dos etapas: la primera restaura y publica con el SDK de .NET 10 y la segunda ejecuta el resultado sobre la imagen de ASP.NET con un usuario sin privilegios; escucha HTTP en el puerto 8080 y recibe la cadena de conexión y los secretos por variables de entorno. El microservicio de IA y la aplicación de análisis se construyen sobre Python 3.14 con sus dependencias fijadas. El cliente se compila con Node y se sirve con nginx, que redirige las rutas de la aplicación a `index.html`, reenvía `/api/` a la API para mantener un único origen (como el proxy de desarrollo) y sirve el service worker, su manifiesto e `index.html` con revalidación obligatoria, para que una versión nueva llegue en la siguiente visita, mientras que los archivos con huella en el nombre se cachean por un año. Un archivo de Compose orquesta la base con pgvector, la API, la IA, el cliente y la analítica, con Ollama como perfil opcional; cada servicio declara su comprobación de salud y el cliente espera a que la API esté sana. Para la comprobación de la API se agregó el endpoint anónimo `GET /salud`, que no devuelve datos. La integración continua compila y prueba .NET (incluidas las pruebas de integración con Testcontainers sobre el Docker del ejecutor), compila y prueba el cliente y ejecuta las pruebas de Python; no despliega. El manual de despliegue explica los pasos, la obligación de servir la PWA por HTTPS (requisito del service worker y de la cámara) y una correspondencia con servicios de Azure, que no se ejecutó.

## Decisiones técnicas

- **D-13-1. Mismo origen para cliente y API** (nginx reenvía `/api/`): el cliente ya usa la URL relativa `/api` (`core/api/api.config.ts`), sin CORS.
- **D-13-2. `GET /salud` anónimo** con `AddHealthChecks` del propio framework, sin paquetes nuevos; la comprobación de la imagen usa `bash` y `/dev/tcp` porque la imagen de ASP.NET no trae `curl`.
- **D-13-3. Dependencias del servicio de IA separadas** (`requirements-servicio.txt`): `requirements.txt` incluye las del ETL y los notebooks, que el contenedor no necesita.
- **D-13-4. HTTPS fuera del contenedor**: lo termina un proxy o el servicio de la nube.
- **D-13-5. Ollama sin versión fija** (`OLLAMA_VERSION`, por defecto `latest`), configurable; las demás imágenes base fijan la versión mayor.

## Texto propuesto para 4.6 (verificación)

Ver la sección de cifras. Se verificó con herramientas: `docker compose config` con variables ficticias (válido, también con el perfil `ollama`; sin variables falla por las obligatorias), la validación de `ci.yml` contra el esquema de GitHub Actions con `check-jsonschema` (detectó un error real de YAML, dos puntos sin comillas en el nombre de un paso, corregido antes del commit) y la construcción de las cuatro imágenes con `docker build`. La matriz de autorización suma `GET /salud` (anónimo, 200).

## Cifras de la verificación

Sobre el commit `fbc918b` (`etapa13-*.txt`):

| Verificación | Resultado |
|---|---|
| `dotnet build` | 0 errores (advertencia previa CS2008) |
| Pruebas unitarias / de integración | 382 / 585 (+2: celdas de `GET /salud`) |
| `ng build` / pruebas del cliente | correcto / 253 |
| `docker compose config` con variables ficticias | válido (código 0); con `--profile ollama` lista los seis servicios; sin variables falla por las obligatorias, como se espera (`etapa13-compose-config.txt`) |
| `ci.yml` contra el esquema de GitHub Actions (`check-jsonschema` 0.39.0) | válido (`etapa13-ci-validacion.txt`) |
| `docker build` (`etapa13-docker-build.txt`) | API 74 s, 104 MB comprimidos (365 MB en disco); IA 16 s, 206 MB; cliente 49 s, 85 MB; analítica 87 s; las cuatro con código 0 |
| Prueba de humo de la imagen de la API (`etapa13-humo-api.txt`) | `/salud` 200, ruta protegida sin token 401, `HEALTHCHECK` en `healthy`, proceso con el usuario `app` |

## Desvíos y limitaciones

1. **Plantilla en `docker-compose.full.env.example` y no en `.env.example`.** El entorno de ejecución de Claude Code no permitió leer `.env.example` (archivo protegido por la configuración de permisos); no se modificó un archivo sin poder verlo. La plantilla nueva se agregó aparte y `docker-compose.full.env` se agregó a `.gitignore`.
2. **No se levantó el sistema completo** con `docker compose up` (solo la imagen de la API, en la prueba de humo) ni se observó una ejecución de la CI en GitHub.
3. **Azure no ejecutado** (pedido).

## Pendientes

- Ejecutar la CI en GitHub (la rama `cierre-pfc` está incluida en los disparadores) y revisar el primer resultado.
- Probar `docker compose up` completo con certificados reales detrás de un proxy HTTPS.

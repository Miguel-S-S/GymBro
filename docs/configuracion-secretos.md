# Configuración de secretos (Etapa 8c, T1)

Ningún secreto se versiona. Los archivos `appsettings*.json` solo llevan valores no secretos; las contraseñas y la clave JWT se cargan con **user-secrets** en desarrollo o con **variables de entorno** en cualquier entorno.

## Claves que GymBro.Api necesita

| Clave de configuración | Variable de entorno | Obligatoria | Uso |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Sí | Cadena completa de PostgreSQL, **con** la contraseña. La de `appsettings.json` no la lleva. |
| `Jwt:Key` | `Jwt__Key` | Sí (la API no arranca sin ella) | Firma de los JWT; al menos 32 caracteres. |
| `SuperAdmin:Email` | `SuperAdmin__Email` | No | Alta del Administrador de la Plataforma al iniciar. |
| `SuperAdmin:Password` | `SuperAdmin__Password` | No | Contraseña inicial de esa cuenta. |
| `Datasets:SembrarAlIniciar` | `Datasets__SembrarAlIniciar` | No | Siembra de los CSV al iniciar. Por defecto: `true` en Development, `false` en el resto (T3). |

## Desarrollo: user-secrets

`GymBro.Api.csproj` tiene `UserSecretsId`. Los valores se guardan fuera del repositorio, en el perfil del usuario (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`), y ASP.NET los carga solo en `Development`.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=gymbro_db;Username=postgres;Password=<contraseña>" --project GymBro.Api
dotnet user-secrets set "Jwt:Key" "<clave de 32 caracteres o más>" --project GymBro.Api
dotnet user-secrets set "SuperAdmin:Email" "<email>" --project GymBro.Api
dotnet user-secrets set "SuperAdmin:Password" "<contraseña>" --project GymBro.Api
dotnet user-secrets list --project GymBro.Api   # muestra los valores: no compartir la salida
```

## Fuera de desarrollo: variables de entorno

El doble guion bajo (`__`) reemplaza a los dos puntos:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=...;Port=...;Database=gymbro_db;Username=...;Password=..."
$env:Jwt__Key = "<clave>"
dotnet run --project GymBro.Api
```

## Base de datos (docker-compose) y script de embeddings

- `docker-compose.yml` exige `POSTGRES_PASSWORD` y la lee del archivo `.env` de la raíz (ignorado por git).
- `GymBro.IA/generador_embeddings.py` lee `POSTGRES_PASSWORD` (obligatoria) y, opcionalmente, `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB` y `POSTGRES_USER`. Si falta la obligatoria, termina con un mensaje que la nombra.
- Plantilla de `.env` (sin valores):

```dotenv
POSTGRES_PASSWORD=
# POSTGRES_HOST=localhost
# POSTGRES_PORT=5433
# POSTGRES_DB=gymbro_db
# POSTGRES_USER=postgres
```

## Archivos que no se versionan

- `.env` y `.env.*` (salvo `.env.example`).
- `GymBro.Api/appsettings.Development.json`: configuración local de cada equipo; dejó de versionarse en la Etapa 8c. Los valores no secretos que necesita la API tienen valor por defecto en el código o en `appsettings.json`; los secretos van en user-secrets. Claves opcionales de desarrollo: `DemoData:Habilitado`, `DemoData:UrlFront`, `Datasets:Ruta`, `Ia:UrlBase`.

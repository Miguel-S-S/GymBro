# GymBro.Api - imagen multi-etapa (Etapa 13). Contexto de construccion: la raiz del repositorio.
#   docker build -f GymBro.Api/Dockerfile -t gymbro-api .
# Sin secretos en la imagen: la cadena de conexion, Jwt__Key, Asistencia__ClaveQr y SuperAdmin__* llegan por variables de
# entorno (docker-compose.full.yml). Escucha HTTP en 8080; HTTPS lo termina el proxy (ver docs/despliegue.md).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src
# Restauracion en una capa aparte (se reutiliza si no cambian los proyectos).
COPY GymBro.Domain/GymBro.Domain.csproj GymBro.Domain/
COPY GymBro.Application/GymBro.Application.csproj GymBro.Application/
COPY GymBro.Infrastructure/GymBro.Infrastructure.csproj GymBro.Infrastructure/
COPY GymBro.Api/GymBro.Api.csproj GymBro.Api/
RUN dotnet restore GymBro.Api/GymBro.Api.csproj
COPY GymBro.Domain/ GymBro.Domain/
COPY GymBro.Application/ GymBro.Application/
COPY GymBro.Infrastructure/ GymBro.Infrastructure/
COPY GymBro.Api/ GymBro.Api/
RUN dotnet publish GymBro.Api/GymBro.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ejecucion
WORKDIR /app
COPY --from=compilacion /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Datasets__SembrarAlIniciar=false
EXPOSE 8080
USER app
# Comprobacion de salud sin herramientas extra: bash con /dev/tcp contra GET /salud (anonimo).
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /salud HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && head -n 1 <&3 | grep -q " 200 "'
ENTRYPOINT ["dotnet", "GymBro.Api.dll"]

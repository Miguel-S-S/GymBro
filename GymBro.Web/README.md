# GymBro.Web — Panel administrativo

Cliente Angular 22 (standalone components, signals, zoneless) con Angular Material y PWA
(`@angular/service-worker`). Consume GymBro.Api.

## Requisitos

- Node.js 22 o superior.
- GymBro.Api corriendo en `http://localhost:5069` (perfil `http` de `launchSettings.json`).
- Para la búsqueda semántica, GymBro.IA en `http://127.0.0.1:8000` y Ollama.

## Desarrollo

```bash
npm install
npm start        # ng serve en http://localhost:4200
```

`ng serve` reenvía `/api/*` a `http://localhost:5069` (`proxy.conf.json`), así que en
desarrollo no hace falta habilitar CORS en la API.

## Build y pruebas

```bash
npm run build                # salida en dist/gymbro-web/browser
npx ng test --watch=false    # Vitest
```

El service worker solo se registra en el build de producción. Para probar la PWA,
serví `dist/gymbro-web/browser` con un servidor estático que además reenvíe `/api` a la API.

## Estructura

```
src/app/
  core/
    api/        Clientes HTTP, uno por controlador de GymBro.Api
    models/     Interfaces alineadas 1:1 con los DTOs de GymBro.Application
    util/       Manejo de errores de la API y formateo
  layout/       Shell (sidenav + toolbar)
  features/
    socios/     Listado, alta, detalle y mediciones
    ejercicios/ Catálogo paginado, búsqueda semántica y selector con autocompletado
    sesiones/   Historial, registro serie por serie y detalle con la comparación prescripción/ejecución
```

## Pendiente (depende de la API)

- Prescripciones: GymBro.Api todavía no expone endpoints.
- Alta de socio con perfil completo (género, nacimiento, objetivo, nivel) y mediciones con
  todas las métricas: los DTOs de alta actuales no los incluyen.
- Autenticación: la API todavía no la implementa.

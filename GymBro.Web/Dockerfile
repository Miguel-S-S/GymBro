# GymBro.Web - compilacion de Angular y servidor web (Etapa 13). Contexto: GymBro.Web/
#   docker build -t gymbro-web GymBro.Web
# nginx sirve la PWA, redirige las rutas del cliente a index.html y reenvia /api/ a la API (servicio "api" del compose).
FROM node:24-alpine AS compilacion
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build

FROM nginx:1.27-alpine AS ejecucion
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=compilacion /src/dist/gymbro-web/browser /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -q -O /dev/null http://127.0.0.1/index.html || exit 1

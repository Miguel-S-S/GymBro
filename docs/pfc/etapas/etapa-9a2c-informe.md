# Etapa 9a-2c — Acceso del Administrador de la Plataforma a Vinculaciones y Mi gimnasio: texto para el informe y registro de decisiones

**Fecha:** 19/11/2026. **Rama:** `etapa9a2c`, creada desde `main` en `cfef78c` (árbol sin cambios en archivos versionados). **Sin merge ni push a `main`:** la integración la decide el autor después de la revisión de Claude Web.
**Commits:** código, pruebas y línea base en `6acfd35`; evidencia sobre ese commit y este informe en el commit siguiente.
**Evidencia:** `docs/pfc/evidencia/etapa9a2c-*.txt`:
- `etapa9a2c-base-*.txt`: línea base sobre `main` (`cfef78c`), antes de cualquier cambio;
- `etapa9a2c-dotnet-build.txt`, `etapa9a2c-dotnet-test.txt`, `etapa9a2c-ng-build.txt`, `etapa9a2c-ng-test.txt`: build y pruebas sobre el commit de código `6acfd35`, con fecha.

Sin migración: la etapa no cambia el modelo de datos.

## Decisiones del autor que guían la etapa (19/11/2026)

- **D1 confirmada:** el Administrador de la Plataforma tiene ABM completo sobre vinculaciones (listar y resolver) y sobre la política de revisión de cualquier gimnasio, auditado.
- **P1 (filtro explícito):** el filtro global de EF de las solicitudes de vinculación no deja pasar al Administrador de la Plataforma, porque su cuenta no tiene gimnasio en el JWT. En lugar de ampliar ese filtro, las dos lecturas que usa la plataforma (pendientes de un gimnasio y una solicitud de un gimnasio) ignoran el filtro y aplican uno explícito por el gimnasio indicado y por baja lógica. Es el mismo criterio de la Etapa 9a-2b para los ejercicios desactivados; el aislamiento del resto del sistema no cambia.
- **P2 (aviso de scripts):** la pantalla "Mi gimnasio" tiene además las secciones "Personal Trainers" y "Asignar socio a un entrenador". Sus endpoints (`api/personal`) son del Dueño y la lista de socios no es visible para la plataforma. Para el Administrador esas secciones se muestran (no se ocultan) con un aviso: la gestión se hace con `admin-usuario.sql` y `admin-asignacion.sql`, como se decidió en la Etapa 9a-2b. No se amplió el alcance.

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas (API real contra PostgreSQL descartable, y specs del cliente). Pendiente: la prueba manual en pantalla.**

Hasta la Etapa 9a-2b, las pantallas de vinculaciones y de configuración del gimnasio eran exclusivas del Dueño. Con la regla del autor de que el Administrador de la Plataforma ve y accede a todo lo que no sean datos de salud, esta etapa abrió:

1. **Vinculaciones.** El Administrador de la Plataforma lista las solicitudes pendientes de cualquier gimnasio y las aprueba o rechaza. Indica el gimnasio en el pedido (`gimnasioId`); sin él, la API responde 400 con el mensaje "Indique el gimnasio."; con un gimnasio inexistente, 404. Una solicitud de otro gimnasio que el indicado responde 404. Al aprobar se aplica la misma validación cruzada por DNI que al Dueño y el socio queda vinculado al gimnasio de la solicitud.
2. **Política de revisión.** El Administrador lee la configuración de cualquier gimnasio (nombre, código de vinculación y política) y cambia `requiere_aprobacion_rutinas`, con las mismas reglas de `gimnasioId`.
3. **El Dueño no cambia.** Sigue operando sobre el gimnasio de su JWT sin indicar nada. Si indica otro gimnasio, la respuesta es 404, igual que un recurso no visible; el parámetro no sirve para saltarse el aislamiento. Se reutiliza `GimnasioDestino.ResolverAsync`, el mismo patrón de Equipamiento.
4. **Auditoría.** Aprobar, rechazar y cambiar la política quedan en `audit_logs` con el gimnasio afectado (`RegistrarEnGimnasioAsync`). Cuando actúa la plataforma, el detalle dice "Administrador de la Plataforma sobre el gimnasio ...". Esto también se aplica al Dueño: antes el gimnasio salía del JWT, ahora del gimnasio resuelto, que para el Dueño es el mismo.
5. **Cliente.** Las rutas `gimnasio/vinculaciones` y `gimnasio/configuracion` admiten `SuperAdministrador` y el menú lateral las muestra a ese rol. Ambas pantallas tienen un selector de gimnasio solo para la plataforma (lista de `api/gimnasios`); sin gimnasio elegido muestran "Elegí un gimnasio ...". No se oculta ni deshabilita nada: aprobar, rechazar y el interruptor de la política funcionan igual que para el Dueño.

**Datos personales.** Las solicitudes de vinculación contienen nombre del socio, DNI declarado, estado, fecha y motivo: datos personales de identificación, **no datos de salud**. Al aprobar, el servidor lee la ficha del socio para comparar el DNI, pero no devuelve nada de ella. La plataforma sigue sin acceso a socios, mediciones, bienestar, sesiones y alertas (filas de la matriz sin cambios).

## Texto propuesto para 4.6 (verificación)

| Criterio | Prueba | Resultado |
|---|---|---|
| La plataforma lista y resuelve vinculaciones de cualquier gimnasio indicando `gimnasioId` | `VinculacionesPlataformaTests`: listar en G1 y G2; aprobar en G1 y rechazar en G2; socio vinculado; `resuelta_por_usuario_id` de la plataforma | Correcta |
| La plataforma no resuelve una solicitud indicando otro gimnasio | `VinculacionesPlataformaTests` (404, la solicitud sigue pendiente) | Correcta |
| La plataforma lee y cambia la política de revisión de cualquier gimnasio | `VinculacionesPlataformaTests` (lectura de G2, cambio, el Dueño de G2 ve el valor nuevo) | Correcta |
| Sin `gimnasioId`, la plataforma recibe 400 con mensaje claro y no se escribe nada | `VinculacionesPlataformaTests` (cuatro endpoints, mensaje "Indique el gimnasio", sin efectos) y matriz | Correcta |
| Gimnasio inexistente: 404 | `VinculacionesPlataformaTests` | Correcta |
| El Dueño sigue limitado a su gimnasio; un `gimnasioId` ajeno responde 404 sin efectos | `VinculacionesPlataformaTests` (listar, leer, resolver y política) y matriz | Correcta |
| El Dueño cambia la política y resuelve como antes | `VinculacionesPlataformaTests` | Correcta |
| Entrenador y Socio siguen sin acceso (403) | `MatrizAutorizacionTests` ampliada (filas de vinculaciones y mi-gimnasio) | Correcta |
| Las acciones quedan auditadas con el gimnasio | `VinculacionesPlataformaTests` (rol, usuario, gimnasio y detalle en aprobar, rechazar y política) | Correcta |
| Las pantallas se abren para la plataforma con selector; para el Dueño no hay selector | `vinculaciones.spec.ts`, `mi-gimnasio.spec.ts`, `app.routes.spec.ts`, `shell.spec.ts` | Correcta |
| Mi gimnasio: entrenadores y asignaciones con aviso de scripts para la plataforma, sin llamar a endpoints del Dueño | `mi-gimnasio.spec.ts` | Correcta |
| Build y pruebas sin errores nuevos (piso: 250 / 391 / 191) | Evidencia `etapa9a2c-*` | **250 / 406 / 204**, 0 errores (más la advertencia CS2008 ya existente) |

**Cifras antes y después.** Unitarias: 250 → 250. Integración: 391 → 406 (+8 en `VinculacionesPlataformaTests`, +7 celdas nuevas en la matriz; las celdas de la plataforma que antes esperaban 403 ahora esperan 400 o 200). Cliente: 191 → 204 (+4 Vinculaciones, +4 Mi gimnasio, +4 rutas, +1 menú).

## Desvíos respecto del pedido

- **Menú lateral.** El pedido nombra rutas y pantallas; además se agregaron los dos ítems al menú de la plataforma (`layout/shell.ts`), porque sin ellos la pantalla solo se abre escribiendo la URL.
- **Ruta de la API.** El Administrador usa la misma ruta `api/mi-gimnasio`, aunque el gimnasio no sea "suyo". No se renombró para no cambiar el contrato del Dueño.
- **`MiGimnasioController`** no está en un archivo propio: está en `GimnasiosController.cs`.
- **Motivo del rechazo.** Cuando rechaza la plataforma, el cliente envía "Rechazada por el Administrador de la Plataforma" en lugar de "Rechazada por el gimnasio".

## Pendientes y limitaciones

- **Prueba manual en pantalla (escritorio y móvil):** no realizada desde Claude Code. Estado: *Parcialmente implementado* en cuanto a verificación de interfaz (specs sí, navegador no).
- **Entrenadores y asignaciones desde la aplicación para la plataforma:** *Trabajo futuro* si el autor lo decide. Requiere abrir `api/personal` y una lista de socios del gimnasio elegido que no incluya datos de salud. Hoy se cubre con los scripts de la 9a-2b.

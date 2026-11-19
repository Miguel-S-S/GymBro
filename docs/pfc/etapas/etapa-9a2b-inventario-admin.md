# Etapa 9a-2b - Paso 1: inventario de lo que el Administrador de la Plataforma puede hacer hoy

**Fecha:** 19/11/2026. **Base leida:** `main` en `2216fe1` (rama `etapa9a2b` recien creada, sin cambios de codigo).
**Alcance:** solo informe; no se modifico codigo. Leido de controladores, servicios y filtros globales de consulta.

## 0. Convenciones

- **Roles en el codigo** (`GymBro.Domain/Enums/RolUsuario.cs`): `SuperAdministrador` = Administrador de la Plataforma (AP), `AdministradorGimnasio` = Dueno (D), `Entrenador` = Personal Trainer (PT), `Socio` (S).
- **Existe:** hay endpoint y servicio que lo permiten para ese rol. **Falta:** no hay forma de hacerlo para ese rol desde la API. **No aplica:** la operacion no tiene sentido para la entidad o el rol (por diseno aprobado).
- Las referencias son `archivo:linea` sobre `2216fe1`. Abreviaturas: `Ctx` = `GymBro.Infrastructure/Persistence/GymBroDbContext.cs`; `Ctrl/` = `GymBro.Api/Controllers/`; `Svc/` = `GymBro.Application/Services/`.
- Un punto transversal: el filtro global de consulta de cada entidad decide si el AP **ve** filas aunque un endpoint lo autorice. Se indica en la columna "Filtro (AP)".

## 1. Tabla entidad x operacion x rol (entidades no relacionadas con salud)

| Entidad | Filtro (AP) | Operacion | AP | Dueno | PT | Socio | Prueba |
|---|---|---|---|---|---|---|---|
| **Gimnasio** | Ve todos (`Ctx:317-320`) | Alta | **Existe** (junto con su Dueno) | No aplica | No aplica | No aplica | `Ctrl/GimnasiosController.cs:11,20-21`; `Svc/GimnasioService.cs:39-68` |
| | | Lectura | **Existe** (lista) | Existe (el propio) | Falta | No aplica | `GimnasiosController.cs:17-18`; `MiGimnasioController` en el mismo archivo `:32,38-39` |
| | | Modificacion | **Parcial**: solo activar/desactivar; nombre y politica de revision **Falta** | Existe (solo politica de revision) | No aplica | No aplica | `GimnasiosController.cs:24-26`; `:42-43`; `GimnasioService.cs:79-99` |
| | | Baja | **Parcial**: desactivar (`Activo=false`) es reversible; no hay baja logica con `fecha_baja` | No aplica | No aplica | No aplica | `GimnasioService.cs:90-99` |
| **Usuario - Dueno** | Ve todos (`Ctx:338-342`) | Alta | **Existe** solo al crear el gimnasio; **Falta** agregar un segundo Dueno o reemplazarlo | No aplica | No aplica | No aplica | `GimnasioService.cs:56-65` |
| | | Lectura | **Falta** (no hay endpoint; `GimnasioResponseDto` no trae al Dueno) | Existe (su sesion) | No aplica | No aplica | `GimnasioService.cs:114-122` |
| | | Modificacion | **Falta** (nombre, email, activo, contrasena) | Falta | No aplica | No aplica | sin endpoint en `Ctrl/` |
| | | Baja | **Falta** | No aplica | No aplica | No aplica | sin endpoint en `Ctrl/` |
| **Usuario - Personal Trainer** | Ve todos (`Ctx:338-342`) | Alta | **Falta** | Existe | No aplica | No aplica | `Ctrl/PersonalController.cs:11,20-21`; `Svc/PersonalService.cs:39-41` |
| | | Lectura | **Falta** (el controlador es solo del Dueno) | Existe | No aplica | No aplica | `PersonalController.cs:11,17-18` |
| | | Modificacion | **Falta** | **Falta** | No aplica | No aplica | sin endpoint |
| | | Baja / desactivar | **Falta** | **Falta** | No aplica | No aplica | sin endpoint; `Usuario.Activo` solo se fija al crear |
| **Socio** (solo ficha administrativa) | **No ve ninguno**: el filtro solo admite proceso de sistema, mismo gimnasio o el propio socio (`Ctx:51-54`) | Alta | **Falta** | Existe | No aplica | Existe (registro propio) | `Ctrl/SocioController.cs:14,52-53`; `Ctrl/AuthController.cs:24-25` |
| | | Lectura | **Falta** (rol no autorizado y filtro lo excluye) | Existe | Existe (asignados) | Existe (la propia) | `SocioController.cs:14,30-31`; `Svc/SocioService.cs:37-45` |
| | | Modificacion | **Falta** | Existe | No aplica | Falta (ver nota 2) | `SocioController.cs:61-62` |
| | | Baja | **Falta** | Existe (baja logica) | No aplica | No aplica | `SocioController.cs:73-74`; `SocioService.cs:137-153` |
| **InvitacionSocio** | **No ve ninguna** (`Ctx:392-394`) | Alta | **Falta** | Existe (al dar de alta o renovar) | No aplica | No aplica | `SocioController.cs:67-68`; `Svc/InvitacionService.cs:65` |
| | | Lectura | **Falta** | Indirecta (consulta por token, anonima) | No aplica | No aplica | `AuthController.cs:31-32` |
| | | Modificacion | No aplica (token de un solo uso) | No aplica | No aplica | Activar (anonimo) | `AuthController.cs:36-37` |
| | | Baja (revocar) | **Falta** | Indirecta (al renovar o dar de baja el socio) | No aplica | No aplica | `InvitacionService.cs:83` |
| **SolicitudVinculacion** | **No ve ninguna** (`Ctx:374-377`) | Alta | No aplica (la inicia el socio) | No aplica | No aplica | Existe | `Ctrl/VinculacionesController.cs:17-19` |
| | | Lectura | **Falta** | Existe (pendientes) | Falta | Existe (las propias) | `VinculacionesController.cs:22-28` |
| | | Modificacion (resolver) | **Falta** | Existe | No aplica | No aplica | `VinculacionesController.cs:30-32` |
| | | Baja | **Falta** | Falta | No aplica | Falta | sin endpoint |
| **AsignacionEntrenadorSocio** | **No ve ninguna** (`Ctx:362-364`) | Alta | **Falta** | Existe | No aplica | No aplica | `PersonalController.cs:24-25`; `PersonalService.cs:65-87` |
| | | Lectura | **Falta** | Indirecta (lista de socios del PT) | Indirecta | No aplica | `SocioService.cs:42` |
| | | Modificacion | No aplica (se da de baja y se crea otra) | No aplica | No aplica | No aplica | - |
| | | Baja (desasignar) | **Falta** | **Falta** | No aplica | No aplica | sin endpoint |
| **Ejercicio global - Dataset** | Ve todos (`Ctx:64-69`) | Alta | No aplica (los carga `DataSeeder`) | No aplica | No aplica | No aplica | `Persistence/Seeders/DataSeeder.cs:61` |
| | | Lectura | Existe | Existe | Existe | Existe | `Ctrl/EjerciciosController.cs:26-62` (sin `[Authorize]` por rol) |
| | | Modificacion | **Falta** | No aplica | No aplica | No aplica | `Svc/EjercicioGimnasioService.cs:108-109` rechaza con 403 todo lo que no es `Origen=Gimnasio` del propio gimnasio |
| | | Baja | **Falta** | No aplica | No aplica | No aplica | idem |
| **Ejercicio global - Proyecto** | Ve todos | Alta | No aplica (`CatalogoProyectoSeeder`) | No aplica | No aplica | No aplica | `Seeders/CatalogoProyectoSeeder.cs:95` |
| | | Lectura | Existe | Existe | Existe | Existe | `EjerciciosController.cs:26-62` |
| | | Modificacion | **Falta** | No aplica | No aplica | No aplica | `EjercicioGimnasioService.cs:108-109` |
| | | Baja | **Falta** | No aplica | No aplica | No aplica | idem |
| **Ejercicio propio del gimnasio** | Ve los de todos los gimnasios | Alta | **Falta** (`CrearAsync` exige `GimnasioId` del JWT; el AP no lo tiene) | Existe | **Falta** (D4) | No aplica | `EjerciciosController.cs:66-72`; `EjercicioGimnasioService.cs:53` |
| | | Lectura | Existe (todos) | Existe (el propio) | Existe (el propio) | Existe (el de su ficha) | `Ctx:64-69` |
| | | Modificacion | **Falta** | Existe | **Falta** (D4) | No aplica | `EjerciciosController.cs:74-77`; `EjercicioGimnasioService.cs:105` |
| | | Baja | **Falta** | Existe (rechazada si hay rutina vigente o pendiente) | **Falta** (D4) | No aplica | `EjerciciosController.cs:80-86`; `EjercicioGimnasioService.cs:90-100` |
| **TipoEquipamiento global** | Ve todos (`Ctx:231-235`) | Alta | No aplica (`EquipamientoSeeder`) | No aplica | No aplica | No aplica | `Seeders/EquipamientoSeeder.cs:27` |
| | | Lectura | Existe (dentro de `GET /api/equipamiento?gimnasioId=`) | Existe | Existe | No aplica | `Ctrl/EquipamientoController.cs:23-25` |
| | | Modificacion | **Falta** | No aplica | No aplica | No aplica | sin endpoint |
| | | Baja | **Falta** | No aplica | No aplica | No aplica | sin endpoint |
| **TipoEquipamiento propio** | Ve los de todos | Alta | **Falta** (exige `GimnasioId` del JWT) | Existe | No aplica | No aplica | `EquipamientoController.cs:34-36`; `Svc/EquipamientoService.cs:108` |
| | | Lectura | Existe | Existe | Existe | No aplica | `EquipamientoService.cs:46-60` |
| | | Modificacion | **Falta** | **Falta** | No aplica | No aplica | sin endpoint |
| | | Baja | **Falta** | **Falta** | No aplica | No aplica | sin endpoint |
| **EquipamientoGimnasio** (marcado) | Ve todos (`Ctx:251-255`) | Alta / Modif. / Baja (un solo `PUT` que reemplaza el conjunto) | **Falta** (solo lectura, decision D2 anterior documentada en el controlador) | Existe | No aplica | No aplica | `EquipamientoController.cs:28-33`; `EquipamientoService.cs:62-104` |
| | | Lectura | Existe (indicando `gimnasioId`, obligatorio) | Existe | Existe | Indirecta (baseline) | `EquipamientoService.cs:49-50` |
| **DosificacionVolumetrica** | Ve todas (filtro solo por `fecha_baja`, `Ctx:78`) | Alta / Lectura / Modif. / Baja | **Falta** (sin endpoint para ningun rol; la usa solo el baseline) | No aplica | No aplica | No aplica | `DataSeeder.cs:24`; `CatalogoProyectoSeeder.cs:99`; `Repositories/BaselineRepository.cs` |
| **MatrizRecomendacion** | Ve todas (`Ctx:77`) | Alta / Lectura / Modif. / Baja | **Falta** (sin endpoint para ningun rol; la usa solo el baseline) | No aplica | No aplica | No aplica | `DataSeeder.cs:36`; `BaselineRepository.cs` |
| **AuditLog** | - | Lectura | **Existe** (con filtros por gimnasio, fecha, accion, entidad) | No aplica | No aplica | No aplica | `Ctrl/AuditoriaController.cs:9,15-16`; `Svc/AuditService.cs:36-55` |
| | | Alta / Modif. / Baja | No aplica (solo la escribe `IAuditService`; no se modifica por decision) | - | - | - | - |

### Cliente (pantallas que hoy abre el AP)
`GymBro.Web/src/app/app.routes.ts`: el AP entra a `ejercicios` (`:91-94`) y `gimnasio/equipamiento` (`:115-117`) en solo lectura, y a `plataforma/gimnasios` (`:131-133`) y `plataforma/auditoria` (`:137-139`). **No** entra a `socios`, `socios/:id`, `gimnasio/vinculaciones`, `gimnasio/configuracion` ni `revisiones` (`:67-125`). Ninguna de esas tres ultimas tiene datos de salud por si misma, pero `socios/:id` muestra mediciones, rutinas y sesiones.

## 2. Entidades de salud: fuera de alcance por decision vigente

El AP no accede a ellas (decision confirmada por el autor; Ley 25.326). Se verifica que hoy tampoco puede: ninguno de estos controladores incluye `SuperAdministrador` y los filtros solo admiten proceso de sistema, mismo gimnasio o el propio socio.

| Entidad | Controlador | Filtro |
|---|---|---|
| `MedicionFisica` | `Ctrl/MedicionController.cs:11` | `Ctx:56-59` |
| `RegistroBienestarDiario` | `Ctrl/BienestarController.cs:23,28,38` | `Ctx:293-296` |
| `PrescripcionEntrenamiento` / `PrescripcionEjercicio` / `RevisionPrescripcion` | `Ctrl/PrescripcionesController.cs:14`; `Ctrl/RevisionesController.cs:12` | `Ctx:122-127` |
| `SesionEntrenamiento` / `SesionEjercicio` / `SerieRealizada` | `Ctrl/SesionesController.cs:13` | `Ctx:72-75,100,105` |

## 3. Hallazgos que el autor debe decidir antes del paso 2

1. **Ficha del Socio mezcla datos administrativos y datos que podrian considerarse de salud.** `Socio` (`GymBro.Domain/Entities/Socio.cs`) tiene nombre, apellido, email, DNI y gimnasio (administrativos), pero tambien genero, fecha de nacimiento, objetivo, nivel y la fecha del consentimiento de datos de salud. El filtro actual (`Ctx:51-54`) excluye al AP de la entidad completa. Dar al AP ABM "solo de los datos administrativos" exige una proyeccion separada (DTO y consultas propias, con `IgnoreQueryFilters` acotado) y decidir si genero y fecha de nacimiento cuentan como salud. **Propuesta:** dejar `Socio` fuera de esta etapa y, si hace falta, cubrir baja o correccion de datos por script (seccion 6), sin lectura de los campos dudosos.
2. **`Socio` no tiene campo de "estado".** El prompt menciona "estado" en la ficha administrativa; en el codigo el unico estado es la baja logica (`FechaBaja`). No se agrega campo nuevo sin autorizacion.
3. **El Dueno tampoco puede editar ni dar de baja tipos de equipamiento propios ni Personal Trainers, ni desasignar socios.** Son faltantes del Dueno, no solo del AP. La 9a-2b solo pide ABM de tipos para el AP; si el AP edita y da de baja tipos propios, conviene decidir si el Dueno tambien (mismo servicio).
4. **Riesgo en `DataSeeder` frente a bajas del AP (relevante para 4.1).** Las guardas `DataSeeder.cs:24`, `:36` y `:61` usan `AnyAsync` **con** el filtro de `fecha_baja` (sin `IgnoreQueryFilters`). Si el AP diera de baja **todos** los ejercicios de origen Dataset (o todas las dosificaciones del dataset o todas las filas de la matriz), el siguiente arranque volveria a cargarlos como filas nuevas. Una edicion o una baja parcial no se pisan (el seeder solo carga si no hay ninguno). `CatalogoProyectoSeeder.cs:95,99` y `EquipamientoSeeder.cs:27` si usan `IgnoreQueryFilters`; su comportamiento frente a una edicion del AP se verifica en el paso 2. **Propuesta:** en el paso 2 cambiar la guarda de ejercicios Dataset (`:61`) a `IgnoreQueryFilters()`; las de matriz y dosificacion quedan igual mientras no haya ABM sobre ellas.
5. **Gimnasio:** no tiene baja logica con `fecha_baja`; la desactivacion cumple esa funcion y es reversible. No se propone cambio.

## 4. Propuesta de reparto (para que el autor decida)

| Faltante | Propuesta | Motivo |
|---|---|---|
| Ejercicios propios de cualquier gimnasio (alta, edicion, baja) | **Implementar** (4.1) | Ya pedido en la etapa; reutiliza `EjercicioGimnasioService` |
| Ejercicios globales Dataset y Proyecto (edicion, baja) | **Implementar** (4.1) | Ya pedido |
| Tipos de equipamiento globales y propios (alta propia, edicion, baja) y marcado de cualquier gimnasio | **Implementar** (4.1) | Ya pedido |
| Gimnasio: editar nombre y politica de revision | Script `admin-gimnasio.sql` | Poco frecuente; un `UPDATE` simple |
| Dueno: ver, editar, desactivar, reemplazar | Script `admin-usuario.sql` | Toca credenciales; un ABM en pantalla es una etapa propia |
| Personal Trainer: alta, edicion, desactivacion por el AP | Script `admin-usuario.sql` (mismo) | Idem |
| Socio (ficha administrativa): correccion de datos y baja | Script `admin-socio.sql` **solo si el autor lo aprueba** (hallazgo 1) | Riesgo de exposicion de datos sensibles |
| InvitacionSocio: revocar | Script `admin-invitacion.sql` | Baja logica simple |
| SolicitudVinculacion: resolver o anular | Script `admin-vinculacion.sql` | Excepcional |
| AsignacionEntrenadorSocio: alta y baja | Script `admin-asignacion.sql` | Excepcional; tambien falta al Dueno |
| DosificacionVolumetrica y MatrizRecomendacion | Script `admin-dosificacion-matriz.sql` | Datos de referencia del motor; ademas ver hallazgo 4 |

Nada de esta tabla esta implementado; es una propuesta pendiente de aprobacion.

## 5. Respuesta del autor (19/11/2026) y resultado

1. Socio: queda fuera del ABM del Administrador en la aplicación; se cubre por script (`admin-socio.sql`, solo datos administrativos).
2. Se agrega el campo de estado del socio (`Activo` / `Inactivo`), informativo; lo cambia el Dueño en pantalla.
3. La edición y baja de tipos de equipamiento propios aplica al Administrador de la Plataforma y al Dueño.
4. Los ejercicios no se dan de baja: se desactivan (todos). Los globales se modifican por script (`admin-ejercicio-global.sql`). La guarda de `DataSeeder` se corrigió (hallazgo 4).
5. D2 y D4 confirmadas.

Lo implementado y lo que quedó por script está en `etapa-9a2b-informe.md` (sección "Resultado del inventario").

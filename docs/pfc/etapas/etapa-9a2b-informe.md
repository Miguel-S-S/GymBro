# Etapa 9a-2b — Administración del catálogo por el Administrador de la Plataforma, ejercicios propuestos por el Personal Trainer y aviso de equipamiento: texto para el informe y registro de decisiones

**Fecha:** 19/11/2026. **Rama:** `etapa9a2b`, creada desde `2216fe1` (`main`, árbol sin cambios en archivos versionados). **Sin merge ni push a `main`:** la integración la decide el autor después de la revisión de Claude Web.
**Commits:** código, pruebas, scripts e inventario en `3dd1683`; evidencia y este informe en el commit siguiente.
**Evidencia:** `docs/pfc/evidencia/etapa9a2b-*.txt`:
- `etapa9a2b-base-*.txt`: línea base sobre `main` (`2216fe1`), antes de cualquier cambio (observación O2);
- `etapa9a2b-migracion.sql.txt`: SQL de la migración (Up y Down), generado sin conexión a ninguna base. **No se aplicó a la base local `gymbro-db`**: se aplica al levantar la API;
- `etapa9a2b-dotnet-build.txt`, `etapa9a2b-dotnet-test.txt`, `etapa9a2b-ng-build.txt`, `etapa9a2b-ng-test.txt`: build y pruebas sobre el commit de código, con fecha.

**Documentos de la etapa:** inventario del paso 1 en `etapa-9a2b-inventario-admin.md`; scripts de respaldo en `docs/pfc/scripts/admin-*.sql`.

## Decisiones del autor que guían la etapa (19/11/2026)

- **D2 confirmada:** el Administrador de la Plataforma hace el ABM de las entidades del negocio, **menos los datos de salud**. Lo que no conviene llevar a la aplicación se resuelve por script (sección 6 del pedido).
- **D4 confirmada:** el Personal Trainer crea ejercicios propios del gimnasio directamente, sin aprobación. Edita y desactiva solo los que creó; el Dueño, todos los del gimnasio.
- **Socio:** queda fuera del ABM del Administrador en la aplicación; se cubre por script.
- **Estado del socio:** se agrega el campo `Estado` (`Activo` / `Inactivo`). Es informativo, lo cambia el Dueño en pantalla y el Administrador por script.
- **Tipos de equipamiento propios:** los editan y dan de baja el Administrador de la Plataforma y el Dueño.
- **Ejercicios:** no se dan de baja; **se desactivan** y se pueden reactivar (todos, globales y propios). Los **globales se modifican solo por script**; desde la aplicación, el Administrador solo los desactiva y reactiva.

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas (API real contra PostgreSQL descartable, y specs del cliente). Pendientes: la prueba manual en pantalla y la aplicación de la migración a la base local.**

La Etapa 9a-2 dejó el catálogo y el equipamiento propios del gimnasio en manos del Dueño; el Administrador de la Plataforma solo podía leerlos. Para que la plataforma pueda mantener la integridad de los datos sin acceder a datos de salud, esta etapa agregó:

1. **Administración por la plataforma.** El Administrador de la Plataforma crea, edita, desactiva y reactiva ejercicios propios de **cualquier** gimnasio. También crea, renombra y da de baja tipos de equipamiento (los propios de cualquier gimnasio y los globales) y marca el equipamiento de cualquier gimnasio. Como su cuenta no tiene gimnasio en el JWT, lo indica en el pedido y el servidor valida que exista. Para el Dueño y el Personal Trainer el gimnasio sigue saliendo del JWT; si el pedido indica otro, responde 404, igual que un recurso no visible, así que el parámetro no sirve para saltarse el aislamiento. Cada operación queda en la auditoría con el rol, el gimnasio afectado y un texto que indica que la hizo la plataforma.
2. **Ejercicios que se desactivan.** La baja de la 9a-2 se reemplazó por desactivar y reactivar (`activo`). El filtro global de EF oculta los desactivados a todos los roles, al catálogo, a la búsqueda semántica y al baseline; el historial de rutinas y sesiones conserva el nombre. Quien gestiona el catálogo los lista aparte ("Ver desactivados") y los reactiva. No se puede desactivar un ejercicio usado por una rutina vigente o pendiente de revisión, tampoco la plataforma. Los ejercicios globales no se editan desde la aplicación: el Administrador los modifica con `admin-ejercicio-global.sql`, que conserva el origen y deja el embedding pendiente si cambia el texto vectorizado.
3. **El Personal Trainer propone ejercicios (D4).** Crea ejercicios propios de su gimnasio con las mismas validaciones que el Dueño. El servidor guarda el autor (`creado_por_usuario_id`, tomado del JWT; el cuerpo no lo puede fijar). El Personal Trainer edita y desactiva solo los suyos; el Dueño, todos los del gimnasio. El catálogo muestra "Propuesto por" al personal, no al Socio. El servidor informa en cada ejercicio si quien consulta puede editarlo o cambiar su estado, con la misma regla que aplica al modificar; la pantalla solo muestra los botones que corresponden.
4. **Aviso al quitar equipamiento (O1).** Antes de guardar, la pantalla consulta una previsualización de solo lectura. Esa consulta informa cuántos ejercicios propios activos del gimnasio quedarían sin poder usarse en el baseline, sus nombres (hasta 10) y qué tipos se quitan. Si hay afectados, se pide confirmación; no se bloquea.
5. **Estado del socio.** La ficha del socio tiene un estado administrativo (`Activo` o `Inactivo`). Es informativo: no bloquea el acceso, las sesiones ni las rutinas. El Dueño lo cambia desde la lista de socios.
6. **Scripts de respaldo.** Lo que el inventario mostró como faltante y no conviene llevar a la aplicación quedó en scripts SQL comentados: gimnasio, personal (Dueño y Personal Trainer), socio (solo datos administrativos), invitaciones, vinculaciones, asignaciones, dosificaciones y matriz, y ejercicios globales. Todos validan que quien ejecuta sea un Administrador de la Plataforma, corren dentro de una transacción que termina en `ROLLBACK` por defecto y registran la operación en `audit_logs`.

**Advertencias:**
- Los ejercicios que proponen el Personal Trainer y el Dueño **siguen sin validación profesional ni de seguridad**, y el baseline los puede prescribir. La advertencia de la 9a-2 ahora también se muestra en el catálogo, en el formulario y en el detalle. Queda como insumo de las reglas de seguridad de la 10a.
- Los scripts dan una capacidad amplia a quien tiene acceso a la base. Por eso no exponen datos de salud, no copian valores personales a la auditoría (solo qué campos cambiaron) y no crean cuentas ni cambian contraseñas: esas operaciones siguen en la aplicación.

## Texto propuesto para 4.6 (verificación)

| Criterio | Prueba | Resultado |
|---|---|---|
| La plataforma crea, edita, desactiva y reactiva ejercicios propios de cualquier gimnasio indicado | `AdministracionCatalogoTests.La_plataforma_crea_edita_desactiva_y_reactiva_ejercicios_propios_de_dos_gimnasios...` | Verificado |
| La plataforma gestiona tipos (propios y globales) y el marcado de cualquier gimnasio; el Dueño, sus tipos propios | `La_plataforma_gestiona_tipos_y_marcado_de_cualquier_gimnasio_y_el_Dueno_los_propios` | Verificado |
| La plataforma debe indicar un gimnasio existente (400 sin gimnasio, 404 inexistente) | `La_plataforma_debe_indicar_un_gimnasio_existente`, matriz | Verificado |
| La plataforma desactiva y reactiva un global, no lo edita por la API (403) y el baseline no usa desactivados | `La_plataforma_desactiva_y_reactiva_un_global_pero_no_lo_edita_y_el_baseline_no_usa_desactivados` | Verificado |
| Los seeders no reactivan un ejercicio desactivado ni pisan un tipo global renombrado (segundo arranque) | `Los_seeders_no_reactivan_ni_pisan_lo_que_cambio_la_plataforma` (corre `CatalogoProyectoSeeder` y `EquipamientoSeeder` otra vez) | Verificado |
| `DataSeeder` no recarga el dataset si todos sus ejercicios están desactivados | `MigracionAdminCatalogoTests.El_DataSeeder_no_recarga_el_dataset...` (CSV sintético) | Verificado |
| Un Dueño o Personal Trainer que indica otro gimnasio recibe 404 y no se escribe nada | `Dueno_o_Personal_Trainer_que_indican_otro_gimnasio_reciben_404_y_no_se_escribe_nada` | Verificado |
| Desactivar un ejercicio de una rutina vigente se rechaza, también para la plataforma | `Desactivar_un_ejercicio_en_una_rutina_vigente_se_rechaza_tambien_para_la_plataforma` y la de la 9a-2 | Verificado |
| El Personal Trainer crea y modifica solo los suyos (403 en los de otros); el Dueño modifica todos | `El_Personal_Trainer_crea_y_modifica_solo_los_suyos_y_el_Dueno_modifica_todos`, `MatrizAutorizacionTests` ampliada | Verificado |
| `CreadoPorUsuarioId` se guarda desde el JWT y el cuerpo no lo puede fijar | Misma prueba (el cuerpo envía otro autor) | Verificado |
| Un Personal Trainer o Dueño de otro gimnasio no ve ni modifica esos ejercicios (404) | `Personal_de_otro_gimnasio_no_ve_ni_modifica_los_ejercicios_ajenos` | Verificado |
| La previsualización informa los afectados y no modifica datos (ni el marcado ni la auditoría) | `La_previsualizacion_informa_los_ejercicios_afectados_sin_modificar_datos` | Verificado |
| No se puede crear un ejercicio con el nombre de uno desactivado | `No_se_puede_crear_un_ejercicio_con_el_nombre_de_uno_desactivado` | Verificado |
| Estado del socio: lo cambia el Dueño, es informativo, valida el valor y aísla por gimnasio | `El_Dueno_cambia_el_estado_del_socio_de_forma_informativa`, matriz | Verificado |
| Migración reversible: el Down quita las columnas, el Up las agrega y las filas existentes quedan activas, sin autor y en estado `Activo`; `CHECK` del estado | `MigracionAdminCatalogoTests` (4 pruebas, PostgreSQL descartable) | Verificado (no aplicada a la base local) |
| Cada operación de la plataforma queda auditada con el gimnasio | Asserts de auditoría en `AdministracionCatalogoTests` | Verificado |
| Los scripts: sin parámetros se rechazan; con `ROLLBACK` no dejan cambios; con `COMMIT` hacen lo que dicen y auditan | `ScriptsAdministracionTests` (17 pruebas, los 8 scripts ejecutados tal cual) | Verificado |
| La plataforma abre todas las pantallas que tenía y ve los botones de gestión; el Personal Trainer, los suyos; el Socio, ninguno | `ejercicios-catalogo.spec.ts`, `ejercicio-form-dialog.spec.ts`, `equipamiento.spec.ts`, `socios-lista.spec.ts`, `app.routes.spec.ts` | Verificado |
| Build y pruebas sin errores nuevos (piso: 170 / 287 / 164) | Evidencia `etapa9a2b-*` | **170 / 353 / 183**, 0 errores (ver la advertencia CS2008 en las diferencias) |

**Sin verificar:** la prueba manual en pantalla (escritorio y celular) y la migración sobre los datos de la base local. Las pruebas de embedding usan un `IIaService` simulado.

## Registro de decisiones de la etapa

| # | Decisión | Estado |
|---|---|---|
| G1 | El gimnasio destino lo resuelve un único auxiliar (`GimnasioDestino`): la plataforma lo indica (400 si falta, 404 si no existe); el resto usa el del JWT y un gimnasio distinto da 404. Lo usan ejercicios y equipamiento. | Decisión de diseño |
| G2 | En la **edición** de un ejercicio el `gimnasioId` del cuerpo se ignora: el gimnasio es el del ejercicio. Solo se usa en el alta. | Decisión de diseño |
| G3 | Comportamiento que cambia respecto de la 9a-2: un Dueño que envía otro `gimnasioId` en el alta ya no ve el campo ignorado, recibe 404 (lo pedía la especificación). Se ajustó la prueba de la 9a-2. | Cambio pedido por la especificación |
| X1 | Desactivar se implementa con una columna `activo` y el filtro global (`FechaBaja == null && Activo`). La gestión lee los desactivados con `IgnoreQueryFilters` y una regla de visibilidad explícita (`AlcanceInactivos`): todos para la plataforma; los propios del gimnasio para el Dueño y el Personal Trainer. | Decisión de diseño |
| X2 | La baja (`DELETE /api/Ejercicios/{id}`) se eliminó y se reemplazó por `PUT /api/Ejercicios/{id}/estado`. Los propios dados de baja en la 9a-2 siguen dados de baja (no se convierten). | Decisión del autor (19/11/2026) |
| X3 | La unicidad del nombre incluye los desactivados, para que reactivar nunca produzca dos ejercicios con el mismo nombre. | Decisión de diseño |
| X4 | `activo` no lleva `HasDefaultValue` en el modelo: con un `bool`, EF omitiría el `false` al insertar. El valor por defecto `true` de las filas existentes lo fija la migración. Mismo criterio para `estado`. | Decisión de diseño |
| P1 | El autor ("Propuesto por") se lee con una consulta aparte acotada a los Id de la página, solo nombre y rol. Si el autor es el Administrador de la Plataforma, el personal ve "Administrador de la Plataforma" y no su nombre. El Socio no lo ve. | Decisión de diseño |
| P2 | Los permisos por ejercicio (`PuedeEditar`, `PuedeCambiarEstado`) los calcula el servidor con la misma función que usa al modificar (`EjercicioCatalogoService.Permisos`). Así el cliente no necesita el id del usuario, que la sesión no trae. | Decisión de diseño |
| T1 | Baja de un tipo: se rechaza si **algún** ejercicio no dado de baja lo usa (activo o desactivado), para que reactivar nunca deje un ejercicio con un tipo dado de baja; al darlo de baja se desmarca en los gimnasios. | Decisión de diseño |
| T2 | Renombrar un tipo copia el nombre al texto `equipamiento` de los ejercicios **propios** que lo usan (criterio de la 9a-2). Los globales del dataset conservan su texto original. La clave del tipo no cambia. | Decisión de diseño |
| S1 | Estado del socio como texto con `CHECK` (`Activo`, `Inactivo`), por defecto `Activo`; endpoint `PUT /api/Socio/{id}/estado`, solo el Dueño. | Decisión del autor (valores y efecto) |
| A1 | Auditoría: `IAuditService.RegistrarEnGimnasioAsync` guarda el gimnasio indicado (el del recurso) y, si actúa la plataforma, lo dice en el detalle. | Decisión de diseño |
| A2 | `DataSeeder`: las comprobaciones de "ya cargado" (dosificaciones, matriz y ejercicios del dataset) cuentan también las filas desactivadas o dadas de baja. Antes, desactivar o dar de baja **todo** el dataset lo habría recargado en el siguiente arranque. | Propuesta del inventario, aceptada |
| SC1 | Scripts con un bloque `DO` y los parámetros declarados al comienzo: validan con `RAISE EXCEPTION` (la transacción queda abortada) y funcionan en cualquier cliente de PostgreSQL. | Decisión de diseño |
| SC2 | `admin-asignacion.sql` y la baja de un Personal Trainer **borran** la asignación en lugar de una baja lógica. El índice único `(entrenador_usuario_id, socio_id)` no es parcial y la aplicación ya las borra; una baja lógica impediría volver a asignar desde la aplicación. La trazabilidad queda en `audit_logs`. | Decisión de diseño (desvío de "bajas lógicas") |
| SC3 | Los scripts no crean cuentas ni cambian contraseñas (el hash lo genera la aplicación) y no leen ni modifican datos de salud del socio. | Decisión de diseño |

## Resultado del inventario: qué quedó en la aplicación y qué por script

| Entidad | Aplicación | Script | Motivo |
|---|---|---|---|
| Ejercicio propio (cualquier gimnasio) | Alta, edición, desactivar y reactivar | — | Pedido de la etapa |
| Ejercicio global | Desactivar y reactivar | `admin-ejercicio-global.sql` (modificar) | Decisión del autor |
| Tipo de equipamiento y marcado | Alta, renombre, baja y marcado | — | Pedido de la etapa (decisión 3 para el Dueño) |
| Gimnasio | Alta, lista, activar y desactivar (ya existían) | `admin-gimnasio.sql` (nombre y política) | Poco frecuente |
| Dueño y Personal Trainer | Alta (ya existía, por gimnasio y por el Dueño) | `admin-usuario.sql` (corregir, activar, desactivar, baja) | Toca cuentas; no crea ni cambia contraseñas |
| Socio (datos administrativos) | Estado por el Dueño | `admin-socio.sql` (corregir, estado, baja) | Decisión del autor: el socio queda fuera del ABM en la aplicación |
| Invitación | — | `admin-invitacion.sql` (revocar) | Excepcional |
| Solicitud de vinculación | — | `admin-vinculacion.sql` (aprobar o rechazar) | Excepcional |
| Asignación Personal Trainer y socio | — | `admin-asignacion.sql` (asignar y quitar) | Excepcional; también le falta al Dueño |
| Dosificación y matriz | — | `admin-dosificacion-matriz.sql` | Datos de referencia del motor |

## Diferencias y pendientes informados

- **Advertencia de build:** la línea base sobre `main` muestra 1 advertencia en una compilación completa (`CS2008: No se especificaron archivos de código fuente` en `GymBro.IA.csproj`, el proyecto de Python). Ya estaba antes de la etapa; la 9a-2 informó 0 porque su build fue incremental. No se tocó.
- **Pantallas no abiertas al Administrador:** `gimnasio/vinculaciones` y `gimnasio/configuracion` no tienen datos de salud, pero siguen siendo solo del Dueño; las cubren los scripts. No se le quitó ninguna pantalla que ya tenía (`app.routes.spec.ts`). Si el autor quiere abrirlas en la aplicación, corresponde una etapa propia.
- **Comparación de nombres en el script de ejercicios globales:** la aplicación ignora acentos al comparar nombres; el script solo ignora mayúsculas (no depende de la extensión `unaccent`). Está indicado en su encabezado.
- **Autor dado de baja:** "Propuesto por" se lee ignorando el filtro de usuarios (decisión P1), así que el nombre se sigue mostrando aunque el autor se haya dado de baja por script. Es intencional: el autor es parte de la trazabilidad del ejercicio.
- **Migración en la base local:** no se aplicó a `gymbro-db` (sin autorización del autor). Se aplica al levantar `GymBro.Api` (`MigrateAsync` en `Program.cs`). Es compatible con el código de `main`: las columnas nuevas tienen valor por defecto o aceptan nulos.
- **Sin verificar:** la prueba manual en pantalla y un embedding real con Ollama encendido para un ejercicio creado por la plataforma o el Personal Trainer.

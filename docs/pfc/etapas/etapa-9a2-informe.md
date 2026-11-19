# Etapa 9a-2 — Ejercicios y equipamiento propios del gimnasio: texto para el informe y registro de decisiones

**Fecha:** 18/11/2026. **Rama:** `etapa9a2`, creada desde `4c7583e` (`main`, que tiene un commit de evidencias de las etapas 6 y 8 posterior a `8953054`, sin cambios de código). **Estado del merge:** código en el commit `b447cd2` de la rama `etapa9a2`, y evidencia con este informe en `d5fbfe4`. Integrado a `main` por fast-forward el 18/11/2026, por decisión del autor, antes de la revisión de Claude Web; esa revisión queda pendiente. La evidencia se generó sobre el árbol de trabajo antes del commit `b447cd2`, con contenido de código idéntico.
**Evidencia:** `docs/pfc/evidencia/etapa9a2-*.txt`:
- `etapa9a2-estado-previo.txt`: estado previo de la base local (solo lectura);
- `etapa9a2-migracion.sql.txt`: SQL de la migración (Up aplicado y Down) y salida de psql;
- `etapa9a2-seeder.txt`: filas afectadas por `EquipamientoSeeder` en la base local;
- `etapa9a2-dotnet-build.txt`, `etapa9a2-dotnet-test.txt`, `etapa9a2-ng-build.txt`, `etapa9a2-ng-test.txt`: build y pruebas con fecha.

**Alcance a validar con los Profesores Orientadores:** esta función es una **extensión propuesta, pendiente de validación académica**. Según una lectura parcial de Claude Web, el Anexo II no la menciona; no se pudo contrastar por completo.

## Texto propuesto para 4.5 (implementación)

**Estado: Implementado y verificado con pruebas automáticas y en la base local (migración y seeder, 18/11/2026). Pendientes: la prueba manual en pantalla y la generación de un embedding real de un ejercicio propio con Ollama encendido.**

El catálogo de ejercicios era común a todos los gimnasios y el equipamiento era un texto libre del dataset, con 36 valores distintos y sucios (espacios de ancho cero, dobles espacios y nombres de ejercicio pegados). Por eso la rutina baseline no podía saber si un ejercicio se podía hacer en un gimnasio concreto. En esta etapa se agregaron tres capacidades:

1. **Equipamiento normalizado.** Se creó la tabla `tipos_equipamiento` con 13 tipos globales y Id fijo, por ejemplo `MANCUERNA`, `POLEA` y `PESO_CORPORAL`. El mapeo del texto del dataset a esos tipos es una lista ordenada de reglas (datos, no heurísticas) en `CatalogoEquipamiento`. Antes de comparar, el texto se normaliza: se quitan los espacios de ancho cero, se colapsan los espacios y se pasa a minúsculas. Un valor que no coincide con ninguna regla produce un error explícito y no se asigna en silencio. Peso corporal y estiramiento son "siempre disponibles". `EquipamientoSeeder` corre al arrancar en todos los entornos, después de `CatalogoProyectoSeeder`. Inserta los tipos que faltan y asigna el tipo solo a los ejercicios globales que todavía no lo tienen, sin pisar una asignación existente. En la base local asignó tipo a los 696 ejercicios y no quedó ninguno sin tipo.
2. **Equipamiento del gimnasio.** El Dueño marca los tipos que tiene, con una cantidad opcional, y puede crear tipos propios. Quitar un tipo es una baja lógica, y un índice único parcial garantiza un solo registro vigente por gimnasio y tipo. El Personal Trainer lo ve en solo lectura. El Administrador de la Plataforma lo ve en solo lectura para cualquier gimnasio, eligiéndolo de una lista.
3. **Ejercicios propios.** El Dueño da de alta, edita y da de baja (lógica) ejercicios visibles solo en su gimnasio. El gimnasio sale del JWT y el origen (`Gimnasio`) lo fija el servidor; si el cuerpo trae esos campos, se ignoran. Una restricción `CHECK` en la base exige que el origen y el gimnasio sean coherentes. El filtro global de EF muestra:
   - los ejercicios globales a todos;
   - los propios al personal del gimnasio (por el claim `gimnasio_id`) y al socio vinculado (por su ficha, porque la cuenta del socio no lleva `gimnasio_id`);
   - todos al Administrador de la Plataforma.

   Al guardar, el servidor arma el texto semántico en un único lugar de C#, con el mismo formato que `generador_embeddings.py`, y pide el embedding al microservicio de IA con un tiempo máximo de 5 s. Si la IA falla o tarda más, el ejercicio se guarda con `embedding = null`, que el script de Python ya interpreta como "pendiente de indexar". Al editar el texto, si la IA falla, el embedding queda en `null`; nunca se conserva el vector de un texto anterior.

La **rutina baseline** ahora usa el gimnasio **del socio**, aunque la genere otro rol. Toma ejercicios globales o de ese gimnasio, nunca de otro. Si el gimnasio declaró equipamiento, solo elige ejercicios cuyo tipo esté marcado o sea siempre disponible. Si no declaró nada, o el socio es independiente, el resultado es el mismo que en la 9a-1. Una zona sin ejercicios factibles se omite, y la observación de la rutina la distingue de una zona sin ejercicios en el catálogo.

**Advertencias:**
- La tabla de tipos y el mapeo son una **propuesta de diseño**, no una clasificación validada por un profesional. El Dueño puede corregir el equipamiento de su gimnasio, pero no el mapeo global.
- Los ejercicios que carga el Dueño **no pasan por ninguna validación profesional ni de seguridad**, y el baseline los puede prescribir. Queda como insumo de las reglas de seguridad de la 10a.
- Los textos del Dueño se vectorizan y alimentarán el RAG de la 9b. Por eso tienen límites de longitud (120 caracteres el nombre y el músculo, 2000 los textos libres) y se rechazan los caracteres de control e invisibles. Es una **sanitización mínima, no una defensa completa** contra la inyección de instrucciones.
- El filtro por equipamiento depende de que el Dueño lo declare bien: un gimnasio que marca poco puede recibir rutinas con menos zonas. La observación de la rutina lo dice.

## Texto propuesto para 4.6 (verificación)

| Criterio | Prueba | Resultado |
|---|---|---|
| Un ejercicio propio de A no aparece para B en listado, detalle (404), zonas, búsqueda inteligente ni baseline | `CatalogoGimnasioTests.Un_ejercicio_propio_de_A_no_aparece_para_B...` y las tres pruebas de baseline | Verificado |
| El Administrador de la Plataforma ve ejercicios y equipamiento de todos los gimnasios | `El_Administrador_de_la_Plataforma_ve_los_ejercicios...`, `..._lee_el_equipamiento_de_cualquier_gimnasio...` | Verificado |
| El Socio independiente ve solo lo global; el vinculado, lo global más lo de su gimnasio | Misma prueba y `Un_ejercicio_propio_de_A...` (socio A) | Verificado |
| El Dueño crea, edita y da de baja un ejercicio propio; uno global da 403 y uno de otro gimnasio, 404 | `El_Dueno_edita_y_da_de_baja...`, `El_Dueno_no_puede_tocar_un_global_403...`, matriz | Verificado |
| Entrenador y Socio no pueden crear, editar ni dar de baja (403) | `MatrizAutorizacionTests` (POST, PUT y DELETE `/api/Ejercicios`) | Verificado |
| `Origen` y `GimnasioId` los fija el servidor | `El_Dueno_crea_un_ejercicio_propio...` (el cuerpo intenta fijarlos) | Verificado |
| `CHECK` de coherencia entre origen y gimnasio; `CHECK` de cantidad; índice único parcial | `EquipamientoSeederTests.El_CHECK_...` (3 casos) | Verificado |
| Se rechazan nombre duplicado (sin mayúsculas ni acentos), zona inexistente, equipamiento no marcado, tipo de otro gimnasio, textos fuera de límite, caracteres invisibles o de control, dificultad y medida inválidas | `Un_pedido_invalido_se_rechaza_con_400` (10 casos), `Un_tipo_propio_de_otro_gimnasio...`, `CatalogoEquipamientoTests` | Verificado |
| Los 36 valores del dataset y los 8 de Core quedan con tipo; un valor desconocido falla de forma explícita | `CatalogoEquipamientoTests` (36 valores reales y 4 desconocidos), `EquipamientoSeederTests` y la base local (696 de 696) | Verificado |
| `EquipamientoSeeder` es idempotente, no pisa una asignación y corre en Production sin datasets | `EquipamientoSeederTests` (entorno Production) | Verificado |
| El Dueño marca equipamiento; no puede marcar un tipo de otro gimnasio; el tipo propio queda aislado | `El_Dueno_marca_equipamiento_y_crea_un_tipo_propio_aislado`, `Un_tipo_propio_de_otro_gimnasio...` | Verificado |
| Baseline con equipamiento declarado: solo ejercicios factibles; peso corporal siempre disponible | Unitaria `ConEquipamientoDeclarado_...` e integración `Baseline_con_equipamiento_declarado...` | Verificado |
| Baseline sin equipamiento declarado o con socio independiente: sin filtro (igual que la 9a-1) | Unitaria `SinEquipamientoDeclarado_...` e integración `Baseline_sin_equipamiento_declarado...` | Verificado |
| Zona sin ejercicios factibles: se omite, la observación lo dice y no supera 500 caracteres | Unitaria `ZonaSinEjerciciosFactibles_...` e integración `Baseline_con_una_zona_sin_equipamiento...` | Verificado |
| El baseline usa el gimnasio del socio aunque lo dispare otro rol | Integración: lo dispara la cuenta del socio, que no tiene `gimnasio_id`; unitaria `SeConsultaElGimnasioDelSocio...` | Verificado |
| Embedding: se genera al guardar; con la IA caída o lenta se guarda `null` sin error; al editar nunca queda un vector viejo | `Con_la_IA_caida_o_lenta...` (`IIaService` simulado: éxito, error y demora) | Verificado |
| El texto que se vectoriza coincide con `generador_embeddings.py` | Unitaria con literal esperado `El_texto_semantico_tiene_el_formato...` | Verificado (la copia de Python debe mantenerse igual) |
| La baja de un ejercicio en una rutina vigente se rechaza | `La_baja_de_un_ejercicio_en_una_rutina_vigente_se_rechaza` | Verificado |
| El historial de rutinas conserva el nombre del ejercicio tras la baja y tras dejar el gimnasio | `El_historial_conserva_el_nombre...` | Verificado (ver la decisión R1) |
| Pantallas nuevas y comportamiento por rol; el Administrador de la Plataforma las puede abrir todas | Specs `equipamiento.spec.ts`, `ejercicios-catalogo.spec.ts`, `ejercicio-form-dialog.spec.ts`, `app.routes.spec.ts`, `shell.spec.ts` | Verificado |
| Build y pruebas sin errores nuevos (piso: 111 / 222 / 147) | Evidencia `etapa9a2-*` | **170 / 287 / 164**, 0 errores y 0 advertencias en `dotnet build` |

**Sin verificar:** la prueba manual en pantalla y la generación de un embedding real de un ejercicio propio con Ollama encendido. Las pruebas usan un `IIaService` simulado.

## Registro de decisiones de la etapa

| # | Decisión | Estado |
|---|---|---|
| C1 | El filtro de `Ejercicio` y `TipoEquipamiento` para el Socio usa una subconsulta a su ficha (`Socios.Any(s => s.Id == SocioId && s.GimnasioId == e.GimnasioId)`), porque `CK_Usuario_Rol_Alcance` obliga a que la cuenta del Socio no tenga `gimnasio_id`. El Socio que se desvincula deja de ver los ejercicios propios en el momento. | Aprobada por el autor (18/11/2026) |
| R1 | Riesgo del historial: las consultas de rutinas y sesiones ya no usan `ThenInclude(Ejercicio)`. Ese INNER JOIN contra una navegación obligatoria hacía **desaparecer la fila entera** de la rutina cuando el filtro ocultaba el ejercicio; se detectó con una prueba de integración. El ejercicio se completa con una consulta aparte que ignora el filtro, limitada a los Id de una rutina o sesión ya autorizada (`EjerciciosDeHistorial`). | Aprobada por el autor (18/11/2026) |
| R2 | La baja de un ejercicio usado por una rutina `Aprobada`, `Editada` o `PendienteRevision` se rechaza con 400 (`ReglaNegocioException`, como el resto de las reglas de negocio), no con 409. | Decisión de diseño |
| N1 | Normalización del equipamiento: quitar U+200B/U+200C/U+200D/U+FEFF, colapsar espacios, recortar y pasar a minúsculas. Hay tres modos de regla: exacta; prefijo con palabra completa (una variante desconocida entre paréntesis no coincide); y prefijo libre, solo para `Stretch…` (cubre `StretchBroomstickDoorway`). `Assisted (partner)` se evalúa antes que `Assisted`. | Propuesta de diseño |
| D1 | `Lever` y `Sled` sin aclaración → `MAQUINA_OTRA`; `Assisted (partner)` → `PESO_CORPORAL`; `Weighted` → `PESO_ADICIONAL`. | **Provisional** (propuesta por defecto; el autor no respondió) |
| D2 | El Administrador de la Plataforma solo **lee** el equipamiento y los ejercicios de los gimnasios; ve y abre todas las pantallas nuevas. | **Provisional** |
| D3 | El baseline no prioriza los ejercicios propios del gimnasio sobre los globales. | **Provisional** |
| D4 | El Personal Trainer solo lee (catálogo y equipamiento). | **Provisional** |
| E1 | Volver a marcar un tipo quitado inserta una fila nueva; la anterior queda dada de baja como historial. | Decisión de diseño |
| E2 | Los tipos siempre disponibles no se guardan en `equipamientos_gimnasio`: se consideran marcados en cualquier gimnasio. | Decisión de diseño |
| E3 | Al crear un tipo propio no se lo marca en el servidor: la pantalla lo agrega marcado y se guarda con "Guardar". La clave se genera como `GIM_` más el nombre en mayúsculas, sin acentos; si se repite, se agrega un sufijo `_2`, `_3`, etc. | Decisión de diseño |
| V1 | Dificultad válida de 1 a 5: rango real verificado en la base local (dataset 1 a 5, Core 1 a 3). | Verificado |
| V2 | Límites de texto: 120 caracteres el nombre y el músculo principal, 2000 los textos libres; 80 el nombre de un tipo propio. Cantidad de equipamiento de 1 a 10 000. | Decisión de diseño |
| V3 | Tiempo máximo del embedding: 5 s, configurable en `Embeddings:TiempoMaximoSegundos`. | Decisión de diseño |
| V4 | Al editar sin cambiar el tipo de equipamiento, no se vuelve a exigir que siga marcado; al cambiarlo, sí. | Decisión de diseño |
| B1 | Si el equipamiento declarado deja sin ejercicios factibles a **todas** las zonas, el baseline responde 400 (regla de negocio) en lugar del 500 de catálogo incompleto, porque lo resuelve el Dueño declarando su equipamiento. | Decisión de diseño |
| A1 | No se agregó `EsSuperAdministrador` a `IUsuarioActual`: los filtros nuevos comparan `_actual.Rol == RolUsuario.SuperAdministrador`, como ya lo hacen los filtros de `Gimnasio` y `Usuario`. | Decisión de diseño |

## Diferencias y pendientes informados

- **Rama de partida:** `main` estaba en `4c7583e` y no en `8953054`. La diferencia es un commit de evidencias de las etapas 6 y 8 (solo archivos `.txt`).
- **Rutas:** las de equipamiento quedaron en `api/equipamiento` (`GET`, `PUT` y `POST tipos`), en un controlador nuevo, porque `MiGimnasioController` exige el rol de Dueño en toda la clase y la lectura también corresponde al Personal Trainer y al Administrador de la Plataforma. La pantalla está en `/gimnasio/equipamiento`.
- **Catálogo:** `GET /api/Ejercicios` acepta el parámetro opcional `origen`. Los DTO suman `GimnasioId`, `TipoEquipamientoId` y `TipoEquipamiento`.
- **Python:** `GymBro.IA/generador_embeddings.py` tiene su propia copia del texto semántico; debe coincidir con `EjercicioTextoSemantico` (lo fija una prueba del lado de C#).
- **Limitación:** un Dueño administra un solo gimnasio, el del JWT. La selección entre varios gimnasios queda como trabajo futuro.
- **Sin verificar:** la prueba manual en pantalla (escritorio y celular) y el embedding real de un ejercicio propio con Ollama.

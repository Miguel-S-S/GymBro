# Etapa 14 — Documentación técnica desde el código

Rama `cierre-pfc`. Commit de documentación: `79dbe09`. Evidencia: `docs/pfc/evidencia/etapa14-*.txt`. Documentos en `docs/arquitectura/`, generados desde el código de esta rama (cada uno lo indica en su encabezado) y citando archivos:

| Documento | Contenido | Fuente verificada |
|---|---|---|
| `arquitectura.md` | Capas, componentes, seguridad y aislamiento, flujos (rutina y sesión, búsqueda y sustitutos, asistencia con QR) con diagramas Mermaid | proyectos .NET, `Program.cs`, controladores, servicios, `GymBro.IA`, `GymBro.Analytics` |
| `modelo-dominio.md` | Conceptos, reglas e invariantes por entidad; diagrama de clases | `GymBro.Domain/Entities`, `Enums`, `Rules` |
| `modelo-datos.md` | Diagrama entidad-relación desde las claves foráneas del snapshot de migraciones; restricciones; lista de las 18 migraciones; esquema `analitica` | `GymBroDbContextModelSnapshot.cs`, `GymBroDbContext.cs`, `rol-analitica.sql` |
| `casos-de-uso.md` | Casos de uso por actor con endpoint y regla de acceso | `GymBro.Api/Controllers`, `app.routes.ts`, `MatrizAutorizacionTests.cs` |
| `requisitos.md` | Requisitos reconstruidos desde lo implementado y **tabla de trazabilidad** objetivo – requisito – componente – prueba – evidencia | código, pruebas y `docs/pfc/evidencia` |
| `estado-implementacion.md` | Implementado y verificado / Parcial / Especificado / Pendiente / Desviación / Trabajo futuro | informes de etapa y evidencia |

## Decisiones

- **D-14-1. Solo lo verificado.** Las relaciones del diagrama entidad-relación se extrajeron con una expresión regular sobre el snapshot de EF Core (no a mano); las restricciones citan dónde se definen en `GymBroDbContext.cs`; los nombres de pruebas y archivos de evidencia citados se comprobaron en el repositorio.
- **D-14-2. Los requisitos se reconstruyen** a partir de lo implementado y de los pedidos de cada etapa; no se agregan requisitos que no tengan código.
- **D-14-3. Mermaid** para todos los diagramas (se renderiza en GitHub y en la mayoría de los editores de Markdown).

## Texto propuesto para 4.5 y 4.6

La documentación técnica se generó al cierre a partir del código versionado en la rama `cierre-pfc`, de modo que describe el sistema tal como está implementado y no como fue planificado. Cada documento indica la rama de origen y cita los archivos de los que surge cada afirmación; las funcionalidades se clasifican según la evidencia disponible: solo se consideran "implementadas y verificadas" las que tienen pruebas automatizadas o una ejecución guardada en `docs/pfc/evidencia`.

## Verificación

- `etapa14-citas.txt`: un script comprobó las 182 citas de archivos y de clases (`…Tests`, `…Service`) de los seis documentos contra el repositorio; no faltó ninguna.
- `etapa14-*.txt` (build y pruebas sobre `79dbe09`, sin cambios de código): `dotnet build` 0 errores; 382 pruebas unitarias, 585 de integración; `ng build` correcto y 253 pruebas del cliente.

## Desvíos y limitaciones

- La documentación no se verificó con un renderizador de Mermaid en esta etapa (se revisó la sintaxis a mano).
- `estado-implementacion.md` refleja el estado al commit de esta etapa; los puntos de espera 1 y 2 y la decisión sobre aplicar sustitutos lo cambiarán.

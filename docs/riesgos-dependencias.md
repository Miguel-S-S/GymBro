# Riesgos aceptados en dependencias

Registro de vulnerabilidades conocidas que no tienen corrección disponible y se aceptan temporalmente, con su justificación y el criterio para revisarlas.

## RD-01 · picomatch: ReDoS con extglobs repetidos (GHSA-fc5q-39xc-vjpf)

| Campo | Valor |
|---|---|
| Registrado | 17/11/2026 |
| Estado | **Aceptado temporalmente**: sin versión corregida publicada |
| Paquete | `picomatch` 4.0.7 (transitivo; versiones vulnerables: todas hasta la 4.0.7 inclusive) |
| Gravedad del aviso | Alta |
| Aviso | https://github.com/advisories/GHSA-fc5q-39xc-vjpf (publicado el 19/10/2026) |
| Proyecto | `GymBro.Web` (solo dependencias de desarrollo) |
| Evidencia | `docs/pfc/evidencia/npm-audit-2026-11-17.txt` |
| Última verificación | 17/11/2026 16:14 (UTC−3): `npm view picomatch version` → 4.0.7; GHSA consultado por la API de GitHub (actualizado el 17/11/2026 15:56 UTC): rango vulnerable `<= 4.0.7`, sin `first_patched_version` |

**Qué es.** Un patrón glob malicioso del tipo `+(*)` provoca un backtracking exponencial y bloquea el event loop de Node.js (denegación de servicio).

**Por qué aparecen 12 advertencias.** Es una sola vulnerabilidad. `npm audit` (y el SDK de JavaScript de Visual Studio, que lo ejecuta al compilar `GymBro.slnx`) también marca a cada paquete que depende de `picomatch`: `@angular/build`, `@angular/cli`, `@angular-devkit/core`, `@angular-devkit/architect`, `@angular-devkit/schematics`, `@schematics/angular`, `@parcel/watcher`, `fdir`, `tinyglobby`, `vite` y `vitest`.

**Por qué se acepta.**
1. **No hay corrección.** 4.0.7 es la última versión publicada de `picomatch` y está dentro del rango vulnerable. Actualizar Angular a 22.2.7 no lo resuelve. `npm audit fix --force` propone bajar `@angular/cli` a 16.2.0, lo que rompe el proyecto.
2. **No llega a producción.** `picomatch` solo está en las herramientas de compilación, pruebas y observación de archivos: `npm ls picomatch --omit=dev` está vacío, `npm run audit:prod` informa 0 vulnerabilidades y el bundle de `dist/` no lo contiene.
3. **El ataque requiere controlar los patrones glob.** En este proyecto los patrones salen de la configuración del propio repositorio (`angular.json`, Vitest), no de datos de usuarios.

**Riesgo residual.** Bajo. Se limita a una denegación de servicio del entorno de desarrollo si alguien introdujera un patrón malicioso en la configuración del repositorio.

**Controles.**
- `npm run audit:prod` (en `GymBro.Web`): audita solo las dependencias que llegan al bundle; tiene que dar 0. Correrlo junto con `ng build` antes de cada entrega.
- `dotnet build GymBro.slnx` sigue mostrando las 12 advertencias a propósito: la auditoría de npm no se desactivó, para no ocultar vulnerabilidades futuras.

**Cuándo revisar.** Al cierre de cada etapa, o antes si se publica `picomatch` > 4.0.7. Entonces: `npm update` (o la actualización de Angular y Vitest que lo incorpore), `npm audit` sin advertencias de `picomatch`, y cerrar este registro.

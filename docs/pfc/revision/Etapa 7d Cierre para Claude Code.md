# Etapa 7d — Cierre documental — Instrucciones para Claude Code

Fecha: 24/11/2026. Emisor: Claude Web. Rama: `etapa7d` (commit `0cdbe67`). **No se toca código, migraciones ni pruebas.** Solo documentación y evidencia, en un segundo commit.

## Reglas
- No ejecutar `DELETE` en la base ni tocar Active Directory. Solo lecturas en `gymbro_db` si hicieran falta (no deberían).
- No modificar `main`. No instalar `gh` ni abrir el pull request: lo abre el autor desde el navegador.
- Si algo de esta lista no se puede cumplir tal cual, detenerse y proponer, sin improvisar.

## Tareas
1. **Copiar al repositorio** `docs/pfc/scripts/corregir-catalogo-piernas.sql` con el texto exacto del archivo que adjunta el autor (no reescribirlo). El autor ya lo ejecutó; el repositorio solo lo versiona. No ejecutarlo.
2. **Crear** `docs/pfc/evidencia/etapa7d-catalogo-antes.txt` con la salida literal de **B12 del Paso 0b** (consulta de solo lectura del 23/11/2026, anterior a la corrección), que pega el autor. Encabezado: qué es, fecha del Paso 0b, y que es el estado anterior a `corregir-catalogo-piernas.sql`. No regenerarla: la base ya está corregida.
3. **Corregir `docs/pfc/etapas/etapa-7d-informe.md`:**
   - Encabezado: la rama `etapa7d` está en GitHub (`origin/etapa7d`) y todavía no hay pull request a `main`; quitar "sin push".
   - Pendiente 1: sin cambios (hecho).
   - Pendiente 2: aclarar que los 14 socios sin la firma del dataset son los 13 del Paso 0b más el socio de validación, creado el 24/11/2026, y que el paso 3 sigue sin ejecutar a la espera de la decisión del autor (Claude Web recomienda `ValidacionFuncional` para cuentas de prueba confirmadas, nunca `Sintetico`).
   - Pendiente 3: marcarlo como resuelto: script copiado y evidencia "antes" tomada del Paso 0b.
   - Pendiente 10: la rama está pusheada; integración a `main` pendiente de la revisión de Claude Web y la prueba manual.
   - Decisión T2: agregar que la especificación v3 queda enmendada en ese punto (máximo por código de protocolo).
   - Agregar una sección breve **"Revisión de Claude Web (24/11/2026)"** que resuma: conforme con la v3, hallazgos M1 a M4 y L1 a L4 (los de `Revision Etapa 7d (24-11-2026).md`) y qué quedó resuelto con este cierre. Sin copiar el documento entero.
4. **Regenerar** `etapa7d-dotnet-test.txt` y `etapa7d-ng-test.txt` (y los dos de build) ejecutando `dotnet build`, `dotnet test GymBro.slnx`, `npx ng build` y `npx ng test --watch=false` **sobre el árbol limpio de la rama** (`git status` sin cambios en archivos rastreados). El encabezado de cada archivo debe decir el hash real del commit probado (`0cdbe67`) y que el árbol estaba limpio. Si los resultados difieren de los anteriores (342, 468 y 211), **detenerse e informar** antes de commitear.
5. **Commit** en `etapa7d` con un mensaje del estilo `etapa 7d: cierre documental (script de catalogo, evidencia antes, informe y registros de pruebas sobre arbol limpio)` y **push** a `origin/etapa7d`.

## Informe al terminar
- Hash del nuevo commit y `git diff --stat 0cdbe67..HEAD` (deben ser solo archivos de `docs/`).
- Resultados reales de build y pruebas (cifras literales).
- Confirmación de que `main` no cambió (`git diff main..origin/main --stat` vacío contra el `main` de antes).
- Cualquier diferencia o problema encontrado.
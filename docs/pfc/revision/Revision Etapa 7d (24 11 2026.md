# Revisión de la Etapa 7d (24/11/2026)

Emisor: Claude Web (orquestador). Objeto revisado: rama `etapa7d` en GitHub, commit único `0cdbe67` sobre `c29890f` (67 archivos, +7821 −26). `main` no tiene cambios. Contraste contra la especificación v3 del 23/11/2026.

## 1. Alcance y límites de esta revisión
**Revisé:** informe de la etapa, toda la evidencia (`etapa7d-*`), la migración (`Up` y `Down`), `PlanificadorPiernas`, `ProtocoloPiernasConfig`, `PrescripcionPiernasService`, `ProtocoloPiernasRepository`, `KpiProgresionService`, los dos controladores nuevos y el cambio en `PrescripcionesController`, el vínculo sesión → línea (`SesionEntrenamientoService`, `ReglasSeguridadService`), `RevisionPrescripcionService`, los cambios en `AuthService`, `SocioService`, `DataSeeder` y `AccesoDtos`, la configuración de `GymBroDbContext` y los cambios del cliente (`rutinas-panel.ts`, `rutina-ejercicio-card.ts`). Confirmé con `git diff` que `PrescripcionBaselineService`, `BaselineRepository`, `IBaselineRepository` y `PrescripcionBaselineServiceTests` **no tienen cambios**.

**No revisé línea por línea:** `KpisProgresion.cs` (funciones puras K1 a K4), `LimitacionesArticularesService`, el `Designer` y el snapshot de la migración, y el contenido de las pruebas nuevas. Me apoyo en su descripción del informe.

**No pude compilar ni ejecutar pruebas** (mi entorno no tiene .NET). Las cifras de pruebas (342 + 468 + 211, todas en verde) son las de los registros de Claude Code, no las reproduje.

## 2. Veredicto
**Conforme con la especificación v3 en todo lo revisado, con tres condiciones antes de integrar a `main`:** (a) prueba manual en pantalla, (b) corregir el informe y completar la evidencia del catálogo, (c) decidir el paso 3 de la clasificación de socios. Ningún hallazgo bloquea la arquitectura ni exige cambiar código.

## 3. Criterios de aceptación
| CA | Estado | Base |
|---|---|---|
| CA1 solo los 10 curados, sin duplicados en inglés | Implementado; verificado por pruebas automáticas | Planificador usa la lista blanca `ejercicios_protocolo`; prueba de integración con un ejercicio Legs del dataset que debe quedar afuera |
| CA2 determinismo y rotación | Implementado; verificado por pruebas | Sin reloj ni azar; índice `(generación − 1) mod n` confirmado en el código |
| CA3 exclusión por limitación con motivo | Implementado; verificado por pruebas | `Exclusiones()` y `MotivoSeleccion` |
| CA4 reducción por alerta | Implementado; verificado por pruebas | `min(series, max(2, round(series × 0,7)))` (T3, equivalente a la especificación con los valores por defecto) |
| CA5 sin RIR 0, todas pasan R-LIM | Implementado; verificado por pruebas | `rirMinimo = max(1, …)`, validación de salida y `IValidadorPrescripcionService` antes de persistir |
| CA6 trazabilidad persistida | Implementado; **verificado en la base real solo la estructura** | La siembra y las columnas están verificadas en `gymbro_db`; no hay todavía ninguna rutina generada con el protocolo |
| CA7 vínculo sesión → línea | Implementado; verificado por pruebas | `ResolverLinea`; las 0 sesiones existentes siguen sin vínculo (esperado) |
| CA8 K1 a K4 | Implementado; verificado por pruebas | Lógica de K1-K4 no revisada por mí línea por línea |
| CA9 regresión | Cumple según los registros | Baseline sin cambios; pruebas previas solo con casos agregados |
| CA10 autorización | Implementado; verificado por pruebas | Los controladores excluyen al Administrador de la Plataforma por rol; matriz ampliada |
| CA11 informe y evidencia | **Parcial** | Ver hallazgos M1 y M2 |

## 4. Hallazgos
| # | Severidad | Hallazgo | Acción |
|---|---|---|---|
| M1 | Media | El informe está desactualizado: dice "commit local, sin push" y el pendiente 10 dice "sin autorización"; la rama ya está en GitHub. Además presenta "14 socios sin la firma (uno más que en el Paso 0b)" como una diferencia sin explicar | Corregir el informe. La explicación: son los 13 del Paso 0b más el socio de validación, creado el 24/11 |
| M2 | Media | Falta `etapa7d-catalogo-antes.txt` y el repositorio no tiene `corregir-catalogo-piernas.sql`, aunque el autor lo ejecutó | El estado anterior ya existe: es la salida literal de **B12 del Paso 0b** (23/11), de solo lectura, con la Prensa en `Gimnasio`/`MAQUINA_ASISTIDA` y el Hack en `MAQUINA_OTRA`. Guardarla como evidencia "antes". Copiar el script al repositorio |
| M3 | Media | Sin prueba manual en pantalla (pendiente 4 del informe). Hasta que no se genere una rutina real con el protocolo, CA6 queda verificado solo en estructura y por pruebas contra una base descartable | Prueba manual con la lista de la sección 5 |
| M4 | Media | Paso 3 de la clasificación sin decidir. Los 13 socios sin la firma del dataset son, según la guía de cuentas de prueba, cuentas de demostración y de prueba funcional | Ver sección 6: no marcarlos `Sintetico` |
| L1 | Baja | El revisor humano (pantalla Revisiones) no ve rol, tempo ni carga sugerida de las líneas del protocolo (desvío 9) | Aceptado como limitación declarada; resolver en la 7c, que toca esas pantallas |
| L2 | Baja | Dos generaciones simultáneas del mismo socio: la segunda falla con 500 por el índice único (desvío 7) | Aceptado. Mejora futura: responder 409 con mensaje claro |
| L3 | Baja | Los registros de build y pruebas se generaron antes del commit y mencionan `c29890f` con "cambios sin commit" | Repetir `dotnet test` y `ng test` sobre el árbol limpio de la rama y regenerar los dos registros con el hash del commit |
| L4 | Baja | `T2` (el máximo de `numero_generacion` por código y no por versión) cambia el texto de la especificación | Aceptada: es lo correcto por el índice único `(socio_id, protocolo_codigo, numero_generacion)`. La especificación v3 queda enmendada en ese punto |

## 5. Prueba manual en pantalla (con el perfil de validación)
Perfil: 43 años, 1 medición, consentimiento, sin historial, sin alertas. El socio es `ValidacionFuncional`, así que **la generación funciona igual** (el origen solo afecta a los KPIs).

**Generación 1, sin limitaciones, resultado esperado según el código:**
- 6 ejercicios, en este orden: Prensa de piernas 45° (Referencia), Sentadilla Hack (Referencia), Sentadilla trasera con barra libre (Variable), Curl femoral sentado (Referencia), Curl femoral acostado (Variable), Elevación de talones de pie (Variable).
- Series 3, 3, 3, 2, 2, 3: total 16.
- Repeticiones 8-12 para los multiarticulares, 10-15 para los curls, 15-20 para el talón.
- RIR 2 en todos. Descanso 180 s en los multiarticulares (por tener 40 años o más) y 75 s en los curls. Tempo excéntrico 4 s en todos.
- Sin carga sugerida (no hay historial). La tarjeta muestra rol y tempo.
- Estado inicial según la política de revisión del gimnasio (pendiente o aprobada).

**Generación 2** (después de aprobar o rechazar la anterior): Sentadilla frontal, Peso muerto rumano y Elevación de talones sentado como variables.

**Con limitaciones** (guardar y volver a generar):
- Lumbar Moderada: desaparecen la Sentadilla trasera y el Peso muerto rumano.
- Rodilla Moderada: desaparece la Extensión de cuádriceps. No cambia la composición de la generación 1.
- Rodilla Alta: la generación falla con un mensaje que nombra el grupo Cuádriceps (consecuencia de la tabla provisional, desvío 3).

**Verificaciones en la base después de generar** (solo lectura): `numero_generacion` 1, `origen_datos = ValidacionFuncional`, `entrada_snapshot` sin nombre, DNI ni email, y `motivo_seleccion` en cada línea.

## 6. Clasificación de socios: decisión recomendada
No ejecutar el paso 3 como está escrito. Los 13 socios sin la firma del dataset son cuentas de demostración y de prueba funcional (Lucas, Ana, Mario, Sofía y otras creadas para probar roles). **No son datos sintéticos del estudio:** el estudio debe salir solo del generador de la Etapa 7e, con parámetros conocidos. Marcarlos `Sintetico` los metería en los KPIs.

Recomendación: si el autor confirma con la lista del PASO 1 que los 13 son cuentas de prueba, marcarlos `ValidacionFuncional` (queda fuera del estudio, que es lo que importa, y el rótulo es el correcto). Si no está seguro de alguno, dejarlo como está (`DatasetExterno`), que también lo excluye. No es urgente y no bloquea la integración.

## 7. Estado y siguiente paso
La etapa está **implementada y verificada con pruebas automáticas; falta la verificación en pantalla**. Orden recomendado:
1. El autor hace la prueba manual (sección 5) y confirma el resultado.
2. Claude Code ejecuta el cierre (`Etapa 7d - Cierre para Claude Code.md`): segundo commit en la rama, solo documentación y evidencia.
3. Claude Web confirma el cierre. El autor abre el pull request desde el navegador y lo integra a `main`.
4. Siguiente etapa, a decidir por el autor: la **7e** (datos sintéticos reproducibles: sin ella no hay datos para los KPIs del estudio) o la **9b-1** (rama `etapa9b1` desde el `main` posterior a la integración).
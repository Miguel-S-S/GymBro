# Etapa 7e — Generador sintético reproducible

> **Datos sintéticos (`Sintetico`): las métricas verifican el procedimiento y no el desempeño con personas.**

Rama `cierre-pfc`. Commits de código: `d82bb44` y `f8b2293` (la asistencia y la cuota heredan el origen del socio). Evidencia: `docs/pfc/evidencia/etapa7e-*.txt`. Parámetros: `docs/pfc/sintetico-parametros.md`. Sin migración.

## Alcance implementado

| Pedido | Estado | Dónde |
|---|---|---|
| Comando de desarrollo, solo en `Development` y con indicador explícito | Implementado y verificado | `Program.cs` (opciones `--generar-sintetico` y `--eliminar-sintetico`), `GymBro.Infrastructure/Sintetico/ModoSintetico.cs` |
| Gimnasio sintético y N socios (60 por defecto), 12 semanas, 2 sesiones por semana | Implementado y verificado | `GeneradorSintetico.cs`, `ParametrosSinteticos.cs` |
| Uso de los servicios reales | Implementado y verificado | planificador de piernas, reglas de seguridad, sesiones y series, bienestar, asistencia y cuotas (ver tabla del documento de parámetros) |
| `origen_datos = Sintetico` en todo | Implementado y verificado | socios; rutinas, asistencias y cuotas heredan el origen del socio (`f8b2293`); sesiones, series, bienestar y alertas no tienen columna de origen y se identifican por su socio |
| Parámetros conocidos y declarados, sin fuente | Implementado | `docs/pfc/sintetico-parametros.md` |
| Determinismo con hash del conjunto | Implementado y verificado | `AlmacenSintetico.ResumirAsync`, `GeneradorSinteticoTests` |
| Ids derivados de la semilla | **Parcial** (desvío 1) | gimnasio, Dueño, socios y cuentas |
| Comando de eliminación de lo sintético y nada más | Implementado y verificado | `AlmacenSintetico.EliminarAsync` |
| K1–K4 recuperan los parámetros dentro de tolerancias declaradas | Implementado y verificado (K4: solo la completitud; ver decisión D-7e-5) | `GeneradorSinteticoTests` |

## Texto propuesto para 4.5 (implementación)

El generador es un modo de ejecución de la propia API. Si el proceso recibe la opción `--generar-sintetico` o `--eliminar-sintetico`, `Program.cs` verifica que el entorno sea `Development` (si no lo es, termina con código 2 antes de leer la configuración de la base) y reemplaza dos servicios: la identidad del pedido (`IUsuarioActual`) por una identidad simulada que el generador cambia en cada paso (el Dueño del gimnasio sintético para generar rutinas, registrar sesiones, asistencias y cuotas; el Socio para su bienestar) y el reloj (`TimeProvider`) por un reloj simulado que el generador fija en cada evento, porque los servicios calculan "hoy" con el reloj inyectado. En ese modo la API no atiende pedidos HTTP: aplica las migraciones como en cualquier arranque, ejecuta la opción y termina.

Con una semilla y los parámetros, el generador crea un gimnasio (código `SINT` más la semilla en hexadecimal, sin revisión humana de rutinas para que la rutina generada quede vigente), su Dueño y N socios con origen `Sintetico`, consentimiento y cuenta sin contraseña utilizable; los identificadores de estas filas se derivan de la semilla con SHA-256. Cada socio tiene su propia secuencia pseudoaleatoria derivada de (semilla, índice), de modo que su historia no depende del orden ni de la cantidad de los demás. La simulación recorre las semanas día por día: el primer día y cada día 1 se generan las cuotas del mes y se sortea si cada socio paga a tiempo, tarde o no paga; cada lunes se genera la rutina de piernas con el planificador real; en los días programados el socio asiste con su probabilidad de adherencia y, si asiste, se registra la asistencia y la sesión con sus series, cuya carga sigue una ganancia semanal compuesta según el nivel, con ruido; las repeticiones y el RIR siguen distribuciones normales redondeadas alrededor de lo prescrito, con un sesgo de RIR y efectos de la fatiga y la ingesta del día; cada día se sortean la fatiga y la ingesta latentes y, con cierta probabilidad, se registra el bienestar con la FC de reposo; cada domingo se evalúa el abandono con una función logística de la adherencia de las últimas cuatro semanas y de la morosidad. Todas las escrituras de negocio pasan por los servicios reales, que a su vez disparan las reglas de seguridad (R-RIR-1, R-PROG-1 y R-FC-1) y la validación R-LIM.

Para verificar el determinismo se calcula un resumen SHA-256 del contenido del gimnasio sintético (fichas, rutinas y sus líneas, sesiones y series, bienestar, asistencias, cuotas y alertas), ordenado de forma canónica y sin los identificadores que los servicios crean internamente. El comando de eliminación borra, dentro de una transacción, los socios con origen `Sintetico` de gimnasios con el prefijo `SINT` y todos sus datos, y el gimnasio solo si ya no le quedan socios; la auditoría, que es de solo agregado, no se borra.

## Decisiones técnicas

- **D-7e-1. Modo de la API y no un proyecto aparte.** Reutiliza exactamente la configuración y la inyección de dependencias de la API, sin duplicarlas; el riesgo de exposición se acota con la doble condición (opción explícita y `Development`) y porque en ese modo no se atienden pedidos HTTP.
- **D-7e-2. Identidad y reloj simulados.** Los servicios exigen un rol (por ejemplo, el bienestar solo lo carga el Socio) y calculan el día con el reloj; simular ambos permite usar los servicios reales sin modificarlos. Las clases (`ContextoSintetico`, `UsuarioActualSintetico`, `RelojSintetico`) solo se registran en el modo generador o en las pruebas.
- **D-7e-3. El socio sintético no sigue la carga sugerida ni las sugerencias de R-PROG-1.** Así K1 mide un parámetro conocido y no la regla. Se declara en el documento de parámetros.
- **D-7e-4. Sin limitación de rodilla alta.** Con rodilla alta el protocolo no tiene ejercicios de cuádriceps y la generación se rechaza (comportamiento aprobado de la 7d); el generador sortea solo "sin limitación" o "moderada".
- **D-7e-5. Qué recupera K4.** El índice de seguridad depende de las alertas que emiten las reglas reales y no de un único parámetro; se verifica la parte que sí es un parámetro (proporción de sesiones con todas las series prescritas) y el índice se informa como valor descriptivo.
- **D-7e-6. Tolerancias declaradas antes de ejecutar.** RIR ± 0,15; series en rango ± 0,05; ganancia semanal ± 0,005; completitud ± 0,08; volumen K2 igual a la suma de las series. Están en el código de la prueba y en el documento de parámetros.

## Texto propuesto para 4.6 (verificación)

Pruebas unitarias (`GymBro.Tests/GeneradorSinteticoModeloTests.cs`, 15): secuencias iguales con la misma semilla y distintas con otra; ids derivados estables; la función de distribución normal contra la tabla; la probabilidad de repeticiones en rango y la esperanza del desvío de RIR del modelo contra simulaciones de 200 000 casos (dos decimales); el riesgo de abandono crece con la inadherencia y la morosidad; la carga compuesta; parámetros inválidos.

Prueba de integración (`GymBro.Tests.Integration/GeneradorSinteticoTests.cs`, contra PostgreSQL descartable con el catálogo del protocolo con la forma de la siembra `piernas-1.0`): se genera la semilla 7 (12 socios, 8 semanas), se elimina, se vuelve a generar con la misma semilla y se genera la semilla 8; se verifica que las dos corridas de la semilla 7 tienen el mismo resumen SHA-256 y los mismos conteos, que la semilla 8 da otro resumen y otros ids, que el generador solo crea socios y rutinas `Sintetico`, que la eliminación borra exactamente lo generado y deja intactos un socio `Real` de otro gimnasio, un socio `Sintetico` de un gimnasio que no es sintético y ese gimnasio, y que K1–K4 calculados con el servicio real (`IKpiProgresionService`) recuperan los parámetros. Además se verificó a mano que fuera de `Development` el proceso termina con código 2 (`etapa7e-produccion-rechazo.txt`).

### Resultados (datos sintéticos)

Suite (12 socios, 8 semanas, semilla 7; primera corrida de la prueba, mismos valores en `f8b2293` porque el origen no interviene en la simulación):

| Comparación | Observado | Esperado según los parámetros | Tolerancia | Resultado |
|---|---|---|---|---|
| K3, series en rango | 0,8185 (n = 2248) | 0,8213 | ± 0,05 | dentro |
| K3, desvío RIR medio | 0,5738 (n = 2248) | 0,5556 | ± 0,15 | dentro |
| K1, ganancia semanal Principiante | 0,0197 (41 pendientes) | 0,0200 | ± 0,005 | dentro |
| K1, ganancia semanal Intermedio | 0,0110 (28) | 0,0100 | ± 0,005 | dentro |
| K1, ganancia semanal Avanzado | 0,0034 (29) | 0,0050 | ± 0,005 | dentro (por debajo, ver limitaciones) |
| K4, sesiones con todas las series | 0,8611 (n = 144) | 0,85 | ± 0,08 | dentro |
| K2, volumen total | igual a la suma de las series | — | igualdad | igual |

Corrida con el tamaño por defecto (60 socios, 12 semanas, semilla 7; `etapa7e-corrida-60x12.txt`, commit `f8b2293`, 4,3 minutos para tres generaciones, una eliminación intermedia, los KPIs y la eliminación final):

| Comparación | Observado | Esperado según los parámetros | Tolerancia | Resultado |
|---|---|---|---|---|
| K3, series en rango | 0,8227 (n = 15 089) | 0,8219 | ± 0,05 | dentro |
| K3, desvío RIR medio | 0,5546 (n = 15 089) | 0,5575 | ± 0,15 | dentro |
| K1, ganancia semanal Principiante | 0,0201 (171 pendientes) | 0,0200 | ± 0,005 | dentro |
| K1, ganancia semanal Intermedio | 0,0101 (208) | 0,0100 | ± 0,005 | dentro |
| K1, ganancia semanal Avanzado | 0,0049 (100) | 0,0050 | ± 0,005 | dentro |
| K4, sesiones con todas las series | 0,8496 (n = 951) | 0,85 | ± 0,08 | dentro |
| K4, índice de seguridad (descriptivo) | 0,7992 | — | — | — |

Volumen de esa corrida: 60 socios, 951 sesiones, 16 040 series, 592 rutinas, 2494 registros de bienestar, 951 asistencias, 180 cuotas y 137 alertas. Resumen SHA-256 de las dos corridas de la semilla 7: `8fa319a9…cb2` (idénticos). Semilla 8: `06afb8c5…9e3`.

Reproducibilidad entre procesos (`etapa7e-reproducibilidad.txt`): la prueba de la semilla 7 (12 socios, 8 semanas) ejecutada en dos procesos distintos, cada uno con su base descartable, dio el mismo resumen `41a7b5c7…7505` en las cuatro generaciones.

## Cifras de la verificación

Sobre el commit `f8b2293` (`etapa7e-*.txt`): `dotnet build` 0 errores (solo la advertencia previa CS2008 de `GymBro.IA.csproj`); 370 pruebas unitarias (355 + 15); 543 de integración (536 + 7); `ng build` correcto; 250 pruebas del cliente (sin cambios en el cliente). Fuera de `Development` el modo generador termina con código 2 (`etapa7e-produccion-rechazo.txt`).

## Desvíos

1. **Ids derivados de la semilla, parcial.** Se derivan de la semilla el gimnasio, el Dueño, los socios y sus cuentas. Las sesiones, series, rutinas, líneas y alertas los crean los servicios reales con `Guid.NewGuid()`; cambiarlo exigiría modificar los servicios de negocio para un fin de prueba. Por eso el resumen de reproducibilidad excluye esos ids y compara el contenido. Misma semilla y parámetros producen el mismo contenido (verificado), no los mismos ids internos.
2. **Fechas de alta de filas creadas por los servicios.** Algunas entidades toman su fecha de alta de `DateTime.UtcNow` (valor por defecto de `EntidadAuditable`) y no del reloj inyectado; no entran en el resumen.
3. **`origen_datos` en asistencias y cuotas (corregido en la etapa).** En la primera versión (`d82bb44`) los servicios de la etapa 11 guardaban el valor por defecto `Real` porque no recibían el origen, lo que contradecía la regla de que todo dato generado lleve `Sintetico`. Se corrigió en `f8b2293`: la asistencia y la cuota heredan el origen del socio, igual que la rutina en el planificador. Las pruebas lo verifican en la etapa 11 y en la 7e. Es un cambio sobre código de esta misma rama (etapa 11), no sobre una decisión aprobada.
4. **Morosidad y abandono no cambian el estado administrativo**: el socio que abandona sigue `Activo` y se le siguen generando cuotas (decisión de diseño, declarada en el documento de parámetros).

## Limitaciones

- En la suite chica (12 socios, 8 semanas), K1 del nivel Avanzado sale por debajo del parámetro (0,0034 contra 0,005): con pocos socios y pocas semanas, una ganancia semanal chica queda mezclada con el redondeo de la carga a 0,5 kg. Con el tamaño por defecto se recupera (0,0049). Queda dentro de la tolerancia en ambos casos; la tolerancia no se ajustó después de ver los resultados.
- No se ejecutó el generador contra `gymbro_db` (lo hace el desarrollador; al arrancar en modo generador la API aplica las migraciones pendientes, incluida la de la etapa 11).
- La generación es secuencial: con 60 socios y 12 semanas, cada generación tarda del orden de un minuto contra una base local (la corrida de evidencia hizo tres generaciones y los KPIs en 4,3 minutos).

## Pendientes


- Ejecutar `dotnet run --project GymBro.Api -- --generar-sintetico` en la máquina del desarrollador (base de desarrollo) para alimentar las etapas 10b-1 y 12.

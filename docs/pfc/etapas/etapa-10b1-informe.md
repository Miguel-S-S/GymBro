# Etapa 10b-1 — Modelo de abandono (churn)

> **Datos sintéticos (Sintetico): las métricas verifican el procedimiento y no el desempeño con personas.**

Rama `cierre-pfc`. Commit de código: `8c8b2cd`. Evidencia: `docs/pfc/evidencia/etapa10b1-*.txt`. Reporte de la ejecución: `docs/pfc/analitica/reporte-abandono-20261125-213201.md` (y `.json`). Migración: `Etapa10b1_PrediccionAbandono` (SQL sin conexión en `etapa10b1-migracion.sql.txt`; **no se aplicó a `gymbro_db`**).

## Alcance implementado

| Pedido | Estado | Dónde |
|---|---|---|
| Proyecto `GymBro.Analytics/` con `requirements.txt` fijado | Implementado y verificado (instalado con Python 3.14.6) | `GymBro.Analytics/requirements.txt` |
| Extracción con un rol de solo lectura y script del rol | Implementado y verificado | `docs/pfc/scripts/rol-analitica.sql`, `gymbro_analytics/conexion.py`, `extraccion.py` |
| Variables: frecuencia de 4 semanas, días desde la última asistencia, tendencia, cuotas vencidas, antigüedad, edad, nivel | Implementado y verificado | `gymbro_analytics/variables.py` |
| Etiqueta definida y declarada | Implementado: sin asistencias en los 30 días desde el corte | idem |
| Regresión logística y bosque aleatorio (`scikit-learn`), línea base de la clase mayoritaria | Implementado y verificado | `gymbro_analytics/modelos.py` |
| Separación temporal sin fuga | Implementado y verificado | idem (`separar_temporal`) |
| Accuracy, Precision, Recall, F1, ROC-AUC, matriz de confusión e intervalo por remuestreo | Implementado y verificado | idem (`metricas`, `intervalos`) |
| Tabla `prediccion_abandono` con migración | Implementado y verificado | `PrediccionAbandono.cs`, `GymBroDbContext.cs`, migración |
| Script de puntuación por lotes | Implementado y verificado (idempotente) | `GymBro.Analytics/puntuar_abandono.py` |
| Encabezado de datos sintéticos en todo informe; sin "predice" ni "validado" | Implementado | `entrenar_abandono.py`, este informe |
| Reporte de una ejecución real | Implementado (base descartable con datos del generador) | `docs/pfc/analitica/` |

## Texto propuesto para 4.5 (implementación)

El modelo de abandono se implementó en un proyecto Python separado, `GymBro.Analytics`, que nunca usa las credenciales de la aplicación. El script `rol-analitica.sql` crea un esquema `analitica` con vistas que exponen solo socios con origen `Sintetico` y sin baja (la vista de socios no incluye nombre, correo ni DNI) y dos roles: uno de solo lectura, con permiso de consulta sobre esas vistas y sesiones en modo de solo lectura, y otro de puntuación, que además solo puede insertar en `prediccion_abandono`. Ninguno de los dos puede leer las tablas de la aplicación: las vistas se ejecutan con los permisos de su dueño. Así, ningún dato de personas reales llega a la analítica.

Para cada fecha de corte semanal t (un lunes) se considera la población de socios con al menos una asistencia en los 28 días anteriores y se calculan, solo con información anterior a t, la frecuencia semanal de asistencia de las últimas cuatro semanas, los días desde la última asistencia, la tendencia (asistencias de las dos últimas semanas menos las de las dos anteriores), las cuotas vencidas e impagas al día t, la antigüedad, la edad y el nivel. La etiqueta es 1 si el socio no registra asistencias en los 30 días desde t. Se usan solo los cortes cuya ventana de 30 días termina dentro del período observado. La separación entre entrenamiento y prueba es temporal: con una fecha de separación S, el entrenamiento usa los cortes cuya ventana de etiqueta termina antes de S y la prueba, los cortes posteriores a S; los cortes intermedios se descartan, de modo que ninguna etiqueta de entrenamiento observa el período de prueba.

Se ajustan una línea base que asigna la clase mayoritaria, una regresión logística sobre variables estandarizadas y un bosque aleatorio, ambos con pesos de clase balanceados. Sobre el conjunto de prueba se informan exactitud, precisión, sensibilidad, F1, área bajo la curva ROC y la matriz de confusión, con intervalos de 95 % obtenidos por remuestreo de socios (no de filas), porque las filas de un mismo socio en distintos cortes no son independientes. El bosque aleatorio se guarda con su versión y un script de puntuación por lotes calcula las variables a una fecha, sin mirar nada posterior, e inserta un puntaje por socio en `prediccion_abandono`; un índice único por socio, fecha y versión hace la operación idempotente.

## Decisiones técnicas

- **D-10b1-1. Vistas solo `Sintetico` como frontera de privacidad.** La restricción no depende del código Python: está en la base. Incluir datos reales requeriría otra revisión legal y ética (Ley 25.326), fuera del alcance.
- **D-10b1-2. Dos roles.** El pedido pide un rol de solo lectura para la extracción; la puntuación necesita escribir, así que tiene su propio rol con `INSERT` únicamente sobre `prediccion_abandono`.
- **D-10b1-3. Población activa y etiqueta a 30 días.** Un socio sin asistencias en las 4 semanas previas ya "abandonó" y no aporta información para anticipar el abandono; se excluye en ese corte.
- **D-10b1-4. Intervalos por conglomerado (socio)**, por la dependencia entre cortes del mismo socio.
- **D-10b1-5. Pesos balanceados y umbral 0,5** (la proporción de abandono es de alrededor del 10 %). El umbral no se ajustó con los datos de prueba.
- **D-10b1-6. Sin endpoint en la API.** La tabla tiene filtro global (personal del gimnasio y plataforma), pero ninguna pantalla de la aplicación la muestra; el Socio nunca ve su puntaje. Los tableros de la Etapa 12 la leen por la vista.
- **D-10b1-7. `unittest` y no `pytest`**, para no agregar dependencias.

## Texto propuesto para 4.6 (verificación)

Pruebas de integración (.NET, PostgreSQL descartable): `RolAnaliticaTests` (7) aplica el script real con un socio `Sintetico` y dos `Real` y verifica que las vistas devuelven solo lo sintético y no exponen nombre, correo ni DNI; que ninguno de los dos roles puede leer `socios`, `registros_bienestar_diario`, `prediccion_abandono` ni `usuarios`; que el rol de lectura no puede insertar y que el de puntuación inserta pero no puede actualizar; que sin completar las contraseñas el script falla y que es idempotente. `MigracionPrediccionAbandonoTests` verifica `Down` y `Up` y que la base rechaza un puntaje fuera de 0 a 1. El generador sintético borra también los puntajes de sus socios.

Pruebas de Python (`GymBro.Analytics/tests/test_abandono.py`, 10): extracción con una conexión simulada (tipos, consultas parametrizadas sobre las vistas, conversión de `timestamptz` al día local); variables con valores conocidos; **ausencia de fuga**: las variables a la fecha t no cambian si se agregan asistencias o pagos posteriores a t; etiqueta y población activa; cortes en lunes con ventana completa; **separación temporal**: la prueba es posterior y ninguna ventana de entrenamiento la alcanza; los modelos superan a la línea base en datos con señal conocida; métricas con valores conocidos; y, contra la base, que solo llegan datos sintéticos y que la sesión es de solo lectura.

Ejecución real (`etapa10b1-ejecucion.txt`): sobre una base descartable con los datos del generador (semilla 42, 150 socios, 26 semanas; 4464 sesiones, 4464 asistencias, 1050 cuotas, 74 abandonos en la simulación), 2129 filas (socio, corte) en 18 cortes (del 02/02/2026 al 01/06/2026); separación el 27/04/2026, con 1046 filas de entrenamiento, 623 de prueba y 460 descartadas por la brecha.

| Modelo (conjunto de prueba) | Accuracy | Precision | Recall | F1 | ROC-AUC |
|---|---|---|---|---|---|
| Línea base | 0,884 [0,837; 0,927] | 0 | 0 | 0 | 0,500 |
| Regresión logística | 0,896 [0,858; 0,927] | 0,534 [0,371; 0,684] | 0,764 [0,696; 0,831] | 0,629 [0,496; 0,734] | 0,891 [0,846; 0,933] |
| Bosque aleatorio | 0,937 [0,915; 0,958] | 0,770 [0,617; 0,889] | 0,653 [0,571; 0,729] | 0,707 [0,614; 0,774] | 0,853 [0,813; 0,895] |

Lectura: el generador hace depender el riesgo de abandono de la adherencia reciente y de la morosidad; los modelos recuperan esa señal (las variables de asistencia reciente concentran la importancia y el coeficiente de cuotas vencidas es positivo). Esto verifica el procedimiento de punta a punta; no informa nada sobre personas. La puntuación por lotes asignó un puntaje a los 83 socios activos al 05/07/2026; repetirla no agregó filas.

## Cifras de la verificación

Sobre el commit `8c8b2cd` (`etapa10b1-*.txt`):

| Verificación | Etapa anterior (9b-2) | Etapa 10b-1 |
|---|---|---|
| `dotnet build` | 0 errores, advertencia previa CS2008 | igual |
| Pruebas unitarias | 382 | 382 |
| Pruebas de integración | 575 | 583 (+8: roles de analítica y migración) |
| `ng build` / pruebas del cliente | correcto / 253 | correcto / 253 (sin cambios en el cliente) |
| Pruebas de Python de `GymBro.Analytics` | — | 10 sin base (1 omitida, la que necesita la base); con la base descartable, la ejecución recorrió también las pruebas de la Etapa 12 que ya estaban en el árbol sin commit: 25 correctas, ninguna omitida |
| SQL de la migración (sin conexión) | — | `etapa10b1-migracion.sql.txt`, códigos 0 y 0 |

## Desvíos y limitaciones

- La ejecución real se hizo contra una base descartable y no contra `gymbro_db`, por la regla de no migrar bases que no sean descartables; el catálogo del protocolo se sembró con la forma de `piernas-1.0` (como en las pruebas). Repetirlo en la base del autor: aplicar las migraciones, ejecutar el generador y luego el script de roles.
- La exactitud de la línea base (0,884) refleja el desbalance de clases; por eso se informan también F1 y ROC-AUC.
- El nombre de la tabla (`prediccion_abandono`) viene del pedido; los textos describen el resultado como "puntaje de riesgo" estimado por el modelo.
- El modelo entrenado no se versiona (`GymBro.Analytics/modelos/` está en `.gitignore`); se regenera con el script.

## Pendientes

- Aplicar la migración en `gymbro_db` y ejecutar `rol-analitica.sql` con contraseñas reales (lo hace el desarrollador).

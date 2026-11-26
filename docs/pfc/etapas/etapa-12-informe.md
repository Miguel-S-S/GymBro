# Etapa 12 — Aplicación de análisis y tableros (Streamlit)

> **Datos sintéticos (Sintetico): las métricas verifican el procedimiento y no el desempeño con personas.**

Rama `cierre-pfc`. Commit de código: `28192b5`. Evidencia: `docs/pfc/evidencia/etapa12-*.txt`. Sin migración.

## Alcance implementado

| Pedido | Estado | Dónde |
|---|---|---|
| `GymBro.Analytics/app/` con acceso por usuario y contraseña de configuración | Implementado y verificado | `app/principal.py` (`ANALITICA_APP_USUARIO`, `ANALITICA_APP_CLAVE`) |
| Sin SSO con el JWT (limitación declarada, DV-14) | Declarado | este informe |
| Lectura con el rol de solo lectura | Implementado y verificado | vistas `analitica.*` (Etapa 10b-1) |
| Selector de gimnasio y rótulo permanente del origen de los datos | Implementado y verificado | barra lateral y aviso en la cabecera |
| Descriptiva (carga, repeticiones, RIR, bienestar), faltantes y tamaño muestral | Implementado y verificado | `estadistica.descriptiva` |
| Correlación Pearson y Spearman con n, coeficiente, p, dispersión y leyenda fija | Implementado y verificado | `estadistica.correlacion` |
| ANOVA de ingesta contra un resultado, agregando por socio, con Shapiro-Wilk, Levene, Kruskal-Wallis y tamaño del efecto | Implementado y verificado | `estadistica.agregar_por_socio_modal`, `comparar_grupos` |
| Retención por cohorte, morosidad, horas pico (día por hora), asistencia frente a antigüedad, riesgo de abandono | Implementado y verificado | `estadistica.retencion_por_cohorte`, `morosidad_por_periodo`, `horas_pico`, `asistencia_vs_antiguedad`; puntajes de `analitica.v_prediccion_abandono` |
| Pregunta que responde y supuestos en cada técnica; sin lenguaje causal ni predictivo | Implementado | `pregunta(...)` en cada pestaña |
| Pruebas de las funciones con datos de resultado conocido | Implementado y verificado | `tests/test_estadistica.py` |
| La aplicación arranca con el generador sintético | Implementado y verificado | `tests/test_app.py`, `etapa12-aplicacion.txt` |

## Texto propuesto para 4.5 (implementación)

La aplicación de análisis es una aplicación Streamlit que lee exclusivamente las vistas del esquema `analitica` con el rol de solo lectura, de modo que solo trabaja con socios de origen `Sintetico`. El acceso se controla con un usuario y una contraseña de configuración, comparados en tiempo constante; si no están configurados, la aplicación no muestra ningún dato. Un selector permite elegir un gimnasio sintético o todos, y un aviso permanente indica el origen de los datos y que las cifras verifican el procedimiento.

Se organiza en cuatro pestañas. La descriptiva informa, para la carga, las repeticiones, el RIR y su desvío respecto del objetivo, y para las variables de bienestar, el tamaño muestral, los faltantes y los estadísticos de posición y dispersión, con un histograma. La de correlación relaciona las variables de bienestar del día con el desvío medio del RIR de ese día mediante Pearson y Spearman, con n, coeficiente, valor p y diagrama de dispersión, y con la leyenda fija "correlación no implica causalidad"; ofrece además la versión por socio (medias), porque los días de un mismo socio no son independientes. La de ANOVA compara el desvío medio del RIR entre socios según su categoría de ingesta más frecuente: se agrega a una observación por socio para evitar la pseudorreplicación, se verifican normalidad por grupo (Shapiro-Wilk) y homogeneidad de varianzas (Levene) y, si algún supuesto no se cumple, se indica que corresponde Kruskal-Wallis; se informan eta cuadrado y epsilon cuadrado. La pestaña de gestión muestra la retención por cohorte de primera asistencia, la morosidad por período (derivada, como en la Etapa 11), un mapa de calor de asistencias por día y hora local, la asistencia semanal frente a la antigüedad y la distribución de los puntajes de riesgo de abandono de la Etapa 10b-1. Cada técnica declara la pregunta que responde y sus supuestos.

## Decisiones técnicas

- **D-12-1. Una observación por socio en el ANOVA** (categoría más frecuente y desvío medio): cumple la independencia que exige la prueba, a costa de comparar entre socios y no dentro de cada socio. Como el generador sortea la ingesta cada día y no por persona, el efecto entre socios queda diluido; el análisis dentro de cada socio (medidas repetidas o modelo mixto) queda como trabajo futuro.
- **D-12-2. Correlación por día y por socio**: la versión por día tiene más potencia pero un valor p optimista; la versión por socio es la conservadora. Ambas están disponibles.
- **D-12-3. Gráficos con Altair**, que ya incluye Streamlit (sin dependencias nuevas).
- **D-12-4. Sin datos personales identificatorios**: los socios se muestran por los primeros caracteres de su id sintético.

## Texto propuesto para 4.6 (verificación)

Pruebas con resultado conocido (`tests/test_estadistica.py`, 12): descriptiva con faltantes; Pearson igual a 1 en una relación lineal perfecta; Spearman igual a 1 y Pearson menor en una monótona no lineal; r = 0,8 en un conjunto de referencia; ANOVA de libro (tres grupos de 3 con medias 2, 5 y 8: F = 27, p = 0,001, eta cuadrado = 0,9, H de Kruskal-Wallis = 7,2); varianzas muy distintas que llevan a recomendar Kruskal-Wallis; agregación por socio con categoría modal y desempate; retención por cohorte, morosidad derivada, horas pico en hora local y asistencia frente a antigüedad con valores calculados a mano. Pruebas de arranque (`tests/test_app.py`, 3): sin usuario configurado no muestra datos; con usuario configurado rechaza una contraseña incorrecta; con la base de analítica, la aplicación autenticada carga las cuatro pestañas sin excepciones, con el rótulo de origen y la leyenda de causalidad.

Ejecución con los datos del generador (`etapa12-aplicacion.txt`; semilla 42, 150 socios, 26 semanas): la aplicación arrancó con `streamlit run` y respondió a la comprobación de salud; las funciones de la aplicación sobre esos datos dieron, entre otros: 70 916 series efectivas y 12 060 registros de bienestar sin faltantes; correlación entre la fatiga del día y el desvío medio del RIR r = 0,564 (n = 2727 días) y r = 0,615 por socio (n = 146), mientras que sueño, estrés y FC de reposo quedan cerca de 0, que es lo que corresponde a los parámetros del generador (solo la fatiga y la ingesta afectan el RIR); en el ANOVA por socio, Levene dio p = 0,024, por lo que corresponde Kruskal-Wallis (H = 5,50, p = 0,064; epsilon cuadrado = 0,024), un resultado no significativo coherente con la dilución descrita en D-12-1. La retención de la cohorte cae del 85 % al 44 % entre el primer y el sexto mes y la morosidad crece con los abandonos de la simulación.

## Cifras de la verificación

Sobre el commit `28192b5` (`etapa12-*.txt`): `dotnet build` 0 errores (advertencia previa CS2008); 382 unitarias y 583 de integración (sin cambios en .NET); `ng build` correcto y 253 pruebas del cliente (sin cambios en el cliente); Python de `GymBro.Analytics`: 25 pruebas, 23 correctas y 2 omitidas sin base, y 25 correctas con la base descartable. La aplicación arrancó con `streamlit run` (salud 200) y, autenticada, cargó las cuatro pestañas sin excepciones con 7 tablas, el rótulo de origen y la leyenda de causalidad (`etapa12-aplicacion.txt`).

## Desvíos y limitaciones

- **DV-14.** Sin inicio de sesión único con el JWT de la API: el acceso es un usuario y una contraseña de configuración, sin roles; la aplicación está pensada para los autores y sobre datos sintéticos. En Azure se reemplazaría por la autenticación integrada del servicio (ver `docs/despliegue.md`).
- Solo datos `Sintetico` por diseño (vistas de la 10b-1).
- El análisis dentro de cada socio queda como trabajo futuro (D-12-1).
- La ejecución se hizo contra la base descartable, no contra `gymbro_db`.

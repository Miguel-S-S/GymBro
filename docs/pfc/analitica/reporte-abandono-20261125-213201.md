# Modelo de abandono (Etapa 10b-1) — reporte de una ejecución

> **Datos sintéticos (Sintetico): las métricas verifican el procedimiento y no el desempeño con personas.**

- Fecha de ejecución: 2026-11-25 21:32
- Versión del modelo guardado: `abandono-bosque-v1-20261125-f7ee4d45c307` (bosque aleatorio; `GymBro.Analytics/modelos/`, no versionado)
- Origen de los datos: Sintetico (vistas `analitica.*`, rol de solo lectura)
- Gimnasio: todos los sintéticos; socios: 150; asistencias: 4464; cuotas: 1050
- Fechas de corte (lunes): 18, de 2026-02-02 a 2026-06-01; filas (socio, corte) de la población activa: 2129

## Definiciones

- **Población en cada corte t:** socios con al menos una asistencia en los 28 días anteriores a t.
- **Etiqueta (abandono = 1):** sin asistencias en los 30 días desde t. Solo se usan cortes cuya ventana de 30 días termina dentro del período observado.
- **Variables (solo información anterior a t):** frecuencia_4s, dias_desde_ultima, tendencia, cuotas_vencidas, antiguedad_dias, edad, nivel.
- **Separación temporal:** fecha de separación 2026-04-27; entrenamiento = cortes con t + 30 días ≤ separación (1046 filas, 2026-02-02 a 2026-03-23); prueba = cortes ≥ separación (623 filas, 2026-04-27 a 2026-06-01); descartadas por la brecha: 460.
- **Proporción de abandono:** entrenamiento 0.093; prueba 0.116.
- **Intervalos:** 95 % por remuestreo de socios (1000 repeticiones, semilla 42). Umbral de clasificación 0,5.

## Resultados en el conjunto de prueba

| Modelo | Accuracy | Precision | Recall | F1 | ROC-AUC | Matriz (VN / FP / FN / VP) |
|---|---|---|---|---|---|---|
| Línea base (clase mayoritaria) | 0.884 [0.837; 0.927] | 0.000 [0.000; 0.000] | 0.000 [0.000; 0.000] | 0.000 [0.000; 0.000] | 0.500 [0.500; 0.500] | 551 / 0 / 72 / 0 |
| Regresión logística | 0.896 [0.858; 0.927] | 0.534 [0.371; 0.684] | 0.764 [0.696; 0.831] | 0.629 [0.496; 0.734] | 0.891 [0.846; 0.933] | 503 / 48 / 17 / 55 |
| Bosque aleatorio | 0.937 [0.915; 0.958] | 0.770 [0.617; 0.889] | 0.653 [0.571; 0.729] | 0.707 [0.614; 0.774] | 0.853 [0.813; 0.895] | 537 / 14 / 25 / 47 |

La línea base asigna a todos la clase mayoritaria; su ROC-AUC es 0,5 por construcción (puntaje constante).

## Descripción de los modelos ajustados

Importancia de variables del bosque aleatorio (reducción media de impureza; describe el modelo, no causas):

- dias_desde_ultima: 0.363
- frecuencia_4s: 0.255
- tendencia: 0.121
- antiguedad_dias: 0.112
- edad: 0.091
- cuotas_vencidas: 0.035
- nivel: 0.024

Coeficientes de la regresión logística sobre variables estandarizadas (signo = dirección de la asociación en estos datos):

- frecuencia_4s: -0.911
- dias_desde_ultima: +0.805
- tendencia: -0.164
- cuotas_vencidas: +0.252
- antiguedad_dias: +0.279
- edad: +0.107
- nivel: -0.031

## Lectura

Los datos los produjo el generador de la Etapa 7e con una regla de abandono conocida (riesgo semanal logístico según la adherencia de las últimas 4 semanas y la morosidad; ver `docs/pfc/sintetico-parametros.md`). Estas cifras muestran que el procedimiento (extracción, variables sin información futura, separación temporal, modelos, métricas e intervalos) funciona de punta a punta y recupera señales que el generador puso a propósito. No dicen nada sobre el comportamiento de personas reales.

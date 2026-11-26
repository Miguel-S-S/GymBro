"""Etapa 10b-1: entrena y evalua el modelo de abandono con datos de origen Sintetico y genera el reporte.

Uso (con el rol de solo lectura; ver docs/pfc/scripts/rol-analitica.sql y gymbro_analytics/conexion.py):
  python GymBro.Analytics/entrenar_abandono.py [--gimnasio ID] [--semilla 42] [--repeticiones 1000] [--salida CARPETA]

Salidas: reporte Markdown con el encabezado de datos sinteticos y el modelo elegido (bosque aleatorio) serializado con
joblib en GymBro.Analytics/modelos/ (no se versiona). El reporte no usa lenguaje predictivo ni de validacion.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import sys
from pathlib import Path

import joblib
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gymbro_analytics import ENCABEZADO_SINTETICO, extraccion  # noqa: E402
from gymbro_analytics.conexion import conectar  # noqa: E402
from gymbro_analytics.modelos import entrenar_y_evaluar, separar_temporal  # noqa: E402
from gymbro_analytics.variables import VARIABLES, construir, fechas_de_corte  # noqa: E402

RAIZ = Path(__file__).resolve().parents[1]
NOMBRES = {
    "linea_base_clase_mayoritaria": "Línea base (clase mayoritaria)",
    "regresion_logistica": "Regresión logística",
    "bosque_aleatorio": "Bosque aleatorio",
}


def huella(df) -> str:
    return hashlib.sha256(df.sort_values(["socio_id", "fecha_corte"]).to_csv(index=False).encode()).hexdigest()[:12]


def formatear(nombre: str, r: dict) -> list[str]:
    m, ic = r["metricas"], r["intervalos"]
    def f(k):
        lo, hi = ic[k]
        return f"{m[k]:.3f} [{lo:.3f}; {hi:.3f}]"
    return [f"| {NOMBRES[nombre]} | {f('accuracy')} | {f('precision')} | {f('recall')} | {f('f1')} | {f('roc_auc')} |"
            f" {m['tn']} / {m['fp']} / {m['fn']} / {m['tp']} |"]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--gimnasio", default=None)
    ap.add_argument("--semilla", type=int, default=42)
    ap.add_argument("--repeticiones", type=int, default=1000)
    ap.add_argument("--salida", type=Path, default=RAIZ / "docs" / "pfc" / "analitica")
    args = ap.parse_args()

    with conectar(solo_lectura=True) as con:
        socios = extraccion.socios(con, args.gimnasio)
        asistencias = extraccion.asistencias(con, args.gimnasio)
        cuotas = extraccion.cuotas(con, args.gimnasio)
    origenes = sorted(socios["origen_datos"].unique().tolist())
    if origenes != ["Sintetico"]:
        sys.exit(f"Se esperaban solo datos Sintetico y llegaron: {origenes}. Revise las vistas del esquema analitica.")

    cortes = fechas_de_corte(asistencias)
    datos = construir(socios, asistencias, cuotas, cortes)
    sep = separar_temporal(datos)
    salida = entrenar_y_evaluar(sep, args.semilla, args.repeticiones)
    version = f"abandono-bosque-v1-{dt.date.today():%Y%m%d}-{huella(datos)}"

    carpeta_modelos = RAIZ / "GymBro.Analytics" / "modelos"
    carpeta_modelos.mkdir(parents=True, exist_ok=True)
    joblib.dump({"modelo": salida["modelos"]["bosque_aleatorio"], "variables": VARIABLES, "version": version},
                carpeta_modelos / f"{version}.joblib")

    tren, prueba = sep.entrenamiento, sep.prueba
    bosque = salida["modelos"]["bosque_aleatorio"]
    importancias = sorted(zip(VARIABLES, bosque.feature_importances_), key=lambda x: -x[1])
    coef = salida["modelos"]["regresion_logistica"][-1].coef_[0]
    lineas = [
        f"# Modelo de abandono (Etapa 10b-1) — reporte de una ejecución",
        "",
        f"> **{ENCABEZADO_SINTETICO}**",
        "",
        f"- Fecha de ejecución: {dt.datetime.now():%Y-%m-%d %H:%M}",
        f"- Versión del modelo guardado: `{version}` (bosque aleatorio; `GymBro.Analytics/modelos/`, no versionado)",
        f"- Origen de los datos: {', '.join(origenes)} (vistas `analitica.*`, rol de solo lectura)",
        f"- Gimnasio: {args.gimnasio or 'todos los sintéticos'}; socios: {socios['socio_id'].nunique()}; asistencias: {len(asistencias)}; cuotas: {len(cuotas)}",
        f"- Fechas de corte (lunes): {len(cortes)}, de {min(cortes):%Y-%m-%d} a {max(cortes):%Y-%m-%d}; filas (socio, corte) de la población activa: {len(datos)}",
        "",
        "## Definiciones",
        "",
        "- **Población en cada corte t:** socios con al menos una asistencia en los 28 días anteriores a t.",
        "- **Etiqueta (abandono = 1):** sin asistencias en los 30 días desde t. Solo se usan cortes cuya ventana de 30 días termina dentro del período observado.",
        f"- **Variables (solo información anterior a t):** {', '.join(VARIABLES)}.",
        f"- **Separación temporal:** fecha de separación {sep.fecha_separacion:%Y-%m-%d}; entrenamiento = cortes con t + 30 días ≤ separación "
        f"({len(tren)} filas, {tren['fecha_corte'].min():%Y-%m-%d} a {tren['fecha_corte'].max():%Y-%m-%d}); prueba = cortes ≥ separación "
        f"({len(prueba)} filas, {prueba['fecha_corte'].min():%Y-%m-%d} a {prueba['fecha_corte'].max():%Y-%m-%d}); descartadas por la brecha: {sep.descartadas}.",
        f"- **Proporción de abandono:** entrenamiento {tren['abandono'].mean():.3f}; prueba {prueba['abandono'].mean():.3f}.",
        f"- **Intervalos:** 95 % por remuestreo de socios ({args.repeticiones} repeticiones, semilla {args.semilla}). Umbral de clasificación 0,5.",
        "",
        "## Resultados en el conjunto de prueba",
        "",
        "| Modelo | Accuracy | Precision | Recall | F1 | ROC-AUC | Matriz (VN / FP / FN / VP) |",
        "|---|---|---|---|---|---|---|",
    ]
    for nombre, r in salida["resultados"].items():
        lineas += formatear(nombre, r)
    lineas += [
        "",
        "La línea base asigna a todos la clase mayoritaria; su ROC-AUC es 0,5 por construcción (puntaje constante).",
        "",
        "## Descripción de los modelos ajustados",
        "",
        "Importancia de variables del bosque aleatorio (reducción media de impureza; describe el modelo, no causas):",
        "",
    ] + [f"- {v}: {i:.3f}" for v, i in importancias] + [
        "",
        "Coeficientes de la regresión logística sobre variables estandarizadas (signo = dirección de la asociación en estos datos):",
        "",
    ] + [f"- {v}: {c:+.3f}" for v, c in zip(VARIABLES, coef)] + [
        "",
        "## Lectura",
        "",
        "Los datos los produjo el generador de la Etapa 7e con una regla de abandono conocida (riesgo semanal logístico según la "
        "adherencia de las últimas 4 semanas y la morosidad; ver `docs/pfc/sintetico-parametros.md`). Estas cifras muestran que el "
        "procedimiento (extracción, variables sin información futura, separación temporal, modelos, métricas e intervalos) funciona "
        "de punta a punta y recupera señales que el generador puso a propósito. No dicen nada sobre el comportamiento de personas reales.",
        "",
    ]
    args.salida.mkdir(parents=True, exist_ok=True)
    destino = args.salida / f"reporte-abandono-{dt.datetime.now():%Y%m%d-%H%M%S}.md"
    destino.write_text("\n".join(lineas), encoding="utf-8")
    (args.salida / f"{destino.stem}.json").write_text(json.dumps(
        {"version": version, "fecha_separacion": str(sep.fecha_separacion.date()),
         "resultados": {k: {"metricas": v["metricas"], "intervalos": v["intervalos"]} for k, v in salida["resultados"].items()}},
        ensure_ascii=False, indent=2, default=lambda o: float(o) if isinstance(o, (np.floating,)) else str(o)), encoding="utf-8")
    print("\n".join(lineas))
    print(f"Reporte: {destino}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

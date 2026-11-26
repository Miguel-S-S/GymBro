"""Etapa 10b-1: separacion temporal, modelos (linea base, regresion logistica, bosque aleatorio) y metricas con intervalo.

Separacion temporal sin fuga: con una fecha de separacion S (un corte), el entrenamiento usa los cortes t con
t + 30 dias <= S (su ventana de etiqueta termina antes de S) y la prueba los cortes t >= S. Los cortes intermedios se
descartan (brecha). Asi, ninguna etiqueta de entrenamiento observa el periodo de prueba.
Los intervalos de confianza (95 %) se obtienen por remuestreo de SOCIOS (bootstrap por conglomerado), porque las filas de
un mismo socio en distintos cortes no son independientes.
"""
from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import pandas as pd
from sklearn.dummy import DummyClassifier
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score, confusion_matrix, f1_score, precision_score, recall_score, roc_auc_score
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

from .variables import VARIABLES, VENTANA_ETIQUETA_DIAS


@dataclass
class Separacion:
    fecha_separacion: pd.Timestamp
    entrenamiento: pd.DataFrame
    prueba: pd.DataFrame
    descartadas: int


def separar_temporal(df: pd.DataFrame, proporcion: float = 0.7) -> Separacion:
    cortes = sorted(df["fecha_corte"].unique())
    if len(cortes) < 3:
        raise ValueError("Hacen falta al menos 3 fechas de corte para separar en el tiempo.")
    s = pd.Timestamp(cortes[min(len(cortes) - 1, max(1, int(len(cortes) * proporcion)))])
    tren = df[df["fecha_corte"] + pd.Timedelta(days=VENTANA_ETIQUETA_DIAS) <= s]
    prueba = df[df["fecha_corte"] >= s]
    if tren.empty or prueba.empty:
        raise ValueError("La separacion temporal dejo un conjunto vacio; genere mas semanas de datos.")
    return Separacion(s, tren, prueba, len(df) - len(tren) - len(prueba))


def modelos(semilla: int) -> dict:
    return {
        "linea_base_clase_mayoritaria": DummyClassifier(strategy="most_frequent"),
        "regresion_logistica": make_pipeline(StandardScaler(), LogisticRegression(max_iter=1000, class_weight="balanced")),
        "bosque_aleatorio": RandomForestClassifier(n_estimators=300, min_samples_leaf=5, class_weight="balanced_subsample",
                                                   random_state=semilla, n_jobs=1),
    }


def metricas(y: np.ndarray, prob: np.ndarray, umbral: float = 0.5) -> dict:
    pred = (prob >= umbral).astype(int)
    tn, fp, fn, tp = confusion_matrix(y, pred, labels=[0, 1]).ravel()
    return {
        "accuracy": accuracy_score(y, pred),
        "precision": precision_score(y, pred, zero_division=0),
        "recall": recall_score(y, pred, zero_division=0),
        "f1": f1_score(y, pred, zero_division=0),
        "roc_auc": roc_auc_score(y, prob) if len(np.unique(y)) == 2 else float("nan"),
        "tn": int(tn), "fp": int(fp), "fn": int(fn), "tp": int(tp),
    }


def intervalos(prueba: pd.DataFrame, prob: np.ndarray, semilla: int, repeticiones: int = 1000, umbral: float = 0.5) -> dict:
    """Percentiles 2,5 y 97,5 de cada metrica remuestreando socios con reemplazo."""
    rng = np.random.default_rng(semilla)
    datos = prueba[["socio_id", "abandono"]].copy()
    datos["prob"] = prob
    grupos = {k: g for k, g in datos.groupby("socio_id")}
    ids = np.array(list(grupos))
    acumulado: dict[str, list[float]] = {k: [] for k in ["accuracy", "precision", "recall", "f1", "roc_auc"]}
    for _ in range(repeticiones):
        muestra = pd.concat([grupos[i] for i in rng.choice(ids, size=len(ids), replace=True)])
        m = metricas(muestra["abandono"].to_numpy(), muestra["prob"].to_numpy(), umbral)
        for k in acumulado:
            if not np.isnan(m[k]):
                acumulado[k].append(m[k])
    return {k: (float(np.percentile(v, 2.5)), float(np.percentile(v, 97.5))) if v else (float("nan"), float("nan"))
            for k, v in acumulado.items()}


def entrenar_y_evaluar(sep: Separacion, semilla: int, repeticiones: int = 1000) -> dict:
    X_tr, y_tr = sep.entrenamiento[VARIABLES].to_numpy(dtype=float), sep.entrenamiento["abandono"].to_numpy()
    X_te, y_te = sep.prueba[VARIABLES].to_numpy(dtype=float), sep.prueba["abandono"].to_numpy()
    resultados = {}
    ajustados = {}
    for nombre, modelo in modelos(semilla).items():
        modelo.fit(X_tr, y_tr)
        prob = modelo.predict_proba(X_te)
        prob = prob[:, 1] if prob.shape[1] == 2 else np.zeros(len(X_te))
        resultados[nombre] = {"metricas": metricas(y_te, prob), "intervalos": intervalos(sep.prueba, prob, semilla, repeticiones)}
        ajustados[nombre] = modelo
    return {"resultados": resultados, "modelos": ajustados}

"""Etapa 9b-1: metricas de recuperacion (recall@k y MRR), funciones puras sin dependencias externas.

Definiciones (por consulta con al menos un id relevante etiquetado):
  - recall@k = |relevantes entre los primeros k devueltos| / |relevantes|
  - rango reciproco = 1 / posicion (desde 1) del primer relevante devuelto; 0 si no aparece ninguno
Los promedios se calculan solo sobre consultas etiquetadas. Una consulta sin etiquetas no se evalua: no se inventan.
"""
from __future__ import annotations

from typing import Iterable, Sequence


def recall_en_k(devueltos: Sequence[str], relevantes: Iterable[str], k: int) -> float:
    rel = {r.strip().lower() for r in relevantes if r and r.strip()}
    if not rel:
        raise ValueError("La consulta no tiene ids relevantes etiquetados.")
    if k < 1:
        raise ValueError("k debe ser al menos 1.")
    primeros = [d.strip().lower() for d in devueltos[:k]]
    return len(rel.intersection(primeros)) / len(rel)


def rango_reciproco(devueltos: Sequence[str], relevantes: Iterable[str]) -> float:
    rel = {r.strip().lower() for r in relevantes if r and r.strip()}
    if not rel:
        raise ValueError("La consulta no tiene ids relevantes etiquetados.")
    for posicion, d in enumerate(devueltos, start=1):
        if d.strip().lower() in rel:
            return 1.0 / posicion
    return 0.0


def resumir(resultados: Sequence[tuple[Sequence[str], Sequence[str]]], ks: Sequence[int] = (1, 3, 5)) -> dict:
    """resultados: lista de (devueltos, relevantes) de consultas etiquetadas. Devuelve n, recall@k medio y MRR."""
    if not resultados:
        raise ValueError("No hay consultas etiquetadas: no se calculan metricas.")
    n = len(resultados)
    salida = {"n": n}
    for k in ks:
        salida[f"recall@{k}"] = sum(recall_en_k(d, r, k) for d, r in resultados) / n
    salida["mrr"] = sum(rango_reciproco(d, r) for d, r in resultados) / n
    return salida


def separar_ids(celda: str | None) -> list[str]:
    """La columna ids_relevantes separa los ids con ';' (vacia = sin etiquetar)."""
    if celda is None:
        return []
    return [p.strip() for p in celda.replace(",", ";").split(";") if p.strip()]

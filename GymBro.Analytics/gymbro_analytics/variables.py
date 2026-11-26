"""Etapa 10b-1: variables y etiqueta del modelo de abandono, por socio y fecha de corte.

Fecha de corte t (un lunes). Poblacion: socios con al menos una asistencia en los 28 dias anteriores a t (socios activos).
Variables (todas con informacion ESTRICTAMENTE anterior a t; nada posterior):
  frecuencia_4s           asistencias en [t-28, t) / 4 (por semana)
  dias_desde_ultima       dias entre la ultima asistencia anterior a t y t
  tendencia               asistencias en [t-14, t) menos asistencias en [t-28, t-14)
  cuotas_vencidas         cuotas con vencimiento < t que al dia t no estaban pagadas (pago nulo o pago >= t)
  antiguedad_dias         t - fecha de alta
  edad                    anios cumplidos a t
  nivel                   1 Principiante, 2 Intermedio, 3 Avanzado
Etiqueta (declarada): abandono = 1 si el socio NO registra asistencias en [t, t + 30 dias); 0 si registra al menos una.
Solo se generan cortes cuya ventana de 30 dias termina dentro del periodo observado (sin etiquetas incompletas).
"""
from __future__ import annotations

import pandas as pd

VENTANA_ETIQUETA_DIAS = 30
VENTANA_ACTIVIDAD_DIAS = 28
VARIABLES = ["frecuencia_4s", "dias_desde_ultima", "tendencia", "cuotas_vencidas", "antiguedad_dias", "edad", "nivel"]


def fechas_de_corte(asistencias: pd.DataFrame, paso_dias: int = 7) -> list[pd.Timestamp]:
    """Lunes desde (primera asistencia + 28 dias) hasta (ultima asistencia observada - 30 dias)."""
    if asistencias.empty:
        return []
    inicio = asistencias["fecha"].min() + pd.Timedelta(days=VENTANA_ACTIVIDAD_DIAS)
    inicio = inicio + pd.Timedelta(days=(7 - inicio.weekday()) % 7)
    fin = asistencias["fecha"].max() - pd.Timedelta(days=VENTANA_ETIQUETA_DIAS)
    cortes = []
    t = inicio
    while t <= fin:
        cortes.append(t)
        t = t + pd.Timedelta(days=paso_dias)
    return cortes


def construir(socios: pd.DataFrame, asistencias: pd.DataFrame, cuotas: pd.DataFrame, cortes: list[pd.Timestamp],
              con_etiqueta: bool = True) -> pd.DataFrame:
    """Una fila por (socio, corte) de la poblacion activa. Con con_etiqueta=False no mira nada posterior a t."""
    filas = []
    por_socio = {k: g["fecha"].sort_values().to_numpy() for k, g in asistencias.groupby("socio_id")}
    cuotas_por_socio = {k: g for k, g in cuotas.groupby("socio_id")}
    info = socios.set_index("socio_id")
    for t in cortes:
        t64 = pd.Timestamp(t).to_datetime64()
        for socio_id, fechas in por_socio.items():
            if socio_id not in info.index:
                continue
            previas = fechas[fechas < t64]
            recientes = previas[previas >= (t - pd.Timedelta(days=VENTANA_ACTIVIDAD_DIAS)).to_datetime64()]
            if len(recientes) == 0:
                continue  # fuera de la poblacion activa en t
            mitad = (t - pd.Timedelta(days=14)).to_datetime64()
            s = info.loc[socio_id]
            q = cuotas_por_socio.get(socio_id)
            vencidas = 0
            if q is not None:
                venc = q[q["vencimiento"] < t]
                vencidas = int(((venc["fecha_pago"].isna()) | (venc["fecha_pago"] >= t)).sum())
            fila = {
                "socio_id": socio_id,
                "gimnasio_id": s["gimnasio_id"],
                "fecha_corte": t,
                "frecuencia_4s": len(recientes) / 4.0,
                "dias_desde_ultima": int((t64 - previas.max()) / pd.Timedelta(days=1)),
                "tendencia": int((recientes >= mitad).sum() - (recientes < mitad).sum()),
                "cuotas_vencidas": vencidas,
                "antiguedad_dias": int((t - s["fecha_alta"]).days),
                "edad": int((t - s["fecha_nacimiento"]).days // 365.25),
                "nivel": int(s["nivel_ordinal"]) if pd.notna(s["nivel_ordinal"]) else 0,
            }
            if con_etiqueta:
                fin = (t + pd.Timedelta(days=VENTANA_ETIQUETA_DIAS)).to_datetime64()
                futuras = fechas[(fechas >= t64) & (fechas < fin)]
                fila["abandono"] = int(len(futuras) == 0)
            filas.append(fila)
    return pd.DataFrame(filas)

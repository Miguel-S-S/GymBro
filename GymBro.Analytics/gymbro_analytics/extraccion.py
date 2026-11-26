"""Extraccion desde las vistas del esquema "analitica" (rol de solo lectura). Las vistas ya filtran origen Sintetico.

Cada funcion recibe una conexion DB-API (psycopg2 en uso real; un doble en las pruebas) y devuelve un DataFrame con
tipos normalizados. No hay SQL dinamico: los filtros van como parametros.
"""
from __future__ import annotations

import pandas as pd

SQL_SOCIOS = """
SELECT socio_id, gimnasio_id, fecha_nacimiento, nivel, fecha_alta, origen_datos
FROM analitica.v_socios
WHERE (%(gimnasio)s IS NULL OR gimnasio_id = %(gimnasio)s)
"""

SQL_ASISTENCIAS = """
SELECT socio_id, gimnasio_id, fecha, instante
FROM analitica.v_asistencias
WHERE (%(gimnasio)s IS NULL OR gimnasio_id = %(gimnasio)s)
"""

SQL_CUOTAS = """
SELECT socio_id, gimnasio_id, anio, mes, monto, vencimiento, fecha_pago, estado
FROM analitica.v_cuotas
WHERE (%(gimnasio)s IS NULL OR gimnasio_id = %(gimnasio)s)
"""

SQL_GIMNASIOS = "SELECT gimnasio_id, nombre FROM analitica.v_gimnasios ORDER BY nombre"

SQL_BIENESTAR = """
SELECT socio_id, gimnasio_id, fecha, horas_sueno, estres, fatiga, peso_kg, nutricion_categoria, ingesta_agua_litros,
       frecuencia_cardiaca_reposo
FROM analitica.v_bienestar
WHERE (%(gimnasio)s IS NULL OR gimnasio_id = %(gimnasio)s)
"""

SQL_SERIES = """
SELECT socio_id, gimnasio_id, sesion_id, fecha, ejercicio_id, orden, orden_serie, peso_kg, repeticiones, rir,
       es_calentamiento, rir_objetivo, repeticiones_minimas, repeticiones_maximas
FROM analitica.v_series
WHERE (%(gimnasio)s IS NULL OR gimnasio_id = %(gimnasio)s)
"""

SQL_PREDICCIONES = """
SELECT socio_id, gimnasio_id, fecha, puntaje, version_modelo
FROM analitica.v_prediccion_abandono
WHERE (%(gimnasio)s IS NULL OR gimnasio_id = %(gimnasio)s)
"""

NIVELES = {"Principiante": 1, "Intermedio": 2, "Avanzado": 3}


def _consultar(con, sql: str, parametros: dict | None = None) -> pd.DataFrame:
    cur = con.cursor()
    try:
        cur.execute(sql, parametros or {})
        columnas = [d[0] for d in cur.description]
        return pd.DataFrame(cur.fetchall(), columns=columnas)
    finally:
        cur.close()


ZONA = "America/Argentina/Buenos_Aires"


def _fecha(serie: pd.Series) -> pd.Series:
    """Columnas de tipo date (sin zona): fecha calendario tal cual."""
    return pd.to_datetime(serie).dt.normalize()


def _dia_local(serie: pd.Series) -> pd.Series:
    """Columnas timestamptz (por ejemplo fecha_alta): dia local de Buenos Aires, sin zona, como DiaLocal en la API."""
    return pd.to_datetime(serie, utc=True).dt.tz_convert(ZONA).dt.tz_localize(None).dt.normalize()


def socios(con, gimnasio: str | None = None) -> pd.DataFrame:
    df = _consultar(con, SQL_SOCIOS, {"gimnasio": gimnasio})
    df["socio_id"] = df["socio_id"].astype(str)
    df["gimnasio_id"] = df["gimnasio_id"].astype(str)
    df["fecha_nacimiento"] = _fecha(df["fecha_nacimiento"])
    df["fecha_alta"] = _dia_local(df["fecha_alta"])
    df["nivel_ordinal"] = df["nivel"].map(NIVELES).astype("Int64")
    return df


def asistencias(con, gimnasio: str | None = None) -> pd.DataFrame:
    df = _consultar(con, SQL_ASISTENCIAS, {"gimnasio": gimnasio})
    df["socio_id"] = df["socio_id"].astype(str)
    df["gimnasio_id"] = df["gimnasio_id"].astype(str)
    df["fecha"] = _fecha(df["fecha"])
    df["instante"] = pd.to_datetime(df["instante"], utc=True)
    return df


def cuotas(con, gimnasio: str | None = None) -> pd.DataFrame:
    df = _consultar(con, SQL_CUOTAS, {"gimnasio": gimnasio})
    df["socio_id"] = df["socio_id"].astype(str)
    df["gimnasio_id"] = df["gimnasio_id"].astype(str)
    df["vencimiento"] = _fecha(df["vencimiento"])
    df["fecha_pago"] = pd.to_datetime(df["fecha_pago"]).dt.normalize()
    df["monto"] = df["monto"].astype(float)
    return df


def gimnasios(con) -> pd.DataFrame:
    df = _consultar(con, SQL_GIMNASIOS)
    df["gimnasio_id"] = df["gimnasio_id"].astype(str)
    return df


def bienestar(con, gimnasio: str | None = None) -> pd.DataFrame:
    df = _consultar(con, SQL_BIENESTAR, {"gimnasio": gimnasio})
    df["socio_id"] = df["socio_id"].astype(str)
    df["fecha"] = _fecha(df["fecha"])
    for c in ["horas_sueno", "estres", "fatiga", "peso_kg", "ingesta_agua_litros", "frecuencia_cardiaca_reposo"]:
        df[c] = pd.to_numeric(df[c], errors="coerce").astype(float)
    return df


def series(con, gimnasio: str | None = None) -> pd.DataFrame:
    df = _consultar(con, SQL_SERIES, {"gimnasio": gimnasio})
    df["socio_id"] = df["socio_id"].astype(str)
    df["fecha"] = _fecha(df["fecha"])
    for c in ["peso_kg", "repeticiones", "rir", "rir_objetivo", "repeticiones_minimas", "repeticiones_maximas"]:
        df[c] = pd.to_numeric(df[c], errors="coerce").astype(float)
    df["desvio_rir"] = df["rir"] - df["rir_objetivo"]
    return df


def predicciones(con, gimnasio: str | None = None) -> pd.DataFrame:
    df = _consultar(con, SQL_PREDICCIONES, {"gimnasio": gimnasio})
    df["socio_id"] = df["socio_id"].astype(str)
    df["fecha"] = _fecha(df["fecha"])
    df["puntaje"] = pd.to_numeric(df["puntaje"], errors="coerce").astype(float)
    return df
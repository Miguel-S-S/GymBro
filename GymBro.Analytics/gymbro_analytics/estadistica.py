"""Etapa 12: funciones estadisticas puras de la aplicacion de analisis (probadas con datos de resultado conocido).

Todas son descriptivas o inferenciales; ninguna establece causalidad ni anticipa resultados futuros.
"""
from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import pandas as pd
from scipy import stats

ALFA = 0.05


def descriptiva(df: pd.DataFrame, columnas: list[str]) -> pd.DataFrame:
    """Por variable: n con dato, faltantes (cantidad y %), media, desvio, minimo, cuartiles y maximo."""
    filas = []
    for c in columnas:
        s = pd.to_numeric(df[c], errors="coerce")
        n = int(s.notna().sum())
        filas.append({
            "variable": c, "n": n, "faltantes": int(s.isna().sum()),
            "faltantes_%": float(100 * s.isna().mean()) if len(s) else float("nan"),
            "media": s.mean(), "desvio": s.std(ddof=1), "minimo": s.min(), "p25": s.quantile(0.25),
            "mediana": s.median(), "p75": s.quantile(0.75), "maximo": s.max(),
        })
    return pd.DataFrame(filas)


@dataclass
class Correlacion:
    n: int
    pearson_r: float
    pearson_p: float
    spearman_rho: float
    spearman_p: float


def correlacion(x: pd.Series, y: pd.Series) -> Correlacion:
    """Pearson (asociacion lineal) y Spearman (monotona, por rangos) sobre los pares completos."""
    d = pd.DataFrame({"x": pd.to_numeric(x, errors="coerce"), "y": pd.to_numeric(y, errors="coerce")}).dropna()
    if len(d) < 3 or d["x"].nunique() < 2 or d["y"].nunique() < 2:
        return Correlacion(len(d), float("nan"), float("nan"), float("nan"), float("nan"))
    r, p = stats.pearsonr(d["x"], d["y"])
    rho, ps = stats.spearmanr(d["x"], d["y"])
    return Correlacion(len(d), float(r), float(p), float(rho), float(ps))


def agregar_por_socio_modal(df: pd.DataFrame, socio: str, grupo: str, resultado: str) -> pd.DataFrame:
    """
    Una fila por socio: su categoria mas frecuente (desempate por orden alfabetico) y la media de su resultado. Evita la
    pseudorreplicacion: las observaciones de un mismo socio no son independientes y no se cuentan como muestras distintas.
    """
    d = df[[socio, grupo, resultado]].dropna()
    moda = (d.groupby([socio, grupo]).size().reset_index(name="k")
             .sort_values([socio, "k", grupo], ascending=[True, False, True]).drop_duplicates(socio))
    media = d.groupby(socio)[resultado].mean().reset_index()
    return moda[[socio, grupo]].merge(media, on=socio)


@dataclass
class ResultadoGrupos:
    grupos: pd.DataFrame           # n, media, desvio y p de Shapiro-Wilk por grupo
    levene_p: float
    supuestos_ok: bool
    anova_f: float
    anova_p: float
    eta_cuadrado: float
    kruskal_h: float
    kruskal_p: float
    epsilon_cuadrado: float
    prueba_recomendada: str


def comparar_grupos(valores: pd.Series, grupos: pd.Series) -> ResultadoGrupos:
    """
    ANOVA de un factor con verificacion de supuestos: normalidad por grupo (Shapiro-Wilk, si n >= 3) y homogeneidad de
    varianzas (Levene, centrada en la mediana). Si algun supuesto no se cumple (p < 0,05), se recomienda Kruskal-Wallis.
    Tamanio del efecto: eta cuadrado (ANOVA) y epsilon cuadrado (Kruskal-Wallis).
    """
    d = pd.DataFrame({"v": pd.to_numeric(valores, errors="coerce"), "g": grupos}).dropna()
    muestras = {g: s["v"].to_numpy() for g, s in d.groupby("g") if len(s) > 0}
    if len(muestras) < 2:
        raise ValueError("Hacen falta al menos dos grupos con datos.")
    filas, normal = [], True
    for g, v in muestras.items():
        p = float(stats.shapiro(v).pvalue) if len(v) >= 3 and np.ptp(v) > 0 else float("nan")
        if not np.isnan(p) and p < ALFA:
            normal = False
        filas.append({"grupo": g, "n": len(v), "media": float(np.mean(v)), "desvio": float(np.std(v, ddof=1)) if len(v) > 1 else float("nan"),
                      "shapiro_p": p})
    arrays = list(muestras.values())
    levene_p = float(stats.levene(*arrays, center="median").pvalue)
    f, p_anova = stats.f_oneway(*arrays)
    todos = np.concatenate(arrays)
    ss_total = float(((todos - todos.mean()) ** 2).sum())
    ss_entre = float(sum(len(a) * (a.mean() - todos.mean()) ** 2 for a in arrays))
    h, p_kw = stats.kruskal(*arrays)
    n, k = len(todos), len(arrays)
    supuestos = normal and levene_p >= ALFA
    return ResultadoGrupos(
        grupos=pd.DataFrame(filas), levene_p=levene_p, supuestos_ok=supuestos,
        anova_f=float(f), anova_p=float(p_anova), eta_cuadrado=ss_entre / ss_total if ss_total > 0 else float("nan"),
        kruskal_h=float(h), kruskal_p=float(p_kw), epsilon_cuadrado=float((h - k + 1) / (n - k)) if n > k else float("nan"),
        prueba_recomendada="ANOVA" if supuestos else "Kruskal-Wallis",
    )


# ---------------- Tableros de gestion ----------------

def retencion_por_cohorte(asistencias: pd.DataFrame) -> pd.DataFrame:
    """
    Cohorte = mes de la primera asistencia observada. Para cada cohorte y mes desde su inicio (0, 1, 2, ...), porcentaje de
    sus socios con al menos una asistencia en ese mes.
    """
    a = asistencias[["socio_id", "fecha"]].copy()
    a["mes"] = a["fecha"].dt.to_period("M")
    primera = a.groupby("socio_id")["mes"].min().rename("cohorte")
    a = a.join(primera, on="socio_id")
    a["desde_inicio"] = (a["mes"] - a["cohorte"]).apply(lambda x: x.n)
    activos = a.groupby(["cohorte", "desde_inicio"])["socio_id"].nunique()
    tam = primera.value_counts().rename("tamanio")
    tabla = (activos.unstack("desde_inicio").div(tam, axis=0) * 100).round(1)
    tabla.index = tabla.index.astype(str)
    tabla = tabla.sort_index()
    tabla.columns = [f"mes {c}" for c in tabla.columns]
    tabla.insert(0, "socios", tam.sort_index().values)
    return tabla


def morosidad_por_periodo(cuotas: pd.DataFrame, hoy: pd.Timestamp) -> pd.DataFrame:
    """Por periodo: cuotas, vencidas a hoy (pendientes con vencimiento anterior), pagadas tarde y porcentajes."""
    c = cuotas.copy()
    c["periodo"] = c["anio"].astype(str) + "-" + c["mes"].astype(int).map("{:02d}".format)
    c["vencida"] = (c["fecha_pago"].isna()) & (c["vencimiento"] < hoy)
    c["pagada_tarde"] = c["fecha_pago"].notna() & (c["fecha_pago"] > c["vencimiento"])
    g = c.groupby("periodo").agg(cuotas=("socio_id", "size"), vencidas=("vencida", "sum"), pagadas_tarde=("pagada_tarde", "sum"))
    g["vencidas_%"] = (100 * g["vencidas"] / g["cuotas"]).round(1)
    g["pagadas_tarde_%"] = (100 * g["pagadas_tarde"] / g["cuotas"]).round(1)
    return g.reset_index()


def horas_pico(asistencias: pd.DataFrame, zona: str = "America/Argentina/Buenos_Aires") -> pd.DataFrame:
    """Cantidad de asistencias por dia de la semana (0 = lunes) y hora local de ingreso."""
    local = asistencias["instante"].dt.tz_convert(zona)
    return (pd.DataFrame({"dia": local.dt.dayofweek, "hora": local.dt.hour})
            .groupby(["dia", "hora"]).size().rename("asistencias").reset_index())


def asistencia_vs_antiguedad(socios: pd.DataFrame, asistencias: pd.DataFrame) -> pd.DataFrame:
    """Por socio: asistencias por semana en el periodo observado y antiguedad (dias) al final del periodo."""
    fin = asistencias["fecha"].max()
    inicio = asistencias["fecha"].min()
    semanas = max(1.0, (fin - inicio).days / 7)
    por_socio = asistencias.groupby("socio_id").size().rename("asistencias")
    d = socios.set_index("socio_id").join(por_socio).fillna({"asistencias": 0})
    d["asistencias_por_semana"] = d["asistencias"] / semanas
    d["antiguedad_dias"] = (fin - d["fecha_alta"]).dt.days
    return d.reset_index()[["socio_id", "antiguedad_dias", "asistencias_por_semana"]]

"""Etapa 12: aplicacion de analisis y tableros de GymBro (Streamlit).

Ejecutar (con el rol de solo lectura de docs/pfc/scripts/rol-analitica.sql):
  ANALITICA_PG_CLAVE=... ANALITICA_APP_USUARIO=... ANALITICA_APP_CLAVE=... \
      GymBro.Analytics/.venv/Scripts/streamlit run GymBro.Analytics/app/principal.py

Acceso: usuario y contrasena de configuracion (ANALITICA_APP_USUARIO y ANALITICA_APP_CLAVE). No hay inicio de sesion
unico con el JWT de la API (limitacion declarada, DV-14). Los datos salen de las vistas del esquema "analitica", que solo
exponen socios con origen Sintetico.
"""
from __future__ import annotations

import hmac
import os
import sys
from pathlib import Path

import altair as alt
import pandas as pd
import streamlit as st

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gymbro_analytics import ENCABEZADO_SINTETICO, extraccion  # noqa: E402
from gymbro_analytics.conexion import conectar  # noqa: E402
from gymbro_analytics.estadistica import (agregar_por_socio_modal, asistencia_vs_antiguedad, comparar_grupos,  # noqa: E402
                                          correlacion, descriptiva, horas_pico, morosidad_por_periodo,
                                          retencion_por_cohorte)

st.set_page_config(page_title="GymBro · Análisis", layout="wide")
DIAS = ["Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom"]
BIENESTAR = {"horas_sueno": "Horas de sueño", "estres": "Estrés (1-7)", "fatiga": "Fatiga (1-7)",
             "frecuencia_cardiaca_reposo": "FC de reposo (lpm)"}


# ---------------- Acceso ----------------

def credenciales_configuradas() -> tuple[str, str] | None:
    u, c = os.environ.get("ANALITICA_APP_USUARIO"), os.environ.get("ANALITICA_APP_CLAVE")
    return (u, c) if u and c else None


def pantalla_de_acceso() -> bool:
    if st.session_state.get("autenticado"):
        return True
    cred = credenciales_configuradas()
    st.title("GymBro · Análisis")
    if cred is None:
        st.error("La aplicación no tiene usuario y contraseña configurados (ANALITICA_APP_USUARIO y ANALITICA_APP_CLAVE). "
                 "No se muestra ningún dato.")
        return False
    with st.form("acceso"):
        usuario = st.text_input("Usuario")
        clave = st.text_input("Contraseña", type="password")
        if st.form_submit_button("Ingresar"):
            ok = hmac.compare_digest(usuario.encode(), cred[0].encode()) & hmac.compare_digest(clave.encode(), cred[1].encode())
            if ok:
                st.session_state["autenticado"] = True
                st.rerun()
            st.error("Usuario o contraseña incorrectos.")
    return False


# ---------------- Datos ----------------

@st.cache_data(ttl=300, show_spinner="Leyendo datos sintéticos...")
def cargar(gimnasio: str | None) -> dict[str, pd.DataFrame]:
    with conectar(solo_lectura=True) as con:
        return {
            "socios": extraccion.socios(con, gimnasio), "asistencias": extraccion.asistencias(con, gimnasio),
            "cuotas": extraccion.cuotas(con, gimnasio), "bienestar": extraccion.bienestar(con, gimnasio),
            "series": extraccion.series(con, gimnasio), "predicciones": extraccion.predicciones(con, gimnasio),
        }


@st.cache_data(ttl=300)
def cargar_gimnasios() -> pd.DataFrame:
    with conectar(solo_lectura=True) as con:
        return extraccion.gimnasios(con)


def rotulo_origen(d: dict[str, pd.DataFrame]) -> None:
    origenes = sorted(d["socios"]["origen_datos"].unique().tolist()) or ["(sin datos)"]
    st.info(f"**Origen de los datos: {', '.join(origenes)}.** {ENCABEZADO_SINTETICO}")


def pregunta(texto: str, supuestos: str) -> None:
    st.markdown(f"**Pregunta que responde:** {texto}  \n**Supuestos y límites:** {supuestos}")


# ---------------- Secciones ----------------

def seccion_descriptiva(d: dict[str, pd.DataFrame]) -> None:
    pregunta("¿Cómo se distribuyen la carga, las repeticiones, el RIR y el bienestar, y cuántos datos faltan?",
             "Describe la muestra; no generaliza a otras poblaciones. Las series de un mismo socio no son independientes.")
    ef = d["series"][~d["series"]["es_calentamiento"].astype(bool)]
    c1, c2 = st.columns(2)
    with c1:
        st.subheader("Series efectivas")
        st.caption(f"{len(ef)} series de {ef['socio_id'].nunique()} socios")
        st.dataframe(descriptiva(ef, ["peso_kg", "repeticiones", "rir", "desvio_rir"]), hide_index=True)
    with c2:
        st.subheader("Bienestar diario")
        st.caption(f"{len(d['bienestar'])} registros de {d['bienestar']['socio_id'].nunique()} socios")
        st.dataframe(descriptiva(d["bienestar"], list(BIENESTAR)), hide_index=True)
    variable = st.selectbox("Distribución de", ["peso_kg", "repeticiones", "rir", "desvio_rir"] + list(BIENESTAR))
    fuente = ef if variable in ef.columns else d["bienestar"]
    st.altair_chart(alt.Chart(fuente.dropna(subset=[variable])).mark_bar().encode(
        x=alt.X(f"{variable}:Q", bin=alt.Bin(maxbins=30), title=variable), y=alt.Y("count()", title="frecuencia")),
        width="stretch")


def dias_con_desvio(d: dict[str, pd.DataFrame]) -> pd.DataFrame:
    ef = d["series"][~d["series"]["es_calentamiento"].astype(bool)].dropna(subset=["desvio_rir"])
    por_dia = ef.groupby(["socio_id", "fecha"])["desvio_rir"].mean().rename("desvio_rir_medio").reset_index()
    return por_dia.merge(d["bienestar"], on=["socio_id", "fecha"], how="inner")


def seccion_correlacion(d: dict[str, pd.DataFrame]) -> None:
    pregunta("¿Se asocian las variables de bienestar del día con el desvío del RIR respecto del objetivo?",
             "Pearson mide asociación lineal y Spearman monótona. **Correlación no implica causalidad.** "
             "Por día, las observaciones de un mismo socio no son independientes y el valor p resulta optimista; "
             "la versión por socio (medias) es la conservadora.")
    st.warning("Correlación no implica causalidad.")
    dias = dias_con_desvio(d)
    nivel = st.radio("Unidad de análisis", ["Día (socio y fecha)", "Socio (medias)"], horizontal=True)
    if nivel.startswith("Socio"):
        dias = dias.groupby("socio_id")[["desvio_rir_medio"] + list(BIENESTAR)].mean().reset_index()
    filas = []
    for col, nombre in BIENESTAR.items():
        c = correlacion(dias[col], dias["desvio_rir_medio"])
        filas.append({"variable": nombre, "n": c.n, "Pearson r": c.pearson_r, "p (Pearson)": c.pearson_p,
                      "Spearman rho": c.spearman_rho, "p (Spearman)": c.spearman_p})
    st.dataframe(pd.DataFrame(filas), hide_index=True)
    var = st.selectbox("Diagrama de dispersión", list(BIENESTAR), format_func=BIENESTAR.get)
    st.altair_chart(alt.Chart(dias.dropna(subset=[var, "desvio_rir_medio"])).mark_circle(opacity=0.4).encode(
        x=alt.X(f"{var}:Q", title=BIENESTAR[var]), y=alt.Y("desvio_rir_medio:Q", title="desvío RIR medio")),
        width="stretch")


def seccion_anova(d: dict[str, pd.DataFrame]) -> None:
    pregunta("¿Difiere el desvío medio del RIR entre socios según su categoría de ingesta más frecuente?",
             "Se agrega por socio (una observación por socio: su categoría más frecuente y su desvío medio) para evitar "
             "la pseudorreplicación. Es una comparación ENTRE socios. ANOVA supone normalidad por grupo (Shapiro-Wilk) y "
             "varianzas homogéneas (Levene); si no se cumplen, se informa Kruskal-Wallis. No establece causalidad.")
    dias = dias_con_desvio(d).dropna(subset=["nutricion_categoria"])
    agregado = agregar_por_socio_modal(dias, "socio_id", "nutricion_categoria", "desvio_rir_medio")
    if agregado["nutricion_categoria"].nunique() < 2:
        st.info("No hay al menos dos categorías con socios para comparar.")
        return
    r = comparar_grupos(agregado["desvio_rir_medio"], agregado["nutricion_categoria"])
    st.dataframe(r.grupos, hide_index=True)
    c1, c2, c3 = st.columns(3)
    c1.metric("Levene (p)", f"{r.levene_p:.3f}")
    c2.metric("ANOVA F (p)", f"{r.anova_f:.2f} ({r.anova_p:.3f})", help=f"eta² = {r.eta_cuadrado:.3f}")
    c3.metric("Kruskal-Wallis H (p)", f"{r.kruskal_h:.2f} ({r.kruskal_p:.3f})", help=f"epsilon² = {r.epsilon_cuadrado:.3f}")
    st.markdown(f"Prueba que corresponde según los supuestos: **{r.prueba_recomendada}**. "
                f"Tamaño del efecto: eta² = {r.eta_cuadrado:.3f}; epsilon² = {r.epsilon_cuadrado:.3f}.")


def seccion_gestion(d: dict[str, pd.DataFrame]) -> None:
    pregunta("¿Cómo evolucionan la retención, la morosidad y la ocupación, y qué socios tienen mayor puntaje de riesgo?",
             "Tableros descriptivos. El puntaje de riesgo viene de un modelo entrenado con datos sintéticos (Etapa 10b-1): "
             "ordena socios por similitud con patrones de abandono de esos datos; no es un pronóstico sobre personas.")
    a = d["asistencias"]
    if a.empty:
        st.info("Sin asistencias para el gimnasio elegido.")
        return
    st.subheader("Retención por cohorte (% de socios con asistencia en cada mes desde su primera asistencia)")
    st.dataframe(retencion_por_cohorte(a))
    st.subheader("Morosidad por período (derivada: pendiente con vencimiento anterior al último día observado)")
    st.dataframe(morosidad_por_periodo(d["cuotas"], a["fecha"].max()), hide_index=True)
    st.subheader("Horas pico (asistencias por día y hora local)")
    hp = horas_pico(a)
    hp["dia"] = hp["dia"].map(lambda i: DIAS[i])
    st.altair_chart(alt.Chart(hp).mark_rect().encode(
        x=alt.X("hora:O", title="hora"), y=alt.Y("dia:O", sort=DIAS, title="día"),
        color=alt.Color("asistencias:Q", title="asistencias")), width="stretch")
    st.subheader("Asistencia frente a antigüedad")
    st.altair_chart(alt.Chart(asistencia_vs_antiguedad(d["socios"], a)).mark_circle(opacity=0.5).encode(
        x=alt.X("antiguedad_dias:Q", title="antigüedad (días)"), y=alt.Y("asistencias_por_semana:Q", title="asistencias por semana")),
        width="stretch")
    st.subheader("Puntaje de riesgo de abandono (último por socio)")
    p = d["predicciones"]
    if p.empty:
        st.info("Todavía no hay puntajes (ejecutar puntuar_abandono.py).")
    else:
        ult = p.sort_values("fecha").groupby("socio_id").tail(1)
        st.caption(f"{len(ult)} socios; fecha {ult['fecha'].max():%Y-%m-%d}; modelo {', '.join(sorted(ult['version_modelo'].unique()))}")
        st.altair_chart(alt.Chart(ult).mark_bar().encode(
            x=alt.X("puntaje:Q", bin=alt.Bin(maxbins=20), title="puntaje"), y=alt.Y("count()", title="socios")),
            width="stretch")
        top = ult.sort_values("puntaje", ascending=False).head(15).copy()
        top["socio"] = top["socio_id"].str[:8]
        st.dataframe(top[["socio", "fecha", "puntaje"]], hide_index=True)


def main() -> None:
    if not pantalla_de_acceso():
        return
    st.title("GymBro · Análisis de datos sintéticos")
    gimnasios = cargar_gimnasios()
    opciones = {"Todos los gimnasios sintéticos": None} | dict(zip(gimnasios["nombre"], gimnasios["gimnasio_id"]))
    elegido = st.sidebar.selectbox("Gimnasio", list(opciones))
    st.sidebar.caption(ENCABEZADO_SINTETICO)
    d = cargar(opciones[elegido])
    rotulo_origen(d)
    st.caption(f"Tamaño muestral: {d['socios']['socio_id'].nunique()} socios, {len(d['asistencias'])} asistencias, "
               f"{len(d['series'])} series, {len(d['bienestar'])} registros de bienestar, {len(d['cuotas'])} cuotas.")
    t1, t2, t3, t4 = st.tabs(["Descriptiva", "Correlación", "ANOVA", "Gestión"])
    with t1:
        seccion_descriptiva(d)
    with t2:
        seccion_correlacion(d)
    with t3:
        seccion_anova(d)
    with t4:
        seccion_gestion(d)


main()

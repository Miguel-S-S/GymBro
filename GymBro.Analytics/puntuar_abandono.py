"""Etapa 10b-1: puntuacion por lotes del riesgo de abandono (escribe en public.prediccion_abandono).

Calcula las variables de cada socio activo a la fecha de corte (por defecto, el dia siguiente a la ultima asistencia
observada), aplica el modelo guardado por entrenar_abandono.py e inserta un puntaje por socio. Es idempotente: un indice
unico (socio, fecha, version) y ON CONFLICT DO NOTHING evitan duplicados.

Usa el rol gymbro_analitica_puntuacion (lee las vistas del esquema analitica, que solo exponen socios Sintetico, e
inserta puntajes; no puede leer ni modificar las tablas de la aplicacion):
  ANALITICA_PG_USUARIO=gymbro_analitica_puntuacion ANALITICA_PG_CLAVE=... python GymBro.Analytics/puntuar_abandono.py \
      [--modelo RUTA.joblib] [--fecha AAAA-MM-DD] [--gimnasio ID]
"""
from __future__ import annotations

import argparse
import datetime as dt
import sys
import uuid
from pathlib import Path

import joblib
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gymbro_analytics import extraccion  # noqa: E402
from gymbro_analytics.conexion import conectar  # noqa: E402
from gymbro_analytics.variables import VARIABLES, construir  # noqa: E402

CARPETA_MODELOS = Path(__file__).resolve().parent / "modelos"

SQL_INSERTAR = """
INSERT INTO public.prediccion_abandono (id, socio_id, fecha, puntaje, version_modelo, origen_datos, fecha_alta, usuario_alta)
VALUES (%s, %s, %s, %s, %s, %s, now(), 1)
ON CONFLICT DO NOTHING
"""


def ultimo_modelo() -> Path:
    candidatos = sorted(CARPETA_MODELOS.glob("abandono-*.joblib"), key=lambda p: p.stat().st_mtime)
    if not candidatos:
        sys.exit("No hay modelos guardados: ejecute antes entrenar_abandono.py.")
    return candidatos[-1]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--modelo", type=Path, default=None)
    ap.add_argument("--fecha", default=None, help="fecha de corte AAAA-MM-DD")
    ap.add_argument("--gimnasio", default=None)
    args = ap.parse_args()

    paquete = joblib.load(args.modelo or ultimo_modelo())
    modelo, version = paquete["modelo"], paquete["version"]
    if paquete["variables"] != VARIABLES:
        sys.exit("El modelo guardado usa otras variables; vuelva a entrenarlo.")

    with conectar(solo_lectura=False, usuario_por_defecto="gymbro_analitica_puntuacion") as con:
        socios = extraccion.socios(con, args.gimnasio)
        asistencias = extraccion.asistencias(con, args.gimnasio)
        cuotas = extraccion.cuotas(con, args.gimnasio)
        corte = pd.Timestamp(args.fecha) if args.fecha else asistencias["fecha"].max() + pd.Timedelta(days=1)
        # Sin etiqueta: solo informacion anterior al corte.
        datos = construir(socios, asistencias, cuotas, [corte], con_etiqueta=False)
        if datos.empty:
            print(f"No hay socios activos al {corte:%Y-%m-%d}.")
            return 0
        puntajes = modelo.predict_proba(datos[VARIABLES].to_numpy(dtype=float))[:, 1]
        origen = socios.set_index("socio_id")["origen_datos"]
        filas = [(str(uuid.uuid4()), s, corte.date(), round(float(p), 4), version, origen[s])
                 for s, p in zip(datos["socio_id"], puntajes)]
        cur = con.cursor()
        try:
            cur.executemany(SQL_INSERTAR, filas)
            con.commit()
        finally:
            cur.close()
    print(f"Puntuados {len(filas)} socios al {corte:%Y-%m-%d} con {version} (los repetidos se omiten).")
    print(f"Puntaje: media {puntajes.mean():.3f}, minimo {puntajes.min():.3f}, maximo {puntajes.max():.3f}.")
    print(f"Fecha de ejecucion: {dt.datetime.now():%Y-%m-%d %H:%M}. Datos sinteticos (Sintetico).")
    return 0


if __name__ == "__main__":
    sys.exit(main())

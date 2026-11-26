"""Conexion a PostgreSQL para la analitica (Etapas 10b-1 y 12).

Usa SIEMPRE un rol propio y nunca las credenciales de la aplicacion:
  - lectura:     rol gymbro_analitica_lectura, solo SELECT sobre las vistas del esquema "analitica", que exponen
                 unicamente datos de socios con origen Sintetico (docs/pfc/scripts/rol-analitica.sql);
  - puntuacion:  rol gymbro_analitica_puntuacion, ademas INSERT en public.prediccion_abandono.
Variables de entorno (sin valores por defecto para la contrasena; nada de secretos en el repositorio):
  ANALITICA_PG_HOST (localhost), ANALITICA_PG_PORT (5433), ANALITICA_PG_DB (gymbro_db),
  ANALITICA_PG_USUARIO (gymbro_analitica_lectura), ANALITICA_PG_CLAVE (obligatoria).
"""
from __future__ import annotations

import os
from contextlib import contextmanager


def parametros(usuario_por_defecto: str = "gymbro_analitica_lectura") -> dict:
    clave = os.environ.get("ANALITICA_PG_CLAVE")
    if not clave:
        raise RuntimeError("Falta ANALITICA_PG_CLAVE (contrasena del rol de analitica; ver docs/pfc/scripts/rol-analitica.sql).")
    return {
        "host": os.environ.get("ANALITICA_PG_HOST", "localhost"),
        "port": int(os.environ.get("ANALITICA_PG_PORT", "5433")),
        "dbname": os.environ.get("ANALITICA_PG_DB", "gymbro_db"),
        "user": os.environ.get("ANALITICA_PG_USUARIO", usuario_por_defecto),
        "password": clave,
    }


@contextmanager
def conectar(solo_lectura: bool = True, usuario_por_defecto: str = "gymbro_analitica_lectura"):
    """Conexion psycopg2. Con solo_lectura, la sesion queda en modo de solo lectura ademas del rol."""
    import psycopg2  # importacion diferida: las pruebas de funciones puras no necesitan el controlador

    con = psycopg2.connect(**parametros(usuario_por_defecto))
    try:
        if solo_lectura:
            con.set_session(readonly=True)
        yield con
    finally:
        con.close()

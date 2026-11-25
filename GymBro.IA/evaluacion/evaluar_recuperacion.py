"""Etapa 9b-1: evaluacion de la recuperacion de ejercicios (recall@1, @3, @5 y MRR), espanol contra ingles.

Lee docs/pfc/evaluacion/banco-consultas.csv (columnas: id, consulta_es, consulta_en, zona, equipamiento, ids_relevantes)
y consulta la API real (POST /api/Ejercicios/busqueda-inteligente), de modo que se evalua todo el camino: embedding de
Ollama, filtros duros de GymBro.Api y orden por distancia coseno en pgvector.

Requisitos: GymBro.Api y GymBro.IA en ejecucion, Ollama con el modelo de embeddings y un JWT valido.
Variables de entorno:
  GYMBRO_API_URL      URL base de la API (por defecto http://localhost:5069)
  GYMBRO_API_TOKEN    JWT de un Dueno o Personal Trainer (o del Administrador de la Plataforma con GYMBRO_GIMNASIO_ID)
  GYMBRO_GIMNASIO_ID  opcional; gimnasio de referencia (solo lo usa el Administrador de la Plataforma)

Uso:
  python GymBro.IA/evaluacion/evaluar_recuperacion.py [--banco RUTA] [--salida CARPETA]

Si ninguna consulta tiene ids relevantes, el script termina SIN calcular metricas (codigo 3): las etiquetas las ponen los
autores y la Personal Trainer; nunca se inventan.
"""
from __future__ import annotations

import argparse
import csv
import datetime as dt
import json
import os
import sys
from pathlib import Path

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent))
from metricas import resumir, separar_ids  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
BANCO_POR_DEFECTO = RAIZ / "docs" / "pfc" / "evaluacion" / "banco-consultas.csv"
SALIDA_POR_DEFECTO = RAIZ / "docs" / "pfc" / "evaluacion" / "resultados"
COLUMNAS = ["id", "consulta_es", "consulta_en", "zona", "equipamiento", "ids_relevantes"]
K_MAXIMO = 5


def leer_banco(ruta: Path) -> list[dict]:
    with ruta.open(encoding="utf-8", newline="") as f:
        lector = csv.DictReader(f)
        faltan = [c for c in COLUMNAS if c not in (lector.fieldnames or [])]
        if faltan:
            sys.exit(f"Al banco le faltan columnas: {', '.join(faltan)}")
        return list(lector)


def buscar(url: str, token: str, gimnasio: str | None, consulta: str, zona: str, equipamiento: str) -> list[str]:
    cuerpo = {"consulta": consulta, "cantidad": K_MAXIMO}
    if zona.strip():
        cuerpo["zona"] = zona.strip()
    if equipamiento.strip():
        cuerpo["equipamiento"] = [e.strip() for e in equipamiento.replace(",", ";").split(";") if e.strip()]
    if gimnasio:
        cuerpo["gimnasioId"] = gimnasio
    r = requests.post(f"{url}/api/Ejercicios/busqueda-inteligente", json=cuerpo,
                      headers={"Authorization": f"Bearer {token}"}, timeout=60)
    r.raise_for_status()
    return [x["id"] for x in r.json()]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--banco", type=Path, default=BANCO_POR_DEFECTO)
    ap.add_argument("--salida", type=Path, default=SALIDA_POR_DEFECTO)
    args = ap.parse_args()

    filas = leer_banco(args.banco)
    etiquetadas = [f for f in filas if separar_ids(f.get("ids_relevantes"))]
    print(f"Banco: {len(filas)} consultas; etiquetadas: {len(etiquetadas)}.")
    if not etiquetadas:
        print("No hay consultas etiquetadas: no se calculan metricas (PUNTO DE ESPERA 1 de la Etapa 9b-1).")
        return 3

    url = os.environ.get("GYMBRO_API_URL", "http://localhost:5069").rstrip("/")
    token = os.environ.get("GYMBRO_API_TOKEN")
    if not token:
        sys.exit("Falta GYMBRO_API_TOKEN (JWT de un Dueno o Personal Trainer).")
    gimnasio = os.environ.get("GYMBRO_GIMNASIO_ID")

    detalle = []
    por_idioma = {"es": [], "en": []}
    for f in etiquetadas:
        relevantes = separar_ids(f["ids_relevantes"])
        fila = {"id": f["id"], "relevantes": relevantes}
        for idioma in ("es", "en"):
            consulta = f[f"consulta_{idioma}"]
            devueltos = buscar(url, token, gimnasio, consulta, f.get("zona", ""), f.get("equipamiento", ""))
            fila[f"devueltos_{idioma}"] = devueltos
            por_idioma[idioma].append((devueltos, relevantes))
        detalle.append(fila)

    resumen = {idioma: resumir(res) for idioma, res in por_idioma.items()}
    marca = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    args.salida.mkdir(parents=True, exist_ok=True)
    salida = args.salida / f"evaluacion-recuperacion-{marca}.json"
    salida.write_text(json.dumps({
        "fecha": dt.datetime.now().isoformat(timespec="seconds"),
        "api": url,
        "banco": str(args.banco.relative_to(RAIZ)) if args.banco.is_relative_to(RAIZ) else str(args.banco),
        "consultas_en_banco": len(filas),
        "consultas_etiquetadas": len(etiquetadas),
        "k_maximo": K_MAXIMO,
        "resumen": resumen,
        "detalle": detalle,
    }, ensure_ascii=False, indent=2), encoding="utf-8")

    for idioma, r in resumen.items():
        print(f"[{idioma}] n={r['n']} recall@1={r['recall@1']:.3f} recall@3={r['recall@3']:.3f} "
              f"recall@5={r['recall@5']:.3f} MRR={r['mrr']:.3f}")
    print(f"Salida guardada en {salida}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

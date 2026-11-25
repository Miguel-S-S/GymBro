"""Etapa 9b-2: medicion de los sustitutos con el modelo local (sin inventar cifras: solo lo que se ejecuta).

Para cada consulta del banco (docs/pfc/evaluacion/banco-consultas.csv) recupera candidatos con la API real
(POST /api/Ejercicios/busqueda-inteligente, filtros duros de la 9b-1), toma el primero como "ejercicio a sustituir" y los
siguientes como candidatos, y llama a GymBro.IA /sugerir-sustitutos/. Mide:
  - porcentaje de salidas que respetan el conjunto recuperado (JSON valido y todos los ids elegidos entre los candidatos);
  - latencia de cada llamada al modelo (la que informa GymBro.IA).
Guarda una salida JSON por ejecucion en docs/pfc/evaluacion/resultados/.

Requisitos: GymBro.Api, GymBro.IA (con OLLAMA_MODELO_GENERACION) y Ollama en ejecucion.
Variables: GYMBRO_API_URL, GYMBRO_API_TOKEN, GYMBRO_GIMNASIO_ID (opcional), GYMBRO_IA_URL (por defecto http://127.0.0.1:8000).
Uso: python GymBro.IA/evaluacion/medir_sustitutos.py [--motivo "molestia lumbar"] [--candidatos 8]
"""
from __future__ import annotations

import argparse
import csv
import datetime as dt
import json
import os
import statistics
import sys
from pathlib import Path

import requests

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from sustitutos import respeta_conjunto  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
BANCO = RAIZ / "docs" / "pfc" / "evaluacion" / "banco-consultas.csv"
SALIDA = RAIZ / "docs" / "pfc" / "evaluacion" / "resultados"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--motivo", default="el socio refiere molestias y necesita una alternativa")
    ap.add_argument("--candidatos", type=int, default=8)
    args = ap.parse_args()

    api = os.environ.get("GYMBRO_API_URL", "http://localhost:5069").rstrip("/")
    ia = os.environ.get("GYMBRO_IA_URL", "http://127.0.0.1:8000").rstrip("/")
    token = os.environ.get("GYMBRO_API_TOKEN")
    if not token:
        sys.exit("Falta GYMBRO_API_TOKEN.")
    gimnasio = os.environ.get("GYMBRO_GIMNASIO_ID")

    with BANCO.open(encoding="utf-8", newline="") as f:
        filas = list(csv.DictReader(f))

    registros = []
    for fila in filas:
        cuerpo = {"consulta": fila["consulta_es"], "cantidad": args.candidatos + 1}
        if fila.get("zona"):
            cuerpo["zona"] = fila["zona"]
        if gimnasio:
            cuerpo["gimnasioId"] = gimnasio
        r = requests.post(f"{api}/api/Ejercicios/busqueda-inteligente", json=cuerpo,
                          headers={"Authorization": f"Bearer {token}"}, timeout=60)
        r.raise_for_status()
        recuperados = r.json()
        if len(recuperados) < 2:
            registros.append({"id": fila["id"], "omitida": "menos de 2 ejercicios recuperados"})
            continue
        ejercicio, candidatos = recuperados[0], recuperados[1:]

        def ref(e):
            return {"id": e["id"], "nombre": e["nombre"], "zona": e.get("zonaObjetivo"), "musculo": e.get("musculoPrincipal"),
                    "equipamiento": e.get("equipamiento")}

        s = requests.post(f"{ia}/sugerir-sustitutos/", json={"ejercicio": ref(ejercicio), "motivo": args.motivo,
                                                             "candidatos": [ref(c) for c in candidatos], "maximo": 3}, timeout=300)
        if s.status_code != 200:
            registros.append({"id": fila["id"], "error": f"{s.status_code}: {s.text[:200]}"})
            continue
        datos = s.json()
        respeta, elegidos = respeta_conjunto(datos["respuesta"], [c["id"] for c in candidatos])
        registros.append({"id": fila["id"], "modelo": datos["modelo"], "latencia_ms": datos["latencia_ms"],
                          "respeta_conjunto": respeta, "elegidos": elegidos, "candidatos": len(candidatos)})

    medidos = [x for x in registros if "respeta_conjunto" in x]
    resumen = {"llamadas": len(medidos)}
    if medidos:
        lat = [x["latencia_ms"] for x in medidos]
        resumen.update({
            "porcentaje_respeta_conjunto": 100.0 * sum(x["respeta_conjunto"] for x in medidos) / len(medidos),
            "latencia_mediana_ms": statistics.median(lat), "latencia_media_ms": statistics.fmean(lat),
            "latencia_max_ms": max(lat), "modelos": sorted({x["modelo"] for x in medidos}),
        })
    SALIDA.mkdir(parents=True, exist_ok=True)
    marca = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    destino = SALIDA / f"medicion-sustitutos-{marca}.json"
    destino.write_text(json.dumps({"fecha": dt.datetime.now().isoformat(timespec="seconds"), "motivo": args.motivo,
                                   "resumen": resumen, "detalle": registros}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(resumen, ensure_ascii=False, indent=2))
    print(f"Salida guardada en {destino}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

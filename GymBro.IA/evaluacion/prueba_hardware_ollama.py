"""Etapa 9b-1 (y PUNTO DE ESPERA 2 de la 9b-2): prueba de hardware de Ollama en el equipo del autor.

Lista los modelos instalados y mide, para cada uno, la latencia por consulta:
  - modelos de embeddings (nombre con "embed" o familia bert): N llamadas a embeddings con textos cortos de ejercicios;
  - modelos de generacion: N generaciones con un pedido fijo, temperatura 0 y 64 tokens como maximo; se informan ademas
    los tokens por segundo que reporta Ollama (eval_count / eval_duration).
Se descarta una llamada de calentamiento por modelo (carga en memoria) y se informa aparte.

Uso (con Ollama en ejecucion, por defecto en http://localhost:11434):
  python GymBro.IA/evaluacion/prueba_hardware_ollama.py [--repeticiones 10] [--salida CARPETA] [--modelos m1,m2]

La salida (JSON) incluye la plataforma y la cantidad de nucleos; no incluye datos personales.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import platform
import statistics
import sys
import time
from pathlib import Path

import ollama

RAIZ = Path(__file__).resolve().parents[2]
SALIDA_POR_DEFECTO = RAIZ / "docs" / "pfc" / "evaluacion" / "resultados"

TEXTOS_EMBEDDING = [
    "Sentadilla trasera con barra para cuadriceps",
    "Curl femoral acostado en maquina",
    "Elevacion de talones de pie",
    "Remo con mancuerna a una mano",
    "Press de banca plano con barra",
]
PEDIDO_GENERACION = ("Elegi un ejercicio para reemplazar la sentadilla trasera en una persona con dolor lumbar, "
                     "entre: prensa de piernas, sentadilla frontal, extension de rodillas. Responde en una linea.")


def es_embedding(modelo: dict) -> bool:
    nombre = (modelo.get("model") or modelo.get("name") or "").lower()
    familia = str((modelo.get("details") or {}).get("family", "")).lower()
    return "embed" in nombre or "bert" in familia


def resumen(valores: list[float]) -> dict:
    ordenados = sorted(valores)
    p95 = ordenados[min(len(ordenados) - 1, int(round(0.95 * (len(ordenados) - 1))))]
    return {"n": len(valores), "media_ms": statistics.fmean(valores), "mediana_ms": statistics.median(valores),
            "p95_ms": p95, "min_ms": ordenados[0], "max_ms": ordenados[-1]}


def medir(cliente: ollama.Client, modelo: str, embedding: bool, repeticiones: int) -> dict:
    def una(i: int):
        inicio = time.perf_counter()
        if embedding:
            cliente.embeddings(model=modelo, prompt=TEXTOS_EMBEDDING[i % len(TEXTOS_EMBEDDING)])
            extra = None
        else:
            r = cliente.generate(model=modelo, prompt=PEDIDO_GENERACION, options={"temperature": 0, "num_predict": 64})
            extra = (r.get("eval_count") or 0) / ((r.get("eval_duration") or 1) / 1e9)
        return (time.perf_counter() - inicio) * 1000, extra

    calentamiento_ms, _ = una(0)
    latencias, tps = [], []
    for i in range(repeticiones):
        ms, extra = una(i)
        latencias.append(ms)
        if extra is not None:
            tps.append(extra)
    salida = {"modelo": modelo, "tipo": "embeddings" if embedding else "generacion",
              "calentamiento_ms": calentamiento_ms, "latencia": resumen(latencias)}
    if tps:
        salida["tokens_por_segundo_media"] = statistics.fmean(tps)
    return salida


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--repeticiones", type=int, default=10)
    ap.add_argument("--salida", type=Path, default=SALIDA_POR_DEFECTO)
    ap.add_argument("--modelos", default="", help="lista separada por comas; por defecto, todos los instalados")
    args = ap.parse_args()

    cliente = ollama.Client(host=os.environ.get("OLLAMA_HOST", "http://localhost:11434"))
    try:
        instalados = cliente.list()
    except Exception as e:  # servidor apagado
        print(f"Ollama no responde: {e}. Inicie Ollama y vuelva a ejecutar.")
        return 2
    modelos = [dict(m) for m in (instalados.get("models") if isinstance(instalados, dict) else instalados.models)]
    pedidos = {m.strip() for m in args.modelos.split(",") if m.strip()}
    if pedidos:
        modelos = [m for m in modelos if (m.get("model") or m.get("name")) in pedidos]
    if not modelos:
        print("No hay modelos para medir.")
        return 2

    resultados = []
    for m in modelos:
        nombre = m.get("model") or m.get("name")
        print(f"Midiendo {nombre}...")
        try:
            resultados.append(medir(cliente, nombre, es_embedding(m), args.repeticiones))
        except Exception as e:
            resultados.append({"modelo": nombre, "error": str(e)})

    marca = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    args.salida.mkdir(parents=True, exist_ok=True)
    salida = args.salida / f"hardware-ollama-{marca}.json"
    salida.write_text(json.dumps({
        "fecha": dt.datetime.now().isoformat(timespec="seconds"),
        "plataforma": platform.platform(), "procesador": platform.processor(), "nucleos": os.cpu_count(),
        "repeticiones": args.repeticiones, "resultados": resultados,
    }, ensure_ascii=False, indent=2, default=str), encoding="utf-8")
    for r in resultados:
        if "error" in r:
            print(f"{r['modelo']}: error {r['error']}")
        else:
            print(f"{r['modelo']} ({r['tipo']}): mediana {r['latencia']['mediana_ms']:.0f} ms, p95 {r['latencia']['p95_ms']:.0f} ms"
                  + (f", {r['tokens_por_segundo_media']:.1f} tokens/s" if "tokens_por_segundo_media" in r else ""))
    print(f"Salida guardada en {salida}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

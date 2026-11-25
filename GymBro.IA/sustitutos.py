"""Etapa 9b-2 (caso a): sustitutos de ejercicios con un modelo de lenguaje local (Ollama).

Recibe el ejercicio a sustituir, el motivo y la lista de candidatos YA RECUPERADOS (por GymBro.Api, con sus filtros
duros) y pide al modelo elegir y justificar solo entre esos candidatos, con salida JSON. Este servicio no valida la
eleccion: devuelve el texto del modelo tal cual y GymBro.Api verifica en codigo, por identificador, que cada elegido
pertenezca al conjunto recuperado (si no, descarta la salida) y pasa cada sustitucion por el validador de limites.

El modelo se configura con OLLAMA_MODELO_GENERACION (sin valor por defecto: la eleccion depende de la prueba de hardware,
PUNTO DE ESPERA 2). Sin modelo configurado u Ollama caido, responde 503 sin efectos.
"""
from __future__ import annotations

import json
import os
import time
from typing import List, Optional

import ollama
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

router = APIRouter()

MAX_CANDIDATOS = 20


class EjercicioRef(BaseModel):
    id: str
    nombre: str
    zona: Optional[str] = None
    musculo: Optional[str] = None
    equipamiento: Optional[str] = None


class PeticionSustitutos(BaseModel):
    ejercicio: EjercicioRef
    motivo: str = Field(min_length=3, max_length=300)
    candidatos: List[EjercicioRef] = Field(min_length=1, max_length=MAX_CANDIDATOS)
    maximo: int = Field(default=3, ge=1, le=5)


def armar_mensajes(p: PeticionSustitutos) -> list[dict]:
    lista = "\n".join(
        f'- id: {c.id} | {c.nombre} | zona: {c.zona or "-"} | musculo: {c.musculo or "-"} | equipamiento: {c.equipamiento or "-"}'
        for c in p.candidatos
    )
    sistema = (
        "Sos un asistente de un personal trainer. Tu tarea es elegir sustitutos de un ejercicio SOLO entre los candidatos "
        "de la lista dada, identificados por su id. No inventes ejercicios ni ids. Si ninguno sirve, devolve una lista vacia. "
        'Responde UNICAMENTE con JSON con esta forma: {"elegidos": [{"id": "<id de la lista>", "justificacion": "<una oracion>"}]}.'
    )
    usuario = (
        f"Ejercicio a sustituir: {p.ejercicio.nombre} (zona: {p.ejercicio.zona or '-'}, musculo: {p.ejercicio.musculo or '-'}, "
        f"equipamiento: {p.ejercicio.equipamiento or '-'}).\n"
        f"Motivo: {p.motivo}\n"
        f"Elegi como maximo {p.maximo} candidatos, del mas al menos adecuado.\n"
        f"Candidatos:\n{lista}"
    )
    return [{"role": "system", "content": sistema}, {"role": "user", "content": usuario}]


@router.post("/sugerir-sustitutos/")
def sugerir_sustitutos(p: PeticionSustitutos):
    modelo = os.environ.get("OLLAMA_MODELO_GENERACION", "").strip()
    if not modelo:
        raise HTTPException(status_code=503, detail="No hay modelo de generacion configurado (OLLAMA_MODELO_GENERACION).")
    inicio = time.perf_counter()
    try:
        r = ollama.chat(model=modelo, messages=armar_mensajes(p), format="json",
                        options={"temperature": 0, "num_predict": 400})
    except Exception as e:  # Ollama caido o modelo no instalado
        raise HTTPException(status_code=503, detail=f"Ollama no disponible: {e}")
    latencia = (time.perf_counter() - inicio) * 1000
    mensaje = r["message"] if isinstance(r, dict) else r.message
    contenido = mensaje["content"] if isinstance(mensaje, dict) else mensaje.content
    return {"modelo": modelo, "respuesta": contenido, "latencia_ms": latencia}


def respeta_conjunto(respuesta: str, candidatos: list[str]) -> tuple[bool, list[str]]:
    """Utilidad para la medicion: True si la respuesta es JSON valido y todos los ids elegidos estan en candidatos."""
    try:
        datos = json.loads(respuesta)
        elegidos = [str(e["id"]).strip().lower() for e in datos.get("elegidos", [])]
    except (ValueError, TypeError, KeyError, AttributeError):
        return False, []
    permitidos = {c.strip().lower() for c in candidatos}
    return all(e in permitidos for e in elegidos), elegidos

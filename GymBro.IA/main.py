"""Microservicio de IA de GymBro (FastAPI + Ollama local).

Etapa 9b-1: /generar-embedding/ recibe el texto en el cuerpo JSON ({"texto": "..."}); la forma anterior con el texto en
la cadena de consulta (?texto=...) se conserva por compatibilidad con otros llamadores.
"""
import os
from typing import Optional

import ollama
from fastapi import Body, FastAPI, HTTPException
from pydantic import BaseModel

from sustitutos import router as router_sustitutos

app = FastAPI(title="GymBro AI Microservice")
# Etapa 9b-2: sustitutos de ejercicios con el modelo de lenguaje local (OLLAMA_MODELO_GENERACION).
app.include_router(router_sustitutos)

MODELO_EMBEDDINGS = os.environ.get("OLLAMA_MODELO_EMBEDDINGS", "nomic-embed-text")


class PeticionEmbedding(BaseModel):
    texto: str


@app.get("/")
def health_check():
    return {"status": "El microservicio de IA de GymBro esta corriendo exitosamente"}


@app.post("/generar-embedding/")
def generar_embedding(texto: Optional[str] = None, cuerpo: Optional[PeticionEmbedding] = Body(default=None)):
    """
    Recibe un texto (por ejemplo, la descripcion de un ejercicio o la consulta de un usuario) y devuelve su vector con el
    modelo de embeddings local de Ollama (por defecto nomic-embed-text, 768 dimensiones).
    """
    contenido = cuerpo.texto if cuerpo is not None else texto
    if contenido is None or not contenido.strip():
        raise HTTPException(status_code=400, detail="Falta el texto (cuerpo JSON {\"texto\": \"...\"}).")
    try:
        respuesta = ollama.embeddings(model=MODELO_EMBEDDINGS, prompt=contenido)
        return {
            "texto_original": contenido,
            "dimensiones": len(respuesta["embedding"]),
            "embedding": respuesta["embedding"],
        }
    except Exception as e:  # Ollama caido o modelo no instalado
        raise HTTPException(status_code=503, detail=f"Ollama no disponible: {e}")

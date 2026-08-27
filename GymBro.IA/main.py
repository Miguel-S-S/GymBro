from fastapi import FastAPI
import ollama

app = FastAPI(title="GymBro AI Microservice")

@app.get("/")
def health_check():
    return {"status": "El microservicio de IA de GymBro esta corriendo exitosamente"}

@app.post("/generar-embedding/")
def generar_embedding(texto: str):
    """
    recibe un texto por ejemplo la descripcion de un ejercicio y devuelve
    su representacion vectorial matematica usando el modelo phi3 local
    """
    try:
        #llamada para ollama en puerto 11434
        respuesta = ollama.embeddings(model='nomic-embed-text', prompt=texto)

        return {
            "texto_original": texto,
            "dimensiones": len(respuesta["embedding"]),
            "embedding": respuesta["embedding"]
        }
    except Exception as e:
        return {"error": str(e)}
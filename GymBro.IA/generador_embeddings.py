import os
import sys

import psycopg2
import requests

# 1. Configuración de conexión desde variables de entorno (Etapa 8c, T1).
#    Obligatoria: POSTGRES_PASSWORD (la misma del archivo .env que usa docker-compose; ver docs/configuracion-secretos.md).
#    Opcionales, con los valores no secretos del entorno local: POSTGRES_HOST, POSTGRES_PORT, POSTGRES_DB, POSTGRES_USER.
VARIABLES_OBLIGATORIAS = ["POSTGRES_PASSWORD"]


def leer_config_db():
    faltantes = [v for v in VARIABLES_OBLIGATORIAS if not os.environ.get(v)]
    if faltantes:
        sys.exit(
            "Faltan variables de entorno: " + ", ".join(faltantes) + ". "
            "Definilas antes de ejecutar el script (ver docs/configuracion-secretos.md)."
        )
    return {
        "dbname": os.environ.get("POSTGRES_DB", "gymbro_db"),
        "user": os.environ.get("POSTGRES_USER", "postgres"),
        "password": os.environ["POSTGRES_PASSWORD"],
        "host": os.environ.get("POSTGRES_HOST", "localhost"),
        "port": os.environ.get("POSTGRES_PORT", "5433"),
    }


OLLAMA_URL = os.environ.get("OLLAMA_URL", "http://localhost:11434/api/embeddings")
MODEL = "nomic-embed-text"

def generar_embeddings():
    db_config = leer_config_db()
    try:
        conn = psycopg2.connect(**db_config)
        cur = conn.cursor()

        # Seleccionamos solo los ejercicios que no tienen vector
        cur.execute("""
            SELECT id, nombre, zona_objetivo, musculo_principal, ejecucion 
            FROM ejercicios 
            WHERE embedding IS NULL;
        """)
        ejercicios = cur.fetchall()

        print(f"Total de ejercicios a procesar: {len(ejercicios)}")

        for ej in ejercicios:
            id_ejercicio = ej[0]
            nombre = ej[1] or ""
            zona = ej[2] or ""
            musculo = ej[3] or ""
            ejecucion = ej[4] or ""

            # 2. La magia del RAG: Construimos el contexto semántico rico
            texto_semantico = f"Ejercicio: {nombre}. Zona objetivo: {zona}. Músculo principal: {musculo}. Técnica y ejecución: {ejecucion}"

            # 3. Petición al Microservicio de IA local (Ollama)
            payload = {
                "model": MODEL,
                "prompt": texto_semantico
            }
            
            respuesta = requests.post(OLLAMA_URL, json=payload)
            
            if respuesta.status_code == 200:
                vector = respuesta.json().get("embedding")
                
                # 4. Inserción en pgvector (formato string de arreglo)
                vector_str = f"[{','.join(map(str, vector))}]"
                
                cur.execute(
                    "UPDATE ejercicios SET embedding = %s WHERE id = %s;",
                    (vector_str, id_ejercicio)
                )
                print(f"[OK] Vectorizado: {nombre}")
            else:
                print(f"[ERROR] Falló Ollama para {nombre}: {respuesta.text}")

        # Confirmamos la transacción
        conn.commit()
        cur.close()
        conn.close()
        print("\nProceso de vectorización completado con éxito.")

    except Exception as e:
        print(f"Ocurrió un error en la base de datos: {e}")

if __name__ == "__main__":
    generar_embeddings()
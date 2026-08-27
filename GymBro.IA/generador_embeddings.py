import psycopg2
import requests
import json

# 1. Configuración de Conexión (Ajusta tu contraseña)
DB_CONFIG = {
    "dbname": "gymbro_db",
    "user": "postgres",
    "password": "admin", 
    "host": "localhost",
    "port": "5433"
}

OLLAMA_URL = "http://localhost:11434/api/embeddings"
MODEL = "nomic-embed-text"

def generar_embeddings():
    try:
        conn = psycopg2.connect(**DB_CONFIG)
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
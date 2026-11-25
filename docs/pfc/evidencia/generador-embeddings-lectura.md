# Lectura de `GymBro.IA/generador_embeddings.py` (solo lectura)

**Fecha:** 19/11/2026 (consulta a la base: 2026-11-20 01:17 UTC, 22:17 hora local del 19/11).
**Repositorio:** rama `etapa9b1`, en `c29890f` (sin cambios en el código; este archivo es el único que se agregó).
**Alcance:** lectura del código y una consulta `BEGIN READ ONLY` a la base local `gymbro-db`. El script **no** se ejecutó.
**Convención:** **[Verificado en el código]** = comprobado leyendo el archivo y la línea citados. **[Verificado en la base]** = salida de una consulta de solo lectura. **[Supuesto]** = no comprobado; se indica por qué.

## 1. Resumen

1. El script lee de `ejercicios` solo las filas con `embedding IS NULL` (línea 42), sin filtrar por baja, estado, origen ni zona.
2. Arma un texto con etiquetas en español y los valores de la base: `Ejercicio`, `Zona objetivo`, `Músculo principal` y `Técnica y ejecución` (línea 56).
3. Llama **directo a Ollama** (`/api/embeddings`, modelo `nomic-embed-text`; líneas 29, 30 y 64). No pasa por `main.py`.
4. Guarda cada vector con un `UPDATE` por fila (líneas 72 a 75) y hace **un solo `commit` al final** (línea 81).
5. Es re-ejecutable: solo toca filas sin vector y no inserta, así que no duplica.
6. No tiene tiempo máximo en la llamada a Ollama (línea 64), ni reintentos, ni valida la dimensión del vector.
7. Cualquier excepción descarta todo el lote y se informa con un mensaje engañoso: "error en la base de datos" (línea 87).
8. La aplicación .NET arma **el mismo texto** (`EjercicioTextoSemantico.cs`, línea 12), fijado por una prueba unitaria, pero llama a Ollama a través de `main.py`.
9. En la base local, los 696 ejercicios globales vigentes tienen vector (Dataset 688, Proyecto 8). Falta 1 ejercicio propio de gimnasio.
10. El catálogo Dataset está en inglés y el de Proyecto en español: el mismo modelo vectoriza textos en dos idiomas (relevante para la 9b-1).

## 2. Flujo paso a paso

1. **Configuración** [Verificado en el código]. `leer_config_db()` (líneas 13 a 26):
   - Exige `POSTGRES_PASSWORD`; si falta, termina con `sys.exit` y nombra la variable (líneas 10 y 14 a 19).
   - Usa estos valores por defecto: `POSTGRES_DB=gymbro_db`, `POSTGRES_USER=postgres`, `POSTGRES_HOST=localhost`, `POSTGRES_PORT=5433` (líneas 21 a 25).
   - Ollama: `OLLAMA_URL`, por defecto `http://localhost:11434/api/embeddings` (línea 29). Modelo fijo: `MODEL = "nomic-embed-text"` (línea 30).
2. **Conexión** [Verificado en el código]: `psycopg2.connect(**db_config)` y un cursor (líneas 35 y 36). psycopg2 abre una transacción implícita que dura hasta el `commit`.
3. **Lectura** [Verificado en el código]: `SELECT id, nombre, zona_objetivo, musculo_principal, ejecucion FROM ejercicios WHERE embedding IS NULL` (líneas 39 a 43). Trae todas las filas de una vez (`fetchall`, línea 44) e imprime cuántas son (línea 46).
4. **Composición del texto** [Verificado en el código]: un `NULL` se reemplaza por cadena vacía (líneas 50 a 53). Línea 56, copiada textual:
   ```python
   texto_semantico = f"Ejercicio: {nombre}. Zona objetivo: {zona}. Músculo principal: {musculo}. Técnica y ejecución: {ejecucion}"
   ```
5. **Embedding** [Verificado en el código]: `requests.post(OLLAMA_URL, json={"model": MODEL, "prompt": texto_semantico})` (líneas 59 a 64). Es una llamada directa a la API de Ollama, no al endpoint `/generar-embedding/` de `main.py`.
6. **Escritura** [Verificado en el código]:
   - Si la respuesta es 200, convierte la lista al formato texto de pgvector `[v1,v2,...]` (línea 70) y ejecuta `UPDATE ejercicios SET embedding = %s WHERE id = %s` (líneas 72 a 75).
   - Si no es 200, imprime `[ERROR] Falló Ollama para {nombre}` y sigue con la próxima fila (líneas 77 y 78).
7. **Cierre** [Verificado en el código]: un único `conn.commit()` después del bucle (línea 81), y luego cierre del cursor y de la conexión (líneas 82 y 83).
8. **Error** [Verificado en el código]: ante cualquier excepción, imprime `Ocurrió un error en la base de datos: {e}` (líneas 86 y 87). No hace `commit` ni cierra la conexión, y no termina con un código de salida distinto de 0.

**Dimensión** [Verificado en el código y en evidencia]: la columna es `vector(768)` (`GymBroDbContext.cs`, línea 73). La evidencia `docs/pfc/evidencia/etapa9a1-embeddings.txt` muestra `dims = 768` en los 8 ejercicios de Core (líneas 22 a 31). El script no fija ni comprueba la dimensión.

## 3. Idioma de cada columna usada

[Verificado en la base]: muestra de 4 filas por origen y conteo heurístico de palabras frecuentes, sobre los ejercicios globales vigentes.

| Columna | Dataset (688) | Proyecto (8) |
|---|---|---|
| Etiquetas del texto (`Ejercicio:`, `Zona objetivo:`, ...) | Español (fijas en el código, línea 56) | Español |
| `nombre` | Inglés (p. ej. "Front Raise", "Bench Dip") | Español, a veces con el inglés entre paréntesis o solo en inglés (p. ej. "Bicicleta (cross bicycles)", "Mountain climber") |
| `zona_objetivo` | Inglés (`Legs`, `Back`, `Chest`, `Shoulders`, `Arms`, `Forearms`), salvo `Movilidad` (91 ejercicios), en español | `Core` |
| `musculo_principal` | Inglés (p. ej. "Shoulder", "Upper Arms") | Español (p. ej. "Recto abdominal", "Oblicuos") |
| `ejecucion` | Inglés: 540 de 688 contienen "the", "and", "with" o "your"; 6 contienen palabras frecuentes del español (heurística, no revisadas una por una) | Español: 8 de 8 |

**[Supuesto]:** la clasificación por idioma de todo el Dataset sale de una muestra y de una heurística por palabras, no de una revisión fila por fila.

## 4. Criterio de selección e idempotencia

- **Selección** [Verificado en el código]: solo `embedding IS NULL` (línea 42). **No** filtra por `fecha_baja`, `activo`, `origen`, `gimnasio_id` ni zona: también vectoriza los ejercicios dados de baja o desactivados y los propios de cualquier gimnasio que no tengan vector.
- **Idempotencia** [Verificado en el código]:
  - Una fila que ya tiene vector no se vuelve a leer, y el script solo hace `UPDATE` sobre filas existentes, así que no duplica.
  - Re-ejecutarlo procesa solo lo que quedó pendiente.
  - No recalcula vectores viejos: si cambia el texto de un ejercicio que ya tiene vector, el script no lo detecta. La aplicación .NET y el script `admin-ejercicio-global.sql` resuelven esto dejando `embedding` en NULL cuando cambia el texto (`EjercicioGimnasioService.cs`, líneas 93 a 98; `admin-ejercicio-global.sql`, líneas 11 y 100).
- **Corrupción:**
  - [Verificado en el código] El vector se escribe completo o no se escribe.
  - [Supuesto, comportamiento de pgvector] Un vector de dimensión distinta de 768 lo rechaza la base al ejecutar el `UPDATE`; eso dispara la excepción del paso 8 y se pierde el lote entero.

## 5. Escritura, transacciones, errores, reintentos y tiempos

| Aspecto | Comportamiento | Fuente |
|---|---|---|
| Escritura | Un `UPDATE` por fila, dentro de una sola transacción | Líneas 72 a 75 y 81 [Verificado en el código] |
| Commit | Uno solo, al final del bucle | Línea 81 [Verificado en el código] |
| Respuesta distinta de 200 | Imprime el error, omite la fila y continúa; la fila queda con NULL | Líneas 77 y 78 [Verificado en el código] |
| Respuesta 200 sin `embedding` | `vector` queda en `None`, `','.join(map(str, None))` lanza `TypeError` y se pierde el lote, con el mensaje "error en la base de datos" | Líneas 67, 70, 86 y 87 [Verificado en el código; el `TypeError` es el comportamiento estándar de Python] |
| Excepción (red, base, dimensión) | No hay commit: se pierden **todos** los `UPDATE` de esa ejecución. La conexión no se cierra explícitamente (no hay `finally`) | Líneas 34 y 86 a 87 [Verificado en el código] |
| Reintentos | No tiene | [Verificado en el código] |
| Tiempo máximo | `requests.post` sin `timeout`: puede esperar indefinidamente si Ollama no responde | Línea 64 [Verificado en el código] |
| Código de salida | 0 aunque haya errores; solo sale con error si falta `POSTGRES_PASSWORD` | Líneas 16 y 86 a 87 [Verificado en el código] |

## 6. Configuración y ejecución

- **Variables de entorno** [Verificado en el código y en `docs/configuracion-secretos.md`, línea 46]:
  - `POSTGRES_PASSWORD` (obligatoria).
  - `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER` y `OLLAMA_URL` (opcionales).
- **Ollama:** `http://localhost:11434/api/embeddings` por defecto (línea 29), con el modelo `nomic-embed-text` instalado en Ollama.
  - [Supuesto] Debe estar descargado con `ollama pull nomic-embed-text`. No se verificó en este equipo durante esta lectura. La evidencia de la 9a-1 muestra que en esa fecha respondía (líneas 40 a 50).
- **Dependencias** [Verificado en `GymBro.IA/requirements.txt`, líneas 6 a 8]: `psycopg2-binary==2.9.12` y `requests==2.34.2`.
- **Comando** [Supuesto: no está escrito como comando en el repositorio; se infiere de la línea 89 y de la evidencia de la 9a-1]: con el entorno virtual de `GymBro.IA` activo y `POSTGRES_PASSWORD` definida solo para esa terminal, ejecutar `python generador_embeddings.py` desde `GymBro.IA`.
- **`.env` de ejemplo:** no se encontró un `.env.example` en la raíz ni en `GymBro.IA` (búsqueda por nombre). La configuración está documentada en `docs/configuracion-secretos.md`.

## 7. Comparación con la aplicación .NET

Camino de la aplicación [Verificado en el código]:
- `EjercicioGimnasioService.GenerarEmbeddingAsync` (líneas 229 a 242) arma el texto con `EjercicioTextoSemantico.Componer` (línea 231).
- Lo envía a `IaService`, que llama a `main.py` (`/generar-embedding/`), y `main.py` llama a Ollama con la librería `ollama`.
- Se usa al crear (línea 78) y al editar, si cambia el texto (línea 98), un ejercicio propio de gimnasio.

| Aspecto | `generador_embeddings.py` | Aplicación .NET | ¿Igual? |
|---|---|---|---|
| Plantilla del texto | Línea 56 | `EjercicioTextoSemantico.cs`, línea 12: `$"Ejercicio: {nombre}. Zona objetivo: {zona}. Músculo principal: {musculo}. Técnica y ejecución: {ejecucion}"` | **Sí**, carácter por carácter. La prueba `El_texto_semantico_tiene_el_formato_del_generador_de_python` (`GymBro.Tests/CatalogoEquipamientoTests.cs`, líneas 107 a 115) fija el literal del lado de C#; **no hay prueba del lado de Python** |
| Campo `nombre` | Valor de la base, NULL pasa a "" | `e.Nombre` de la entidad, ya validado por el servicio | Mismo campo. [Supuesto] Si la validación recorta espacios, una fila cargada por otra vía podría diferir en blancos |
| Campo `zona` | `zona_objetivo` | `e.ZonaObjetivo` | Sí |
| Campo `musculo` | `musculo_principal` | `e.MusculoPrincipal` | Sí |
| Campo `ejecucion` | `ejecucion` | `e.Ejecucion` | Sí |
| Llamada al modelo | Directa: `POST {OLLAMA_URL}` con JSON (línea 64) | `IaService.cs`, línea 18: `POST /generar-embedding/?texto=...` (texto en la URL) hacia `main.py` (`Ia:UrlBase`, por defecto `http://127.0.0.1:8000`, `Program.cs`, línea 24); `main.py`, línea 18: `ollama.embeddings(model='nomic-embed-text', prompt=texto)` | Mismo modelo y mismo campo `prompt`. [Supuesto] La librería `ollama` 0.6.2 usa el mismo endpoint `/api/embeddings`; no se comprobó en el código de la librería |
| Tiempo máximo | Ninguno | 5 s (`EmbeddingConfig`, línea 19; `WaitAsync`, línea 235) | No |
| Validación de dimensión | Ninguna (la base rechaza) | Exige 768; si no, NULL (línea 236) | No |
| Ante un fallo | Omite la fila, o pierde el lote si hay excepción | Guarda el ejercicio con `embedding = NULL` y la auditoría dice "Embedding pendiente" (líneas 82, 83 y 238 a 241) | No (son complementarios: el script completa lo que la aplicación deja en NULL) |
| Error de Ollama en `main.py` | No aplica | `main.py` responde **200** con `{"error": ...}` (líneas 25 y 26); `IaService` devuelve un vector vacío y el servicio lo guarda como NULL | No aplica |

No se unificó nada.

## 8. Pruebas y evidencia de funcionamiento

- **Prueba automática del script de Python:** no hay. [Verificado: no hay pruebas en `GymBro.IA`; búsqueda por nombre en el repositorio.]
- **Prueba de equivalencia del texto:** solo del lado de C# (sección 7).
- **Evidencia de ejecución:** `docs/pfc/evidencia/etapa9a1-embeddings.txt`.
  - El autor lo ejecutó el 17/11/2026: 8 solicitudes con respuesta 200 registradas por Ollama (líneas 42 a 49).
  - Resultado: 0 ejercicios sin vector en Dataset y Proyecto (líneas 16 a 20), dimensión 768.
- **Consulta pedida** [Verificado en la base; `BEGIN READ ONLY`, sin datos personales]:

  ```
  SELECT origen, count(*) total, count(*) FILTER (WHERE embedding IS NOT NULL) con_embedding,
         count(*) FILTER (WHERE embedding IS NULL) sin_embedding
  FROM ejercicios WHERE fecha_baja IS NULL GROUP BY origen;

    origen  | total | con_embedding | sin_embedding
  ----------+-------+---------------+---------------
   Gimnasio |     1 |             0 |             1
   Proyecto |     8 |             8 |             0
   Dataset  |   688 |           688 |             0
  ```

  La columna `origen` existe en la base local. Los 688 de Dataset incluyen 1 ejercicio desactivado (`activo = false`), según el conteo guardado en `docs/pfc/evidencia/etapa9b1-embeddings-conteo.txt`. El que falta es un ejercicio propio de gimnasio dado de alta el 19/11/2026: la aplicación lo guardó sin vector, lo que indica que el servicio de IA no respondió en ese momento [Supuesto: no se revisaron los registros de la API].

## 9. Riesgos e inconsistencias detectadas

1. **Llamada sin tiempo máximo** (línea 64): un Ollama colgado deja el script esperando indefinidamente.
2. **Todo o nada con un mensaje engañoso** (líneas 81, 86 y 87): una sola excepción pierde todos los vectores ya calculados y siempre se informa como "error en la base de datos". El código de salida es 0.
3. **Respuesta 200 sin `embedding`** (líneas 67 y 70): termina en `TypeError` y pierde el lote; no se trata como una fila fallida.
4. **Fallos silenciosos por fila** (líneas 77 y 78): una fila con respuesta distinta de 200 queda en NULL y el final dice "completado con éxito" (línea 84).
5. **Selección amplia** (línea 42): vectoriza ejercicios dados de baja y desactivados. No rompe nada, pero gasta llamadas.
6. **Dos copias del texto** (Python, línea 56, y C#, línea 12): solo la de C# está fijada por una prueba. Un cambio en Python no lo detecta ninguna prueba.
7. **Dos caminos al modelo:** Python va directo a Ollama; .NET pasa por `main.py`. Si cambia el modelo o sus parámetros en uno solo, los vectores dejan de ser comparables.
8. **`main.py` responde 200 ante un error** (líneas 25 y 26), y además recibe el texto en la URL (`IaService.cs`, línea 18).
9. **Catálogo en dos idiomas** (sección 3): consultas en español contra contenido en su mayoría en inglés. Es lo que la 9b-1 propone medir (variantes V1 y V2).
10. **Prefijos de tarea de `nomic-embed-text`** [Supuesto, no verificado en la ficha oficial en esta lectura]: hasta donde se sabe, el modelo espera `search_document:` en los documentos y `search_query:` en las consultas. Ni el script ni la aplicación los usan. Hay que verificarlo en la ficha oficial antes de afirmarlo en el informe.

## 10. Lo que no se pudo verificar

- Que Ollama y el modelo estén instalados y respondan hoy en este equipo: no se llamó a Ollama.
- El endpoint que usa internamente la librería `ollama` 0.6.2: no se leyó su código.
- El idioma de cada fila del Dataset: se usó una muestra y una heurística.
- Si el modelo es multilingüe y si requiere prefijos: falta consultar la ficha oficial.
- El comando exacto con el que el autor ejecuta el script: no está documentado en el repositorio.
- Por qué el ejercicio propio quedó sin vector: no se revisaron los registros de la API.

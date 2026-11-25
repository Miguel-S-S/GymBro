# Etapa 9b-1 — Recuperación medible (filtros y evaluación)

Rama `cierre-pfc`. Commit de código: `c04bb14`. Evidencia: `docs/pfc/evidencia/etapa9b1-*.txt`. Sin migración.

> **PUNTO DE ESPERA 1.** El banco `docs/pfc/evaluacion/banco-consultas.csv` tiene 34 consultas candidatas con la columna `ids_relevantes` **vacía**. Las etiquetas las ponen los autores y la Personal Trainer. **Este informe no contiene ninguna cifra de recall ni de MRR**; las secciones de resultados se completan cuando existan las etiquetas y una ejecución guardada del script de evaluación.

## Alcance implementado

| Pedido | Estado | Dónde |
|---|---|---|
| Filtro duro: ejercicios activos (y sin baja) | Implementado y verificado | `GymBro.Infrastructure/Services/BuscadorSemanticoService.cs` |
| Filtro duro: equipamiento disponible en el gimnasio (`EquipamientoGimnasio`) | Implementado y verificado | idem (misma regla que el planificador de piernas) |
| Filtro duro: nunca ejercicios de otro gimnasio | Implementado y verificado | idem (también para el Administrador de la Plataforma) |
| Zona opcional (y claves de equipamiento opcionales) | Implementado y verificado | `BusquedaSemanticaRequest` |
| Consulta por el cuerpo (`POST`) en la API | Implementado y verificado | `POST /api/Ejercicios/busqueda-inteligente` |
| Consulta por el cuerpo en `GymBro.IA/main.py`, con compatibilidad | Implementado y verificado (pruebas con Ollama simulado) | `main.py`, `IaService.cs`, `GymBro.IA/test_main.py` |
| Compatibilidad con los llamadores existentes | Implementado y verificado | `GET /api/Ejercicios/busqueda-inteligente` se conserva y aplica los mismos filtros |
| Script de evaluación recall@1/3/5 y MRR, español contra inglés, con salida guardada | Implementado; métricas verificadas con valores conocidos; **sin ejecutar sobre el banco (no hay etiquetas)** | `GymBro.IA/evaluacion/evaluar_recuperacion.py`, `metricas.py`, `test_metricas.py` |
| Script de prueba de hardware de Ollama | Implementado; **no ejecutado** (Ollama no estaba en ejecución en el equipo durante la etapa) | `GymBro.IA/evaluacion/prueba_hardware_ollama.py` |
| Banco con al menos 30 consultas candidatas y etiquetas vacías | Implementado | `docs/pfc/evaluacion/banco-consultas.csv` (34) |

## Texto propuesto para 4.5 (implementación)

La búsqueda semántica de ejercicios convierte la consulta en un vector con el modelo de embeddings local (`nomic-embed-text` en Ollama, a través de `GymBro.IA`) y ordena los ejercicios por distancia coseno con pgvector. Hasta esta etapa el único filtro era que el ejercicio tuviera vector. Se agregaron filtros duros que se aplican en la consulta **antes** de ordenar, de modo que un ejercicio que no los cumple nunca se devuelve aunque sea el más parecido: el ejercicio debe estar activo y sin baja lógica; debe ser global o propio del gimnasio de referencia, nunca de otro gimnasio; y, si el gimnasio declaró su equipamiento, el tipo de equipamiento del ejercicio debe estar marcado por el gimnasio o ser "siempre disponible" (por ejemplo, peso corporal), que es la misma regla que ya usa el planificador de piernas. Si el gimnasio no declaró equipamiento no se restringe por ese motivo. El gimnasio de referencia es el de la sesión para el Dueño y el Personal Trainer (indicar otro responde 404), el de la ficha para el Socio, y el indicado para el Administrador de la Plataforma (sin indicarlo, solo ejercicios globales y sin filtro de equipamiento). Opcionalmente, el pedido restringe por zona y por claves de tipo de equipamiento, que nunca amplían lo disponible.

La consulta viaja ahora en el cuerpo de un `POST` tanto hacia la API como hacia el microservicio de IA, para que el texto del usuario no quede en la URL (y, con ella, en los registros de servidores y proxies). Las formas anteriores con el texto en la URL se conservan por compatibilidad. Si el servicio de IA u Ollama no responden, la API devuelve 503 con un mensaje propio, sin efectos.

Para medir la recuperación se preparó un banco de consultas candidatas en español y en inglés con su zona y equipamiento, y un script que consulta la API real (embedding, filtros y orden) y calcula recall@1, recall@3, recall@5 y MRR por idioma. Las etiquetas de relevancia las asignan los autores y la Personal Trainer: el script se niega a calcular métricas si ninguna consulta está etiquetada. Un segundo script mide, en el equipo donde corre Ollama, la latencia de cada modelo instalado (embeddings y generación), insumo para elegir el modelo de la 9b-2.

## Decisiones técnicas

- **D-9b1-1. Filtros en la base, antes del orden.** Filtrar después de traer los k más parecidos podría devolver menos de k resultados o ninguno; filtrar antes garantiza k válidos si existen.
- **D-9b1-2. Sin equipamiento declarado no se filtra por equipamiento.** Es la misma decisión que el planificador de piernas (9a-2 y 7d); un gimnasio recién creado no queda sin resultados.
- **D-9b1-3. Plataforma sin gimnasio: solo globales.** El Administrador de la Plataforma ve todos los ejercicios en el catálogo, pero en la búsqueda nunca se mezclan ejercicios propios de distintos gimnasios.
- **D-9b1-4. `POST` sin resultados responde 200 con una lista vacía**; el `GET` anterior conserva su 404 por compatibilidad.
- **D-9b1-5. Evaluación contra la API real** (y no reimplementando los filtros en Python), para medir el sistema que se usa. Requiere un JWT en una variable de entorno; el script no guarda el token.
- **D-9b1-6. Pruebas de Python con `unittest`** (biblioteca estándar) para no agregar dependencias a `GymBro.IA`.
- **D-9b1-7. Excepción nueva `ServicioExternoNoDisponibleException` (503).** La búsqueda antes respondía 500 genérico si la IA no estaba disponible.

## Texto propuesto para 4.6 (verificación)

Pruebas de integración nuevas (`GymBro.Tests.Integration/BusquedaSemanticaTests.cs`, contra PostgreSQL + pgvector descartable con un servicio de IA simulado que devuelve siempre el mismo vector): tres "intrusos" a distancia 0 de la consulta (un ejercicio inactivo, uno propio de otro gimnasio y uno con barra, que el gimnasio no tiene) y cuatro válidos algo más lejos. Se verifica que el Dueño, el Personal Trainer, el Socio y la plataforma con el gimnasio indicado nunca reciben a los intrusos y que el primer resultado es el válido más cercano; que la plataforma sin gimnasio solo recibe globales (y, sin equipamiento declarado de referencia, también el de barra); que la zona y las claves de equipamiento restringen; que el `GET` anterior responde y aplica los mismos filtros; que los pedidos inválidos responden 400 sin llamar a la IA; que el Dueño que indica otro gimnasio recibe 404; y que con la IA caída se responde 503 con mensaje claro.

Pruebas de Python (`GymBro.IA/test_main.py`, 4, y `GymBro.IA/evaluacion/test_metricas.py`, 6): el microservicio acepta el texto en el cuerpo y en la cadena de consulta, responde 400 sin texto y 503 con Ollama caído (Ollama reemplazado por un doble); recall@k y MRR con valores conocidos; sin etiquetas no hay métricas; el banco tiene al menos 30 consultas y sus etiquetas, si existen, son GUID.

## Cifras de la verificación

Sobre el commit `c04bb14` (`etapa9b1-*.txt`):

| Verificación | Etapa anterior (7e) | Etapa 9b-1 |
|---|---|---|
| `dotnet build` | 0 errores, advertencia previa CS2008 | igual |
| Pruebas unitarias | 370 | 370 |
| Pruebas de integración | 543 | 558 (+15: búsqueda semántica y celdas nuevas de la matriz) |
| `ng build` / pruebas del cliente | correcto / 250 | correcto / 250 (sin cambios en el cliente) |
| Pruebas de Python | — | 10 (4 del microservicio y 6 de métricas y banco) |
| `evaluar_recuperacion.py` sobre el banco | — | termina con código 3 sin calcular métricas: 34 consultas, 0 etiquetadas |

## Desvíos y limitaciones

- La prueba de hardware de Ollama **no se ejecutó**: el servicio de Ollama no estaba en ejecución en el equipo durante la etapa y no se lo inició desde Claude Code. Cómo correrla: iniciar Ollama y ejecutar `GymBro.IA/venv/Scripts/python GymBro.IA/evaluacion/prueba_hardware_ollama.py`; la salida queda en `docs/pfc/evaluacion/resultados/`.
- El `GET` anterior ahora también rechaza `cantidad` fuera de 1 a 50 con 400 (antes no tenía límite).
- El entorno virtual de `GymBro.IA` usa Python 3.14.6 (la guía del proyecto menciona 3.11): las pruebas corrieron con 3.14.6.

## Pendientes

- **PUNTO DE ESPERA 1:** etiquetar `ids_relevantes` (ids de ejercicios separados por `;`) con los autores y la Personal Trainer; luego ejecutar `evaluar_recuperacion.py` con la API, la IA y Ollama en marcha y completar este informe con la salida guardada.
- Ejecutar la prueba de hardware de Ollama en el equipo del autor (insumo del PUNTO DE ESPERA 2).

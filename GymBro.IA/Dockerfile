# GymBro.IA - microservicio FastAPI (Etapa 13). Contexto: GymBro.IA/
#   docker build -t gymbro-ia GymBro.IA
# Ollama corre aparte (perfil "ollama" de docker-compose.full.yml o en el equipo): OLLAMA_HOST indica donde.
FROM python:3.14-slim
WORKDIR /app
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 OLLAMA_HOST=http://ollama:11434
COPY requirements-servicio.txt .
RUN pip install --no-cache-dir -r requirements-servicio.txt
COPY main.py sustitutos.py ./
RUN useradd --create-home --uid 10001 ia
USER ia
EXPOSE 8000
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8000/', timeout=4)"
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]

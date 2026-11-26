# GymBro.Analytics - aplicacion Streamlit y scripts del modelo de abandono (Etapa 13). Contexto: GymBro.Analytics/
#   docker build -t gymbro-analitica GymBro.Analytics
# Lee la base con el rol de solo lectura (docs/pfc/scripts/rol-analitica.sql); credenciales por variables de entorno.
FROM python:3.14-slim
WORKDIR /app
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY gymbro_analytics/ gymbro_analytics/
COPY app/ app/
COPY entrenar_abandono.py puntuar_abandono.py ./
RUN useradd --create-home --uid 10001 analitica && mkdir -p /app/modelos && chown analitica /app/modelos
USER analitica
EXPOSE 8501
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8501/_stcore/health', timeout=4)"
CMD ["streamlit", "run", "app/principal.py", "--server.address=0.0.0.0", "--server.port=8501", "--server.headless=true", "--browser.gatherUsageStats=false"]

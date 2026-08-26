import pandas as pd
from deep_translator import GoogleTranslator

def traducir_texto(texto):
    if pd.isna(texto) or not isinstance(texto, str):
        return texto
    try:
        return GoogleTranslator(source='en', target='es').translate(texto)
    except Exception as e:
        print(f"Error al traducir '{texto}': {e}")
        return texto

# 1. Leer el dataset original
print("Leyendo el archivo CSV original...")
df = pd.read_csv(r"C:\Users\miguel\Downloads\dataset gym\archive\gym_exercise_dataset.csv")

# Tomamos una muestra de los primeros 100 ejercicios para la prueba
df_muestra = df.head(100).copy()

# 2. Traducir las columnas importantes
print("Traduciendo nombres de ejercicios...")
df_muestra['Exercise Name'] = df_muestra['Exercise Name'].apply(traducir_texto)

print("Traduciendo músculos principales...")
df_muestra['Main_muscle'] = df_muestra['Main_muscle'].apply(traducir_texto)

print("Traduciendo equipamiento...")
df_muestra['Equipment'] = df_muestra['Equipment'].apply(traducir_texto)

# (Opcional) Puedes descomentar estas líneas si también quieres traducir las instrucciones, 
# pero tomará más tiempo de procesamiento:
# df_muestra['Preparation'] = df_muestra['Preparation'].apply(traducir_texto)
# df_muestra['Execution'] = df_muestra['Execution'].apply(traducir_texto)

# 3. Guardar el nuevo dataset
nuevo_nombre = "gym_ejercicios_es.csv"
df_muestra.to_csv(nuevo_nombre, index=False, encoding='utf-8')
print(f"¡Traducción completada! Archivo guardado como: {nuevo_nombre}")
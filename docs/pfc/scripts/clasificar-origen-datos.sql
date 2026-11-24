-- =====================================================================
-- GymBro - Etapa 7d - Clasificación de socios por origen de datos
-- EJECUTAR DESPUÉS de aplicar la migración Etapa7d_ProtocoloPiernas.
-- La migración deja TODOS los socios existentes en 'DatasetExterno' (valor conservador:
-- queda fuera de los KPIs del estudio, que incluyen solo 'Sintetico').
-- Este script corrige solo a los socios que NO son del dataset de Kaggle. Solo UPDATE.
-- Ejecutar con:  psql -v ON_ERROR_STOP=1 -f clasificar-origen-datos.sql
-- =====================================================================

-- PASO 1 (solo lectura): socios que NO tienen la firma del dataset. Esperado: 13 filas.
-- Firma del dataset (Paso 0b, B10): sin email, sin DNI, independiente, sin consentimiento = 734 socios.
SELECT s.id, s.gimnasio_id, s.fecha_alta, s.origen_datos,
       (s.consentimiento_datos_salud_fecha IS NOT NULL) AS con_consentimiento
FROM socios s
WHERE s.fecha_baja IS NULL
  AND NOT ((s.email IS NULL OR s.email = '') AND s.dni IS NULL AND s.gimnasio_id IS NULL
           AND s.consentimiento_datos_salud_fecha IS NULL)
ORDER BY s.fecha_alta;

-- PASO 2: tu perfil (caso de prueba funcional, FUERA de los resultados de la tesis).
-- Reemplazá el texto por tu id real. Mientras diga REEMPLAZAR_POR_TU_ID el script falla a propósito
-- (no es un UUID válido) y no cambia nada.
BEGIN;
DO $$
DECLARE v_n integer;
BEGIN
  UPDATE socios SET origen_datos = 'ValidacionFuncional'
   WHERE id = 'REEMPLAZAR_POR_TU_ID'::uuid AND fecha_baja IS NULL;
  GET DIAGNOSTICS v_n = ROW_COUNT;
  IF v_n <> 1 THEN RAISE EXCEPTION 'Se esperaba 1 fila y fueron %', v_n; END IF;
END $$;
COMMIT;

-- PASO 3 (OPCIONAL, ejecutar solo si confirmás que los otros 12 socios del PASO 1 son de prueba
-- sintéticos y no personas reales): marcarlos 'Sintetico'. Si alguno es una persona real, usá
-- 'Real' para ese id. Si dudás, no lo ejecutes: quedan en 'DatasetExterno', fuera del estudio.
-- BEGIN;
-- UPDATE socios SET origen_datos = 'Sintetico'
--  WHERE id IN ('<id1>'::uuid, '<id2>'::uuid /* ... los 12 ... */) AND fecha_baja IS NULL;
-- COMMIT;

-- VERIFICACIÓN (solo lectura)
SELECT origen_datos, count(*) FROM socios WHERE fecha_baja IS NULL GROUP BY 1 ORDER BY 2 DESC;

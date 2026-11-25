-- =====================================================================
-- GymBro - Etapa 7d - Corrección del catálogo de piernas (ejecutar UNA vez)
-- Decisiones del autor (23/11/2026): P4 (los 10 ejercicios pasan a Origen=Proyecto,
-- la Prensa pasa a ser global) y P5 (Prensa y Hack con tipo MAQUINA_DISCOS).
-- Solo UPDATE. Sin DELETE. Todo o nada: si cualquier conteo no coincide, aborta y revierte.
-- Ejecutar con:  psql -v ON_ERROR_STOP=1 -f corregir-catalogo-piernas.sql
-- Puede ejecutarse ya, antes de la migración de la 7d: no depende de ella.
-- =====================================================================
BEGIN;

-- Estado ANTES (guardar la salida en docs/pfc/evidencia/etapa7d-catalogo-antes.txt)
SELECT e.id, e.nombre, e.origen, e.gimnasio_id, te.clave AS tipo_clave
FROM ejercicios e LEFT JOIN tipos_equipamiento te ON te.id = e.tipo_equipamiento_id
WHERE e.id IN ('3203840d-5a79-4c84-9758-b40238f7c85f','1537a321-40ab-4a60-b3b8-e73cab167c28',
               'c7499d5c-48c5-4d16-a691-54a96079ca49','69e15aaf-ca0d-433b-be03-6b496450b1d2',
               '15a5f846-25f5-48a5-a570-264c2b894462','2e9d6c61-6be0-4f4a-8f1b-348869e07a65',
               '14a284c5-5159-4bdb-83f7-733154e07228','a33d764a-d3f2-437e-a527-38e877fdba0f',
               'bca285fb-60ee-40a7-bdc9-771b201365a1','608e091b-1ddb-4a05-abb7-c0c03e48eee6')
ORDER BY e.nombre;

DO $$
DECLARE
  v_discos uuid;
  v_n integer;
BEGIN
  SELECT id INTO v_discos FROM tipos_equipamiento WHERE clave = 'MAQUINA_DISCOS';
  IF v_discos IS NULL THEN
    RAISE EXCEPTION 'No existe el tipo de equipamiento MAQUINA_DISCOS';
  END IF;

  -- 1) Prensa 45°: de ejercicio propio del gimnasio a ejercicio global del proyecto.
  --    origen y gimnasio_id cambian en la MISMA sentencia (CK_Ejercicio_OrigenGimnasio).
  UPDATE ejercicios
     SET origen = 'Proyecto', gimnasio_id = NULL, tipo_equipamiento_id = v_discos
   WHERE id = '69e15aaf-ca0d-433b-be03-6b496450b1d2'
     AND origen = 'Gimnasio'
     AND gimnasio_id = 'f8667ba5-e022-45b0-8981-8b715c189e30';
  GET DIAGNOSTICS v_n = ROW_COUNT;
  IF v_n <> 1 THEN RAISE EXCEPTION 'Prensa: se esperaba 1 fila y fueron %', v_n; END IF;

  -- 2) Sentadilla Hack: a Proyecto y tipo MAQUINA_DISCOS.
  UPDATE ejercicios
     SET origen = 'Proyecto', tipo_equipamiento_id = v_discos
   WHERE id = 'c7499d5c-48c5-4d16-a691-54a96079ca49'
     AND origen = 'Dataset' AND gimnasio_id IS NULL;
  GET DIAGNOSTICS v_n = ROW_COUNT;
  IF v_n <> 1 THEN RAISE EXCEPTION 'Hack: se esperaba 1 fila y fueron %', v_n; END IF;

  -- 3) Los otros 8: solo cambia el origen.
  UPDATE ejercicios
     SET origen = 'Proyecto'
   WHERE id IN ('3203840d-5a79-4c84-9758-b40238f7c85f','1537a321-40ab-4a60-b3b8-e73cab167c28',
                '15a5f846-25f5-48a5-a570-264c2b894462','2e9d6c61-6be0-4f4a-8f1b-348869e07a65',
                '14a284c5-5159-4bdb-83f7-733154e07228','a33d764a-d3f2-437e-a527-38e877fdba0f',
                'bca285fb-60ee-40a7-bdc9-771b201365a1','608e091b-1ddb-4a05-abb7-c0c03e48eee6')
     AND origen = 'Dataset' AND gimnasio_id IS NULL;
  GET DIAGNOSTICS v_n = ROW_COUNT;
  IF v_n <> 8 THEN RAISE EXCEPTION 'Resto: se esperaban 8 filas y fueron %', v_n; END IF;
END $$;

-- Estado DESPUÉS (guardar en docs/pfc/evidencia/etapa7d-catalogo-despues.txt).
-- Esperado: 10 filas, origen=Proyecto, gimnasio_id vacío, Prensa y Hack con MAQUINA_DISCOS,
-- y embedding_ok = true en las 10 (el UPDATE no toca el embedding).
SELECT e.id, e.nombre, e.origen, e.gimnasio_id, te.clave AS tipo_clave,
       (e.embedding IS NOT NULL) AS embedding_ok
FROM ejercicios e LEFT JOIN tipos_equipamiento te ON te.id = e.tipo_equipamiento_id
WHERE e.origen = 'Proyecto'
  AND e.id IN ('3203840d-5a79-4c84-9758-b40238f7c85f','1537a321-40ab-4a60-b3b8-e73cab167c28',
               'c7499d5c-48c5-4d16-a691-54a96079ca49','69e15aaf-ca0d-433b-be03-6b496450b1d2',
               '15a5f846-25f5-48a5-a570-264c2b894462','2e9d6c61-6be0-4f4a-8f1b-348869e07a65',
               '14a284c5-5159-4bdb-83f7-733154e07228','a33d764a-d3f2-437e-a527-38e877fdba0f',
               'bca285fb-60ee-40a7-bdc9-771b201365a1','608e091b-1ddb-4a05-abb7-c0c03e48eee6')
ORDER BY e.nombre;

COMMIT;
-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-dosificacion-matriz.sql
-- Etapa 9a-2b. Alternativa por script aprobada por el autor (decision D2, 19/11/2026).
--
-- QUE HACE
--   Datos de referencia del motor de la rutina baseline (no son datos de ningun socio):
--     DOSIS_MODIFICAR : series y repeticiones (o segundos) minimas y maximas de una fila de dosificaciones.
--     DOSIS_BAJA      : baja logica de una fila de dosificaciones (fecha_baja).
--     MATRIZ_MODIFICAR: textos de recomendacion general y sugerencia de dieta de una fila de matrices_recomendacion.
--     MATRIZ_BAJA     : baja logica de una fila de matrices_recomendacion.
--
-- CUANDO USARLO
--   Correccion de un valor del dataset detectada en la validacion (como la plancha 30-60 x 1 de la 9a-1).
--
-- A QUIEN LE PEGA
--   A las rutinas baseline que se generen DESPUES del cambio. Las rutinas ya generadas no cambian.
--   Seeders: CatalogoProyectoSeeder solo inserta las filas de Core que faltan (no pisa ediciones ni
--   reactiva bajas). DataSeeder solo carga los CSV si no hay ninguna fila del dataset; desde la Etapa
--   9a-2b esa comprobacion cuenta tambien las dadas de baja, asi que dar de baja filas no provoca una recarga.
--
-- RESTRICCIONES QUE SE RESPETAN
--   series y repeticiones > 0 y minimo <= maximo (lo exige este script; la base no tiene CHECK para eso).
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-dosificacion-matriz.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
-- =====================================================================================================

BEGIN;

DO $$
DECLARE
    -- ---------------- Parametros (completar) ----------------
    p_admin_usuario_id uuid := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta
    p_operacion        text := 'DOSIS_MODIFICAR'; -- DOSIS_MODIFICAR | DOSIS_BAJA | MATRIZ_MODIFICAR | MATRIZ_BAJA
    p_dosis_id         uuid := '00000000-0000-0000-0000-000000000000'; -- solo DOSIS_*: fila de dosificaciones (id uuid)
    p_matriz_id        int  := 0;      -- solo MATRIZ_*: fila de matrices_recomendacion (id entero)
    -- Solo DOSIS_MODIFICAR (NULL = sin cambio):
    p_series_min int := NULL;
    p_series_max int := NULL;
    p_reps_min   int := NULL;   -- con tipo_medida = 'Segundos', son segundos
    p_reps_max   int := NULL;
    -- Solo MATRIZ_MODIFICAR (NULL = sin cambio):
    p_recomendacion_general text := NULL;
    p_sugerencia_dieta      text := NULL;
    -- ---------------- Fin de parametros ----------------
    r record;
    v_entidad text;
    v_entidad_id text;
    v_detalle text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    CASE upper(p_operacion)
    WHEN 'DOSIS_MODIFICAR', 'DOSIS_BAJA' THEN
        v_entidad := 'DosificacionVolumetrica';
        v_entidad_id := p_dosis_id::text;
        SELECT * INTO r FROM dosificaciones WHERE id = p_dosis_id AND fecha_baja IS NULL FOR UPDATE;
        IF NOT FOUND THEN RAISE EXCEPTION 'Dosificacion % inexistente o dada de baja', p_dosis_id; END IF;

        IF upper(p_operacion) = 'DOSIS_BAJA' THEN
            UPDATE dosificaciones SET fecha_baja = now() WHERE id = p_dosis_id;
            v_detalle := format('BAJA %s/%s/%s', r.parte_cuerpo, r.subzona_objetivo, r.zona_objetivo);
        ELSE
            -- Valores finales: el nuevo si se indico, si no el actual.
            p_series_min := coalesce(p_series_min, r.series_minimas);
            p_series_max := coalesce(p_series_max, r.series_maximas);
            p_reps_min   := coalesce(p_reps_min, r.repeticiones_minimas);
            p_reps_max   := coalesce(p_reps_max, r.repeticiones_maximas);
            IF p_series_min < 1 OR p_reps_min < 1 OR p_series_min > p_series_max OR p_reps_min > p_reps_max THEN
                RAISE EXCEPTION 'Valores invalidos: deben ser mayores que 0 y el minimo no puede superar al maximo';
            END IF;
            UPDATE dosificaciones
               SET series_minimas = p_series_min, series_maximas = p_series_max,
                   repeticiones_minimas = p_reps_min, repeticiones_maximas = p_reps_max, fecha_modificacion = now()
             WHERE id = p_dosis_id;
            v_detalle := format('MODIFICAR series %s-%s -> %s-%s; repeticiones %s-%s -> %s-%s (%s)',
                r.series_minimas, r.series_maximas, p_series_min, p_series_max,
                r.repeticiones_minimas, r.repeticiones_maximas, p_reps_min, p_reps_max, r.tipo_medida);
        END IF;

    WHEN 'MATRIZ_MODIFICAR', 'MATRIZ_BAJA' THEN
        v_entidad := 'MatrizRecomendacion';
        v_entidad_id := p_matriz_id::text;
        PERFORM 1 FROM matrices_recomendacion WHERE id = p_matriz_id AND fecha_baja IS NULL FOR UPDATE;
        IF NOT FOUND THEN RAISE EXCEPTION 'Fila de matriz % inexistente o dada de baja', p_matriz_id; END IF;

        IF upper(p_operacion) = 'MATRIZ_BAJA' THEN
            UPDATE matrices_recomendacion SET fecha_baja = now() WHERE id = p_matriz_id;
            v_detalle := 'BAJA';
        ELSE
            IF p_recomendacion_general IS NULL AND p_sugerencia_dieta IS NULL THEN
                RAISE EXCEPTION 'MATRIZ_MODIFICAR sin cambios';
            END IF;
            UPDATE matrices_recomendacion
               SET recomendacion_general = coalesce(p_recomendacion_general, recomendacion_general),
                   sugerencia_dieta      = coalesce(p_sugerencia_dieta, sugerencia_dieta),
                   fecha_modificacion    = now()
             WHERE id = p_matriz_id;
            v_detalle := 'MODIFICAR campos: ' || concat_ws(', ',
                CASE WHEN p_recomendacion_general IS NOT NULL THEN 'recomendacion_general' END,
                CASE WHEN p_sugerencia_dieta IS NOT NULL THEN 'sugerencia_dieta' END);
        END IF;

    ELSE
        RAISE EXCEPTION 'Operacion desconocida: %', p_operacion;
    END CASE;

    -- Catalogo global: sin gimnasio.
    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', NULL,
            'SCRIPT_' || upper(p_operacion), v_entidad, v_entidad_id,
            left('Script admin-dosificacion-matriz.sql. ' || v_detalle, 500));

    RAISE NOTICE '% %: %', v_entidad, v_entidad_id, v_detalle;
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

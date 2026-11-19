-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-ejercicio-global.sql
-- Etapa 9a-2b. Decision del autor del 19/11/2026: los ejercicios globales (origen Dataset o Proyecto) se
-- MODIFICAN por script; desde la aplicacion el Administrador de la Plataforma solo los desactiva y reactiva.
--
-- QUE HACE
--   Modifica los campos de UN ejercicio global: nombre, zona, musculo principal, sinergistas,
--   estabilizadores, preparacion, ejecucion, dificultad, tipo de medida y tipo de equipamiento.
--   Conserva el origen (Dataset o Proyecto): nunca lo convierte en ejercicio de gimnasio.
--   Si cambia el texto que se vectoriza (nombre, zona, musculo principal o ejecucion), deja embedding = NULL:
--   GymBro.IA/generador_embeddings.py lo regenera (procesa los que tienen embedding IS NULL). Nunca queda un
--   vector de un texto anterior (mismo criterio que la Etapa 9a-2).
--   No da de baja: los ejercicios se desactivan desde la aplicacion (Ejercicios > Desactivar).
--
-- CUANDO USARLO
--   Correccion de un error del dataset o del catalogo de Core (nombre, tecnica, dificultad, equipamiento).
--
-- A QUIEN LE PEGA
--   A todos los gimnasios: el ejercicio es global. Las rutinas y sesiones existentes muestran el nombre nuevo
--   (lo leen por id). El baseline lo usa con los datos nuevos a partir del cambio.
--   Seeders: CatalogoProyectoSeeder y DataSeeder solo insertan filas que faltan; no pisan esta edicion.
--   EquipamientoSeeder solo asigna tipo a los globales con tipo_equipamiento_id NULL; no pisa este cambio.
--
-- RESTRICCIONES QUE SE RESPETAN
--   CK_Ejercicio_TipoMedida (Repeticiones | Segundos), CK_Ejercicio_OrigenGimnasio (sigue sin gimnasio),
--   dificultad 1 a 5, nombre <= 120 caracteres, tipo de equipamiento global y vigente, nombre no repetido
--   (comparacion sin mayusculas; la aplicacion ademas ignora acentos: revisar a mano los casos con tildes).
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-ejercicio-global.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
--   5. Si el NOTICE indica "embedding pendiente", correr generador_embeddings.py (ver GymBro.IA).
-- =====================================================================================================

BEGIN;

DO $$
DECLARE
    -- ---------------- Parametros (completar; NULL = sin cambio) ----------------
    p_admin_usuario_id uuid := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta
    p_ejercicio_id     uuid := '00000000-0000-0000-0000-000000000000'; -- ejercicio global a modificar
    p_nombre           text := NULL;
    p_zona_objetivo    text := NULL;   -- debe ser una zona que ya exista en el catalogo
    p_musculo          text := NULL;
    p_sinergistas      text := NULL;
    p_estabilizadores  text := NULL;
    p_preparacion      text := NULL;
    p_ejecucion        text := NULL;
    p_dificultad       int  := NULL;   -- 1 a 5
    p_tipo_medida      text := NULL;   -- Repeticiones | Segundos
    p_tipo_equipamiento_id uuid := NULL; -- tipo GLOBAL (tipos_equipamiento.gimnasio_id IS NULL)
    -- ---------------- Fin de parametros ----------------
    r record;
    v_cambia_texto boolean;
    v_detalle text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    -- Solo globales (sin gimnasio) y no dados de baja; puede estar activo o desactivado.
    SELECT * INTO r FROM ejercicios WHERE id = p_ejercicio_id AND gimnasio_id IS NULL AND fecha_baja IS NULL FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Ejercicio % inexistente, dado de baja o propio de un gimnasio (este script es solo para globales)', p_ejercicio_id;
    END IF;

    -- Validaciones (las mismas reglas que EjercicioGimnasioService).
    IF p_nombre IS NOT NULL THEN
        p_nombre := btrim(p_nombre);
        IF p_nombre = '' OR length(p_nombre) > 120 THEN RAISE EXCEPTION 'El nombre debe tener entre 1 y 120 caracteres'; END IF;
        IF EXISTS (SELECT 1 FROM ejercicios WHERE id <> p_ejercicio_id AND fecha_baja IS NULL AND lower(btrim(nombre)) = lower(p_nombre)) THEN
            RAISE EXCEPTION 'Ya existe un ejercicio con ese nombre (global o de algun gimnasio)';
        END IF;
    END IF;
    IF p_zona_objetivo IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ejercicios WHERE zona_objetivo = p_zona_objetivo AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'La zona % no existe en el catalogo', p_zona_objetivo;
    END IF;
    IF p_musculo IS NOT NULL AND (btrim(p_musculo) = '' OR length(btrim(p_musculo)) > 120) THEN
        RAISE EXCEPTION 'El musculo principal debe tener entre 1 y 120 caracteres';
    END IF;
    IF p_ejecucion IS NOT NULL AND (btrim(p_ejecucion) = '' OR length(p_ejecucion) > 2000) THEN
        RAISE EXCEPTION 'La ejecucion debe tener entre 1 y 2000 caracteres';
    END IF;
    IF length(coalesce(p_sinergistas, '')) > 2000 OR length(coalesce(p_estabilizadores, '')) > 2000 OR length(coalesce(p_preparacion, '')) > 2000 THEN
        RAISE EXCEPTION 'Sinergistas, estabilizadores y preparacion admiten hasta 2000 caracteres';
    END IF;
    IF p_dificultad IS NOT NULL AND (p_dificultad < 1 OR p_dificultad > 5) THEN
        RAISE EXCEPTION 'La dificultad debe estar entre 1 y 5';
    END IF;
    IF p_tipo_medida IS NOT NULL AND p_tipo_medida NOT IN ('Repeticiones', 'Segundos') THEN
        RAISE EXCEPTION 'El tipo de medida debe ser Repeticiones o Segundos';
    END IF;
    IF p_tipo_equipamiento_id IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM tipos_equipamiento WHERE id = p_tipo_equipamiento_id AND gimnasio_id IS NULL AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'El tipo de equipamiento debe ser global y vigente';
    END IF;

    -- Cambia el texto que se vectoriza si cambia alguno de los cuatro campos (EjercicioTextoSemantico).
    v_cambia_texto := (p_nombre IS NOT NULL AND p_nombre <> r.nombre)
                   OR (p_zona_objetivo IS NOT NULL AND p_zona_objetivo <> r.zona_objetivo)
                   OR (p_musculo IS NOT NULL AND btrim(p_musculo) <> r.musculo_principal)
                   OR (p_ejecucion IS NOT NULL AND btrim(p_ejecucion) <> r.ejecucion);

    UPDATE ejercicios SET
        nombre               = coalesce(p_nombre, nombre),
        zona_objetivo        = coalesce(p_zona_objetivo, zona_objetivo),
        musculo_principal    = coalesce(btrim(p_musculo), musculo_principal),
        sinergistas          = coalesce(p_sinergistas, sinergistas),
        estabilizadores      = coalesce(p_estabilizadores, estabilizadores),
        preparacion          = coalesce(p_preparacion, preparacion),
        ejecucion            = coalesce(btrim(p_ejecucion), ejecucion),
        dificultad           = coalesce(p_dificultad, dificultad),
        tipo_medida          = coalesce(p_tipo_medida, tipo_medida),
        tipo_equipamiento_id = coalesce(p_tipo_equipamiento_id, tipo_equipamiento_id),
        embedding            = CASE WHEN v_cambia_texto THEN NULL ELSE embedding END,  -- nunca un vector viejo
        fecha_modificacion   = now()
        -- origen y gimnasio_id no se tocan: sigue siendo global (Dataset o Proyecto).
    WHERE id = p_ejercicio_id;

    v_detalle := 'Campos: ' || concat_ws(', ',
        CASE WHEN p_nombre IS NOT NULL THEN format('nombre "%s" -> "%s"', r.nombre, p_nombre) END,
        CASE WHEN p_zona_objetivo IS NOT NULL THEN 'zona' END, CASE WHEN p_musculo IS NOT NULL THEN 'musculo' END,
        CASE WHEN p_sinergistas IS NOT NULL THEN 'sinergistas' END, CASE WHEN p_estabilizadores IS NOT NULL THEN 'estabilizadores' END,
        CASE WHEN p_preparacion IS NOT NULL THEN 'preparacion' END, CASE WHEN p_ejecucion IS NOT NULL THEN 'ejecucion' END,
        CASE WHEN p_dificultad IS NOT NULL THEN 'dificultad' END, CASE WHEN p_tipo_medida IS NOT NULL THEN 'tipo_medida' END,
        CASE WHEN p_tipo_equipamiento_id IS NOT NULL THEN 'tipo_equipamiento' END)
        || CASE WHEN v_cambia_texto THEN '. Embedding pendiente' ELSE '' END;

    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', NULL,
            'SCRIPT_EDICION_EJERCICIO_GLOBAL', 'Ejercicio', p_ejercicio_id::text,
            left(format('Script admin-ejercicio-global.sql (origen %s). %s', r.origen, v_detalle), 500));

    RAISE NOTICE 'Ejercicio % (%): %', p_ejercicio_id, r.origen, v_detalle;
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

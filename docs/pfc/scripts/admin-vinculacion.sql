-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-vinculacion.sql
-- Etapa 9a-2b. Alternativa por script aprobada por el autor (decision D2, 19/11/2026).
--
-- QUE HACE
--   Resuelve una solicitud de vinculacion PENDIENTE (un socio independiente pide unirse a un gimnasio):
--     APROBAR  : mismas validaciones que la aplicacion (el DNI declarado coincide con el de la ficha y el
--                socio no esta vinculado a otro gimnasio); vincula al socio y marca la solicitud Aprobada.
--     RECHAZAR : marca la solicitud Rechazada con un motivo.
--
-- CUANDO USARLO
--   Gimnasio sin Dueno activo que resuelva las solicitudes, o una solicitud trabada por un error.
--
-- A QUIEN LE PEGA
--   Fila de solicitudes_vinculacion indicada y, si se aprueba, la columna gimnasio_id de la ficha del socio.
--   El indice unico parcial (socio_id, gimnasio_id) WHERE estado = 'Pendiente' se respeta: la solicitud
--   deja de estar Pendiente. No lee datos de salud del socio.
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-vinculacion.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
-- =====================================================================================================

BEGIN;

DO $$
DECLARE
    -- ---------------- Parametros (completar) ----------------
    p_admin_usuario_id uuid := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta
    p_solicitud_id     uuid := '00000000-0000-0000-0000-000000000000'; -- solicitud pendiente
    p_operacion        text := 'RECHAZAR';  -- APROBAR | RECHAZAR
    p_motivo           text := 'Resuelta por el Administrador de la Plataforma'; -- se guarda en la solicitud
    -- ---------------- Fin de parametros ----------------
    v_socio_id      uuid;
    v_gimnasio_id   uuid;
    v_dni_declarado text;
    v_dni_ficha     text;
    v_gimnasio_ficha uuid;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    SELECT socio_id, gimnasio_id, dni_declarado INTO v_socio_id, v_gimnasio_id, v_dni_declarado
      FROM solicitudes_vinculacion
     WHERE id = p_solicitud_id AND estado = 'Pendiente' AND fecha_baja IS NULL FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Solicitud % inexistente o ya resuelta', p_solicitud_id;
    END IF;

    CASE upper(p_operacion)
    WHEN 'APROBAR' THEN
        -- Solo dni y gimnasio de la ficha (datos administrativos).
        SELECT dni, gimnasio_id INTO v_dni_ficha, v_gimnasio_ficha FROM socios WHERE id = v_socio_id AND fecha_baja IS NULL FOR UPDATE;
        IF NOT FOUND THEN RAISE EXCEPTION 'Socio % inexistente o dado de baja', v_socio_id; END IF;
        IF v_dni_ficha IS DISTINCT FROM v_dni_declarado THEN
            RAISE EXCEPTION 'El DNI de la solicitud no coincide con el de la ficha del socio';
        END IF;
        IF v_gimnasio_ficha IS NOT NULL THEN
            RAISE EXCEPTION 'El socio ya esta vinculado a un gimnasio';
        END IF;
        UPDATE socios SET gimnasio_id = v_gimnasio_id, fecha_modificacion = now() WHERE id = v_socio_id;
        UPDATE solicitudes_vinculacion
           SET estado = 'Aprobada', resuelta_por_usuario_id = p_admin_usuario_id, fecha_resolucion = now(),
               motivo = p_motivo, fecha_modificacion = now()
         WHERE id = p_solicitud_id;

    WHEN 'RECHAZAR' THEN
        UPDATE solicitudes_vinculacion
           SET estado = 'Rechazada', resuelta_por_usuario_id = p_admin_usuario_id, fecha_resolucion = now(),
               motivo = p_motivo, fecha_modificacion = now()
         WHERE id = p_solicitud_id;

    ELSE
        RAISE EXCEPTION 'Operacion desconocida: % (use APROBAR o RECHAZAR)', p_operacion;
    END CASE;

    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', v_gimnasio_id,
            'SCRIPT_VINCULACION_' || CASE WHEN upper(p_operacion) = 'APROBAR' THEN 'APROBADA' ELSE 'RECHAZADA' END,
            'SolicitudVinculacion', p_solicitud_id::text, left('Script admin-vinculacion.sql. ' || coalesce(p_motivo, ''), 500));

    RAISE NOTICE 'Solicitud %: %', p_solicitud_id, upper(p_operacion);
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

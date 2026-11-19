-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-gimnasio.sql
-- Etapa 9a-2b. Alternativa por script aprobada por el autor (decision D2, 19/11/2026).
--
-- QUE HACE
--   Modifica los datos de un gimnasio que la aplicacion no permite editar al Administrador de la
--   Plataforma: el nombre y la politica de revision humana de rutinas. Activar y desactivar un gimnasio
--   SI esta en la aplicacion (Plataforma > Gimnasios); por eso no se repite aca.
--
-- CUANDO USARLO
--   Correccion excepcional pedida por el Dueno o detectada por la plataforma (por ejemplo, un nombre mal
--   cargado en el alta). No reemplaza a la pantalla del Dueno, que ya cambia su politica de revision.
--
-- A QUIEN LE PEGA
--   Solo a la fila del gimnasio indicado (tabla gimnasios). No toca usuarios, socios ni datos de salud.
--   El cambio de politica afecta a las rutinas que se generen despues; las existentes no cambian.
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-gimnasio.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
-- =====================================================================================================

BEGIN;                                                         -- todo dentro de una transaccion

DO $$
DECLARE
    -- ---------------- Parametros (completar) ----------------
    p_admin_usuario_id  uuid    := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta (queda en la auditoria)
    p_gimnasio_id       uuid    := '00000000-0000-0000-0000-000000000000'; -- gimnasio a modificar
    p_nuevo_nombre      text    := NULL;   -- nombre nuevo (NULL = no se cambia); maximo 150 caracteres
    p_requiere_revision boolean := NULL;   -- politica de revision humana (NULL = no se cambia)
    -- ---------------- Fin de parametros ----------------
    v_nombre_actual text;                  -- nombre antes del cambio (para la auditoria)
    v_detalle       text := '';            -- texto que se guarda en audit_logs
BEGIN
    -- Quien ejecuta debe ser un Administrador de la Plataforma vigente.
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    -- El gimnasio debe existir y no estar dado de baja. FOR UPDATE bloquea la fila durante la transaccion.
    SELECT nombre INTO v_nombre_actual FROM gimnasios WHERE id = p_gimnasio_id AND fecha_baja IS NULL FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Gimnasio % inexistente o dado de baja', p_gimnasio_id;
    END IF;

    IF p_nuevo_nombre IS NULL AND p_requiere_revision IS NULL THEN
        RAISE EXCEPTION 'No se indico ningun cambio';
    END IF;

    IF p_nuevo_nombre IS NOT NULL THEN
        p_nuevo_nombre := btrim(p_nuevo_nombre);                -- sin espacios al comienzo ni al final
        IF p_nuevo_nombre = '' OR length(p_nuevo_nombre) > 150 THEN   -- mismo limite que la columna (varchar 150)
            RAISE EXCEPTION 'El nombre debe tener entre 1 y 150 caracteres';
        END IF;
        UPDATE gimnasios SET nombre = p_nuevo_nombre, fecha_modificacion = now() WHERE id = p_gimnasio_id;
        v_detalle := v_detalle || format('nombre: "%s" -> "%s". ', v_nombre_actual, p_nuevo_nombre);
    END IF;

    IF p_requiere_revision IS NOT NULL THEN
        UPDATE gimnasios SET requiere_aprobacion_rutinas = p_requiere_revision, fecha_modificacion = now() WHERE id = p_gimnasio_id;
        v_detalle := v_detalle || format('requiere_aprobacion_rutinas = %s. ', p_requiere_revision);
    END IF;

    -- Registro de auditoria (append-only), visible en Plataforma > Auditoria.
    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', p_gimnasio_id,
            'SCRIPT_MODIFICACION_GIMNASIO', 'Gimnasio', p_gimnasio_id::text,
            left('Script admin-gimnasio.sql. ' || v_detalle, 500));

    RAISE NOTICE 'Gimnasio %: %', p_gimnasio_id, v_detalle;
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

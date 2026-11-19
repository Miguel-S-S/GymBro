-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-invitacion.sql
-- Etapa 9a-2b. Alternativa por script aprobada por el autor (decision D2, 19/11/2026).
--
-- QUE HACE
--   Revoca las invitaciones VIGENTES de un socio (no usadas, no revocadas y no vencidas): el enlace deja
--   de servir para activar la cuenta. Es lo mismo que hace la aplicacion al renovar la invitacion o dar de
--   baja al socio. No emite invitaciones nuevas: el token lo genera la aplicacion (el Dueno la renueva).
--
-- CUANDO USARLO
--   Enlace de invitacion enviado a un destinatario equivocado o filtrado, cuando el Dueno no puede
--   renovarla desde la aplicacion.
--
-- A QUIEN LE PEGA
--   Filas de invitaciones del socio indicado (columna revocada_utc). No borra filas: quedan como historial.
--   No toca la ficha del socio ni su cuenta.
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-invitacion.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
-- =====================================================================================================

BEGIN;

DO $$
DECLARE
    -- ---------------- Parametros (completar) ----------------
    p_admin_usuario_id uuid := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta
    p_socio_id         uuid := '00000000-0000-0000-0000-000000000000'; -- socio cuyas invitaciones se revocan
    -- ---------------- Fin de parametros ----------------
    v_gimnasio_id uuid;
    v_revocadas   int;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    SELECT gimnasio_id INTO v_gimnasio_id FROM socios WHERE id = p_socio_id;   -- solo para la auditoria
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Socio % inexistente', p_socio_id;
    END IF;

    -- Vigente = no usada, no revocada, no vencida y no dada de baja.
    UPDATE invitaciones
       SET revocada_utc = now(), fecha_modificacion = now()
     WHERE socio_id = p_socio_id AND usada_utc IS NULL AND revocada_utc IS NULL
       AND expira_utc > now() AND fecha_baja IS NULL;
    GET DIAGNOSTICS v_revocadas = ROW_COUNT;                         -- cuantas filas cambiaron

    IF v_revocadas = 0 THEN
        RAISE NOTICE 'El socio % no tenia invitaciones vigentes: no se cambio nada', p_socio_id;
        RETURN;                                                       -- sin cambios no se audita
    END IF;

    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', v_gimnasio_id,
            'SCRIPT_REVOCAR_INVITACION', 'InvitacionSocio', p_socio_id::text,
            format('Script admin-invitacion.sql. Invitaciones revocadas: %s', v_revocadas));

    RAISE NOTICE 'Socio %: % invitaciones revocadas', p_socio_id, v_revocadas;
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

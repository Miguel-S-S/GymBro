-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-asignacion.sql
-- Etapa 9a-2b. Alternativa por script aprobada por el autor (decision D2, 19/11/2026).
--
-- QUE HACE
--   ASIGNAR   : asigna un socio a un Personal Trainer del mismo gimnasio (igual que la aplicacion).
--   DESASIGNAR: quita esa asignacion. Hoy no existe en la aplicacion ni para el Dueno.
--
--   Por que DESASIGNAR borra la fila en vez de una baja logica: el indice unico (entrenador_usuario_id,
--   socio_id) NO es parcial y la aplicacion ya borra las asignaciones al desvincular un socio
--   (AsignacionRepository.QuitarDeSocioAsync). Una baja logica impediria volver a asignarlo desde la
--   aplicacion (violaria el indice). La trazabilidad queda en audit_logs.
--
-- CUANDO USARLO
--   Personal Trainer que deja de atender a un socio, o asignacion hecha por error.
--
-- A QUIEN LE PEGA
--   Una fila de asignaciones. Al desasignar, el Personal Trainer deja de ver al socio y sus datos
--   (rutinas, sesiones, mediciones); el Dueno los sigue viendo. No lee datos de salud.
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-asignacion.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
-- =====================================================================================================

BEGIN;

DO $$
DECLARE
    -- ---------------- Parametros (completar) ----------------
    p_admin_usuario_id    uuid := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta
    p_entrenador_usuario_id uuid := '00000000-0000-0000-0000-000000000000'; -- cuenta del Personal Trainer
    p_socio_id            uuid := '00000000-0000-0000-0000-000000000000'; -- ficha del socio
    p_operacion           text := 'DESASIGNAR'; -- ASIGNAR | DESASIGNAR
    -- ---------------- Fin de parametros ----------------
    v_gimnasio_entrenador uuid;
    v_gimnasio_socio      uuid;
    v_filas               int;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    -- El entrenador debe ser un Personal Trainer vigente; el socio, una ficha vigente del mismo gimnasio.
    SELECT gimnasio_id INTO v_gimnasio_entrenador FROM usuarios
     WHERE id = p_entrenador_usuario_id AND rol = 'Entrenador' AND fecha_baja IS NULL;
    IF NOT FOUND THEN RAISE EXCEPTION 'Personal Trainer % inexistente o dado de baja', p_entrenador_usuario_id; END IF;

    SELECT gimnasio_id INTO v_gimnasio_socio FROM socios WHERE id = p_socio_id AND fecha_baja IS NULL;
    IF NOT FOUND THEN RAISE EXCEPTION 'Socio % inexistente o dado de baja', p_socio_id; END IF;

    CASE upper(p_operacion)
    WHEN 'ASIGNAR' THEN
        IF v_gimnasio_socio IS DISTINCT FROM v_gimnasio_entrenador THEN
            RAISE EXCEPTION 'El socio y el Personal Trainer no son del mismo gimnasio';
        END IF;
        IF EXISTS (SELECT 1 FROM asignaciones WHERE entrenador_usuario_id = p_entrenador_usuario_id AND socio_id = p_socio_id) THEN
            RAISE EXCEPTION 'El socio ya esta asignado a ese Personal Trainer';
        END IF;
        INSERT INTO asignaciones (id, gimnasio_id, entrenador_usuario_id, socio_id, fecha_alta, usuario_alta)
        VALUES (gen_random_uuid(), v_gimnasio_entrenador, p_entrenador_usuario_id, p_socio_id, now(), 1);
        -- usuario_alta = 1: mismo valor por defecto que usa la aplicacion (EntidadAuditable.UsuarioAlta).

    WHEN 'DESASIGNAR' THEN
        DELETE FROM asignaciones WHERE entrenador_usuario_id = p_entrenador_usuario_id AND socio_id = p_socio_id;
        GET DIAGNOSTICS v_filas = ROW_COUNT;
        IF v_filas = 0 THEN RAISE EXCEPTION 'No existe esa asignacion'; END IF;

    ELSE
        RAISE EXCEPTION 'Operacion desconocida: % (use ASIGNAR o DESASIGNAR)', p_operacion;
    END CASE;

    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', v_gimnasio_entrenador,
            'SCRIPT_' || upper(p_operacion) || '_ENTRENADOR', 'AsignacionEntrenadorSocio', NULL,
            format('Script admin-asignacion.sql. entrenador=%s; socio=%s', p_entrenador_usuario_id, p_socio_id));

    RAISE NOTICE '%: entrenador % / socio %', upper(p_operacion), p_entrenador_usuario_id, p_socio_id;
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

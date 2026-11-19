-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-usuario.sql
-- Etapa 9a-2b. Alternativa por script aprobada por el autor (decision D2, 19/11/2026).
--
-- QUE HACE
--   Administra cuentas del PERSONAL de un gimnasio (Dueno de Gimnasio y Personal Trainer), que la
--   aplicacion no deja gestionar al Administrador de la Plataforma:
--     CORREGIR    : nombre completo y/o email.
--     DESACTIVAR  : usuarios.activo = false (no puede iniciar sesion; reversible).
--     ACTIVAR     : usuarios.activo = true.
--     BAJA        : baja logica (fecha_baja). Para un Personal Trainer, ademas quita sus asignaciones.
--   NO da de alta cuentas ni cambia contrasenas: el hash lo genera la aplicacion (el Administrador de la
--   Plataforma crea gimnasio + Dueno en Plataforma > Gimnasios; el Dueno crea sus Personal Trainers).
--   NO toca cuentas de Socio ni de Administrador de la Plataforma.
--
-- CUANDO USARLO
--   Dueno que deja el gimnasio, Personal Trainer que ya no trabaja, email mal cargado, cuenta comprometida.
--
-- A QUIEN LE PEGA
--   A la fila de usuarios indicada y, en la BAJA de un Personal Trainer, a sus filas de asignaciones (se
--   borran, igual que hace la aplicacion al desvincular un socio). Los ejercicios que haya creado conservan
--   creado_por_usuario_id (FK Restrict): por eso la baja es logica y nunca un DELETE.
--   Ojo: dar de baja o desactivar al UNICO Dueno deja al gimnasio sin quien lo administre.
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-usuario.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
-- =====================================================================================================

BEGIN;

DO $$
DECLARE
    -- ---------------- Parametros (completar) ----------------
    p_admin_usuario_id uuid := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta
    p_usuario_id       uuid := '00000000-0000-0000-0000-000000000000'; -- cuenta del Dueno o Personal Trainer
    p_operacion        text := 'CORREGIR';  -- CORREGIR | DESACTIVAR | ACTIVAR | BAJA
    p_nuevo_nombre     text := NULL;        -- solo CORREGIR (NULL = no se cambia)
    p_nuevo_email      text := NULL;        -- solo CORREGIR (NULL = no se cambia); se guarda en minusculas
    -- ---------------- Fin de parametros ----------------
    v_rol         text;
    v_gimnasio_id uuid;
    v_detalle     text;
    v_asignaciones int := 0;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    -- Solo personal de gimnasio, vigente. FOR UPDATE bloquea la fila.
    SELECT rol, gimnasio_id INTO v_rol, v_gimnasio_id
      FROM usuarios WHERE id = p_usuario_id AND fecha_baja IS NULL FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Usuario % inexistente o dado de baja', p_usuario_id;
    END IF;
    IF v_rol NOT IN ('AdministradorGimnasio', 'Entrenador') THEN
        RAISE EXCEPTION 'Este script solo administra Duenos y Personal Trainers (rol actual: %)', v_rol;
    END IF;

    CASE upper(p_operacion)
    WHEN 'CORREGIR' THEN
        IF p_nuevo_nombre IS NULL AND p_nuevo_email IS NULL THEN
            RAISE EXCEPTION 'CORREGIR sin cambios';
        END IF;
        IF p_nuevo_nombre IS NOT NULL THEN
            p_nuevo_nombre := btrim(p_nuevo_nombre);
            IF p_nuevo_nombre = '' THEN RAISE EXCEPTION 'El nombre no puede quedar vacio'; END IF;
            UPDATE usuarios SET nombre_completo = p_nuevo_nombre, fecha_modificacion = now() WHERE id = p_usuario_id;
        END IF;
        IF p_nuevo_email IS NOT NULL THEN
            p_nuevo_email := lower(btrim(p_nuevo_email));                          -- la aplicacion normaliza asi
            IF p_nuevo_email !~ '^[^@\s]+@[^@\s]+\.[^@\s]+$' THEN RAISE EXCEPTION 'Email invalido'; END IF;
            -- El indice unico de email incluye todas las cuentas (tambien las dadas de baja).
            IF EXISTS (SELECT 1 FROM usuarios WHERE email = p_nuevo_email AND id <> p_usuario_id) THEN
                RAISE EXCEPTION 'Ya existe una cuenta con ese email';
            END IF;
            UPDATE usuarios SET email = p_nuevo_email, fecha_modificacion = now() WHERE id = p_usuario_id;
        END IF;
        v_detalle := format('CORREGIR nombre=%s email=%s', coalesce(p_nuevo_nombre, '(sin cambio)'), coalesce(p_nuevo_email, '(sin cambio)'));

    WHEN 'DESACTIVAR' THEN
        UPDATE usuarios SET activo = false, fecha_modificacion = now() WHERE id = p_usuario_id;
        v_detalle := 'DESACTIVAR';

    WHEN 'ACTIVAR' THEN
        UPDATE usuarios SET activo = true, fecha_modificacion = now() WHERE id = p_usuario_id;
        v_detalle := 'ACTIVAR';

    WHEN 'BAJA' THEN
        IF v_rol = 'Entrenador' THEN
            -- Mismo criterio que la aplicacion: las asignaciones se borran (su indice unico no es parcial).
            DELETE FROM asignaciones WHERE entrenador_usuario_id = p_usuario_id;
            GET DIAGNOSTICS v_asignaciones = ROW_COUNT;
        END IF;
        UPDATE usuarios SET activo = false, fecha_baja = now(), fecha_modificacion = now() WHERE id = p_usuario_id;
        v_detalle := format('BAJA (asignaciones quitadas: %s)', v_asignaciones);

    ELSE
        RAISE EXCEPTION 'Operacion desconocida: % (use CORREGIR, DESACTIVAR, ACTIVAR o BAJA)', p_operacion;
    END CASE;

    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', v_gimnasio_id,
            'SCRIPT_' || upper(p_operacion) || '_USUARIO', 'Usuario', p_usuario_id::text,
            left(format('Script admin-usuario.sql. Rol %s. %s', v_rol, v_detalle), 500));

    RAISE NOTICE 'Usuario % (%): %', p_usuario_id, v_rol, v_detalle;
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

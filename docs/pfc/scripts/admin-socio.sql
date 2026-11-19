-- =====================================================================================================
-- GymBro - docs/pfc/scripts/admin-socio.sql
-- Etapa 9a-2b. Alternativa por script aprobada por el autor (decision 1 del 19/11/2026: el Socio queda
-- fuera del ABM del Administrador de la Plataforma en la aplicacion y se cubre por script).
--
-- QUE HACE
--   Solo DATOS ADMINISTRATIVOS de la ficha del socio:
--     CORREGIR : nombre, apellido, email y/o DNI.
--     ESTADO   : estado administrativo Activo | Inactivo (informativo, Etapa 9a-2b).
--     BAJA     : igual que la aplicacion. Ficha sin cuenta: baja logica y se libera el DNI.
--                Socio con cuenta propia: se desvincula del gimnasio (queda independiente con su historial).
--
-- LEY 25.326 - PROHIBIDO EN ESTE SCRIPT
--   No lee ni modifica genero, fecha_nacimiento, objetivo, nivel, consentimiento_datos_salud_fecha,
--   mediciones, bienestar, rutinas, sesiones ni series. No hay ningun SELECT que devuelva esos datos.
--
-- CUANDO USARLO
--   Correccion excepcional a pedido del Dueno o del propio socio (dato administrativo mal cargado), o un
--   gimnasio sin Dueno activo que no puede hacerlo desde la aplicacion.
--
-- A QUIEN LE PEGA
--   Fila de socios indicada. En BAJA: revoca sus invitaciones vigentes y borra sus asignaciones a Personal
--   Trainers (lo mismo que hace la aplicacion). Si tiene una rutina pendiente de revision, la BAJA se rechaza.
--   CORREGIR el email solo se permite si el socio no activo su cuenta (misma regla que la aplicacion).
--
-- COMO SE USA
--   1. Completar los parametros del bloque DECLARE (no hay datos reales en este archivo).
--   2. Ejecutar el archivo completo: psql -h localhost -p 5433 -U <usuario> -d <base> -f admin-socio.sql
--   3. Revisar los mensajes NOTICE. El archivo termina en ROLLBACK: no queda ningun cambio.
--   4. Si el resultado es el esperado, comentar el ROLLBACK, descomentar el COMMIT y volver a ejecutar.
-- =====================================================================================================

BEGIN;

DO $$
DECLARE
    -- ---------------- Parametros (completar) ----------------
    p_admin_usuario_id uuid := '00000000-0000-0000-0000-000000000000'; -- Administrador de la Plataforma que ejecuta
    p_socio_id         uuid := '00000000-0000-0000-0000-000000000000'; -- ficha del socio
    p_operacion        text := 'ESTADO';   -- CORREGIR | ESTADO | BAJA
    p_nuevo_estado     text := 'Inactivo'; -- solo ESTADO: Activo | Inactivo
    p_nuevo_nombre     text := NULL;       -- solo CORREGIR (NULL = sin cambio)
    p_nuevo_apellido   text := NULL;       -- solo CORREGIR (NULL = sin cambio)
    p_nuevo_email      text := NULL;       -- solo CORREGIR (NULL = sin cambio)
    p_nuevo_dni        text := NULL;       -- solo CORREGIR (NULL = sin cambio); 7 a 9 digitos, se quitan los puntos
    -- ---------------- Fin de parametros ----------------
    v_gimnasio_id uuid;
    v_tiene_cuenta boolean;
    v_detalle text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_admin_usuario_id AND rol = 'SuperAdministrador' AND fecha_baja IS NULL) THEN
        RAISE EXCEPTION 'p_admin_usuario_id no es un Administrador de la Plataforma vigente';
    END IF;

    -- Solo se lee el gimnasio (dato administrativo). FOR UPDATE bloquea la fila.
    SELECT gimnasio_id INTO v_gimnasio_id FROM socios WHERE id = p_socio_id AND fecha_baja IS NULL FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Socio % inexistente o dado de baja', p_socio_id;
    END IF;
    v_tiene_cuenta := EXISTS (SELECT 1 FROM usuarios WHERE socio_id = p_socio_id AND fecha_baja IS NULL);

    CASE upper(p_operacion)
    WHEN 'ESTADO' THEN
        -- El CHECK CK_Socio_Estado tambien lo exige; se valida antes para dar un mensaje claro.
        IF p_nuevo_estado NOT IN ('Activo', 'Inactivo') THEN
            RAISE EXCEPTION 'El estado debe ser Activo o Inactivo';
        END IF;
        UPDATE socios SET estado = p_nuevo_estado, fecha_modificacion = now() WHERE id = p_socio_id;
        v_detalle := 'ESTADO ' || p_nuevo_estado;

    WHEN 'CORREGIR' THEN
        IF coalesce(p_nuevo_nombre, p_nuevo_apellido, p_nuevo_email, p_nuevo_dni) IS NULL THEN
            RAISE EXCEPTION 'CORREGIR sin cambios';
        END IF;
        IF p_nuevo_nombre IS NOT NULL THEN
            IF btrim(p_nuevo_nombre) = '' THEN RAISE EXCEPTION 'El nombre no puede quedar vacio'; END IF;
            UPDATE socios SET nombre = btrim(p_nuevo_nombre) WHERE id = p_socio_id;
        END IF;
        IF p_nuevo_apellido IS NOT NULL THEN
            IF btrim(p_nuevo_apellido) = '' THEN RAISE EXCEPTION 'El apellido no puede quedar vacio'; END IF;
            UPDATE socios SET apellido = btrim(p_nuevo_apellido) WHERE id = p_socio_id;
        END IF;
        IF p_nuevo_email IS NOT NULL THEN
            IF v_tiene_cuenta THEN
                RAISE EXCEPTION 'El socio ya activo su cuenta: el email lo cambia el propio socio, no la plataforma';
            END IF;
            p_nuevo_email := lower(btrim(p_nuevo_email));
            IF p_nuevo_email !~ '^[^@\s]+@[^@\s]+\.[^@\s]+$' THEN RAISE EXCEPTION 'Email invalido'; END IF;
            IF EXISTS (SELECT 1 FROM usuarios WHERE email = p_nuevo_email) THEN
                RAISE EXCEPTION 'Ya existe una cuenta con ese email';
            END IF;
            UPDATE socios SET email = p_nuevo_email WHERE id = p_socio_id;
        END IF;
        IF p_nuevo_dni IS NOT NULL THEN
            p_nuevo_dni := btrim(replace(p_nuevo_dni, '.', ''));                -- misma normalizacion que la aplicacion
            IF p_nuevo_dni !~ '^[0-9]{7,9}$' THEN RAISE EXCEPTION 'DNI invalido (7 a 9 digitos)'; END IF;
            -- El indice unico de dni incluye las fichas dadas de baja.
            IF EXISTS (SELECT 1 FROM socios WHERE dni = p_nuevo_dni AND id <> p_socio_id) THEN
                RAISE EXCEPTION 'Ya existe un socio registrado con ese DNI';
            END IF;
            UPDATE socios SET dni = p_nuevo_dni WHERE id = p_socio_id;
        END IF;
        UPDATE socios SET fecha_modificacion = now() WHERE id = p_socio_id;
        -- En la auditoria no se copian los valores (son datos personales): solo que campos cambiaron.
        v_detalle := 'CORREGIR campos: '
            || concat_ws(', ', CASE WHEN p_nuevo_nombre IS NOT NULL THEN 'nombre' END, CASE WHEN p_nuevo_apellido IS NOT NULL THEN 'apellido' END,
                         CASE WHEN p_nuevo_email IS NOT NULL THEN 'email' END, CASE WHEN p_nuevo_dni IS NOT NULL THEN 'dni' END);

    WHEN 'BAJA' THEN
        -- Misma regla que la aplicacion: no se da de baja con rutinas pendientes de revision.
        IF EXISTS (SELECT 1 FROM prescripciones_entrenamiento
                    WHERE socio_id = p_socio_id AND estado = 'PendienteRevision' AND fecha_baja IS NULL) THEN
            RAISE EXCEPTION 'El socio tiene rutinas pendientes de revision: resolverlas antes de la baja';
        END IF;
        DELETE FROM asignaciones WHERE socio_id = p_socio_id;                   -- igual que la aplicacion
        UPDATE invitaciones SET revocada_utc = now(), fecha_modificacion = now()  -- revoca las vigentes
         WHERE socio_id = p_socio_id AND usada_utc IS NULL AND revocada_utc IS NULL AND fecha_baja IS NULL;
        IF v_tiene_cuenta THEN
            UPDATE socios SET gimnasio_id = NULL, fecha_modificacion = now() WHERE id = p_socio_id;  -- desvincula
            v_detalle := 'BAJA: desvinculado del gimnasio (tiene cuenta propia)';
        ELSE
            UPDATE socios SET fecha_baja = now(), dni = NULL WHERE id = p_socio_id;                 -- baja logica
            v_detalle := 'BAJA logica (sin cuenta); DNI liberado';
        END IF;

    ELSE
        RAISE EXCEPTION 'Operacion desconocida: % (use CORREGIR, ESTADO o BAJA)', p_operacion;
    END CASE;

    INSERT INTO audit_logs (id, fecha, usuario_id, rol, gimnasio_id, accion, entidad, entidad_id, detalle)
    VALUES (gen_random_uuid(), now(), p_admin_usuario_id, 'SuperAdministrador', v_gimnasio_id,
            'SCRIPT_' || upper(p_operacion) || '_SOCIO', 'Socio', p_socio_id::text,
            left('Script admin-socio.sql. ' || v_detalle, 500));

    RAISE NOTICE 'Socio %: %', p_socio_id, v_detalle;
END $$;

ROLLBACK;   -- por defecto no se guarda nada
-- COMMIT;  -- para guardar: comentar el ROLLBACK de arriba y descomentar esta linea

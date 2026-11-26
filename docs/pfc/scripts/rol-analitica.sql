-- =====================================================================================================
-- GymBro - docs/pfc/scripts/rol-analitica.sql
-- Etapas 10b-1 y 12. Roles de base de datos para GymBro.Analytics.
--
-- QUE HACE
--   1. Crea (o reemplaza) el esquema "analitica" con vistas que exponen SOLO datos de socios con
--      origen_datos = 'Sintetico' y sin baja logica: socios (sin nombre, email ni DNI), asistencias, cuotas,
--      bienestar diario, series realizadas con su dosis prescrita, gimnasios con socios sinteticos y puntajes
--      de abandono. Ningun dato de personas reales llega a la analitica (Ley 25.326).
--   2. Crea o actualiza dos roles con contrasena:
--        gymbro_analitica_lectura     solo SELECT sobre las vistas; sesiones de solo lectura por defecto.
--        gymbro_analitica_puntuacion  SELECT sobre las vistas e INSERT en public.prediccion_abandono.
--      Ninguno tiene permisos sobre las tablas de la aplicacion (las vistas se ejecutan con los permisos de su
--      duenio, el usuario administrador que corre este script).
--
-- A QUIEN LE PEGA
--   Crea el esquema "analitica", sus vistas y los dos roles. No modifica tablas ni datos de la aplicacion.
--
-- COMO SE USA
--   1. Reemplazar las dos contrasenas del bloque DECLARE (al menos 16 caracteres; no versionar el archivo
--      con contrasenas reales: usar una copia local).
--   2. Ejecutar con el usuario administrador de la base, DESPUES de aplicar las migraciones de la API
--      (necesita las tablas asistencias, cuotas_socio y prediccion_abandono):
--        psql -h localhost -p 5433 -U <administrador> -d <base> -f rol-analitica.sql
--   3. Configurar GymBro.Analytics con ANALITICA_PG_USUARIO y ANALITICA_PG_CLAVE (ver gymbro_analytics/conexion.py).
--   Es idempotente: se puede volver a ejecutar (actualiza vistas, contrasenas y permisos).
-- =====================================================================================================

BEGIN;

CREATE SCHEMA IF NOT EXISTS analitica;

CREATE OR REPLACE VIEW analitica.v_socios AS
SELECT s.id AS socio_id, s.gimnasio_id, s.fecha_nacimiento::date AS fecha_nacimiento, s.genero, s.nivel, s.objetivo,
       s.fecha_alta, s.estado, s.origen_datos
FROM socios s
WHERE s.origen_datos = 'Sintetico' AND s.fecha_baja IS NULL AND s.gimnasio_id IS NOT NULL;

CREATE OR REPLACE VIEW analitica.v_gimnasios AS
SELECT g.id AS gimnasio_id, g.nombre
FROM gimnasios g
WHERE g.fecha_baja IS NULL AND EXISTS (SELECT 1 FROM analitica.v_socios s WHERE s.gimnasio_id = g.id);

CREATE OR REPLACE VIEW analitica.v_asistencias AS
SELECT a.id AS asistencia_id, a.socio_id, a.gimnasio_id, a.fecha, a.instante, a.metodo
FROM asistencias a
JOIN analitica.v_socios s ON s.socio_id = a.socio_id
WHERE a.fecha_baja IS NULL;

CREATE OR REPLACE VIEW analitica.v_cuotas AS
SELECT c.socio_id, c.gimnasio_id, c.anio, c.mes, c.monto, c.vencimiento, c.fecha_pago, c.estado
FROM cuotas_socio c
JOIN analitica.v_socios s ON s.socio_id = c.socio_id
WHERE c.fecha_baja IS NULL;

CREATE OR REPLACE VIEW analitica.v_bienestar AS
SELECT r.socio_id, s.gimnasio_id, r.fecha, r.horas_sueno, r.estres, r.fatiga, r.peso_kg,
       CASE r.nutricion_categoria WHEN 1 THEN 'Baja' WHEN 2 THEN 'Habitual' WHEN 3 THEN 'Alta' END AS nutricion_categoria,
       r.ingesta_agua_litros, r.frecuencia_cardiaca_reposo
FROM registros_bienestar_diario r
JOIN analitica.v_socios s ON s.socio_id = r.socio_id
WHERE r.fecha_baja IS NULL;

CREATE OR REPLACE VIEW analitica.v_series AS
SELECT se.socio_id, s.gimnasio_id, se.id AS sesion_id,
       (se.inicio AT TIME ZONE 'America/Argentina/Buenos_Aires')::date AS fecha, se.inicio,
       sj.ejercicio_id, sj.orden, sr.orden_serie, sr.peso_levantado_kg AS peso_kg, sr.repeticiones, sr.rir,
       sr.es_calentamiento, pe.rir_objetivo, pe.repeticiones_minimas, pe.repeticiones_maximas
FROM sesiones se
JOIN analitica.v_socios s ON s.socio_id = se.socio_id
JOIN sesiones_ejercicios sj ON sj.sesion_id = se.id AND sj.fecha_baja IS NULL
JOIN series_realizadas sr ON sr.sesion_ejercicio_id = sj.id AND sr.fecha_baja IS NULL
LEFT JOIN prescripciones_ejercicios pe ON pe.id = sj.prescripcion_ejercicio_id
WHERE se.fecha_baja IS NULL;

CREATE OR REPLACE VIEW analitica.v_prediccion_abandono AS
SELECT p.socio_id, s.gimnasio_id, p.fecha, p.puntaje, p.version_modelo, p.origen_datos
FROM prediccion_abandono p
JOIN analitica.v_socios s ON s.socio_id = p.socio_id
WHERE p.fecha_baja IS NULL;

DO $$
DECLARE
    -- ---------------- Parametros (completar en una copia local) ----------------
    p_clave_lectura     text := 'REEMPLAZAR_CLAVE_LECTURA';
    p_clave_puntuacion  text := 'REEMPLAZAR_CLAVE_PUNTUACION';
BEGIN
    IF p_clave_lectura LIKE 'REEMPLAZAR%' OR p_clave_puntuacion LIKE 'REEMPLAZAR%'
       OR length(p_clave_lectura) < 16 OR length(p_clave_puntuacion) < 16 THEN
        RAISE EXCEPTION 'Complete las dos contrasenas del bloque DECLARE (al menos 16 caracteres).';
    END IF;

    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gymbro_analitica_lectura') THEN
        EXECUTE format('ALTER ROLE gymbro_analitica_lectura LOGIN PASSWORD %L', p_clave_lectura);
    ELSE
        EXECUTE format('CREATE ROLE gymbro_analitica_lectura LOGIN PASSWORD %L', p_clave_lectura);
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gymbro_analitica_puntuacion') THEN
        EXECUTE format('ALTER ROLE gymbro_analitica_puntuacion LOGIN PASSWORD %L', p_clave_puntuacion);
    ELSE
        EXECUTE format('CREATE ROLE gymbro_analitica_puntuacion LOGIN PASSWORD %L', p_clave_puntuacion);
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO gymbro_analitica_lectura, gymbro_analitica_puntuacion', current_database());
    RAISE NOTICE 'Roles de analitica listos en la base %.', current_database();
END $$;

-- Lectura: sesiones de solo lectura por defecto, ademas de los permisos.
ALTER ROLE gymbro_analitica_lectura SET default_transaction_read_only = on;

-- Sin permisos sobre las tablas de la aplicacion (por si se otorgaron antes).
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM gymbro_analitica_lectura, gymbro_analitica_puntuacion;

GRANT USAGE ON SCHEMA analitica TO gymbro_analitica_lectura, gymbro_analitica_puntuacion;
GRANT SELECT ON ALL TABLES IN SCHEMA analitica TO gymbro_analitica_lectura, gymbro_analitica_puntuacion;

-- Puntuacion por lotes: solo insertar puntajes (sin leer ni modificar la tabla).
GRANT INSERT ON public.prediccion_abandono TO gymbro_analitica_puntuacion;

COMMIT;

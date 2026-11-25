# Etapa 11 — Asistencia y cuotas

Rama `cierre-pfc`. Commit de código: `8e846fe`. Evidencia: `docs/pfc/evidencia/etapa11-*.txt`. Migración: `Etapa11_AsistenciaYCuotas` (SQL generado sin conexión en `etapa11-migracion.sql.txt`). **La base local `gymbro_db` no se migró desde Claude Code**: la migración se aplica al levantar `GymBro.Api` (`Program.cs`, `MigrateAsync`) y la aplicación la confirma el desarrollador.

## Alcance implementado

| Pedido | Estado | Dónde |
|---|---|---|
| Entidad `Asistencia` (socio, gimnasio, instante, método Qr/Manual, usuario que registró) | Implementado y verificado | `GymBro.Domain/Entities/Asistencia.cs` |
| Entidad `CuotaSocio` (socio, gimnasio, período año-mes, monto, vencimiento, fecha de pago opcional, estado Pendiente/Pagada) | Implementado y verificado | `GymBro.Domain/Entities/CuotaSocio.cs` |
| Morosidad derivada (no es un estado guardado) | Implementado y verificado | `CuotaSocio.EstaVencida`, `CuotaRepository.ListarVencidasAsync` |
| Filtros globales por gimnasio | Implementado y verificado | `GymBroDbContext.ConfigurarAsistenciaYCuotas` |
| Una asistencia por socio y día local, idempotente | Implementado y verificado | índice único parcial `(socio_id, fecha)`; `AsistenciaService.RegistrarAsync` |
| QR firmado de 2 minutos con clave de configuración | Implementado y verificado | `TokenAsistencia`, `AsistenciaConfig` |
| QR dibujado en el cliente; cámara solo con `BarcodeDetector`; ingreso manual de código siempre | Implementado; verificado por pruebas de componente (la lectura con cámara real **no se verificó**) | `features/mi/mi-asistencia.ts`, `features/gestion/asistencia.ts`, `core/util/qr.ts` |
| Generación de cuotas del mes para socios activos, pago manual, morosos | Implementado y verificado | `CuotaService` |
| Permisos por rol y plataforma en solo lectura entre gimnasios | Implementado y verificado | controladores y servicios; matriz de autorización |
| Pantallas mínimas (Dueño, personal, Socio) | Implementado; verificado por pruebas de componente | `features/gestion/*`, `features/mi/mi-asistencia.ts` |

## Texto propuesto para 4.5 (implementación)

Se incorporaron dos entidades al dominio. `Asistencia` registra el ingreso de un socio al gimnasio con el instante en UTC, el día local calculado en el servidor (zona `America/Argentina/Buenos_Aires`, la misma de `DiaLocal`), el método (`Qr` o `Manual`), la cuenta del personal que la registró y el origen de los datos. Un índice único parcial sobre `(socio_id, fecha)` entre las filas vigentes garantiza una asistencia por socio y día; el servicio consulta antes de insertar y, si dos registros simultáneos chocan con el índice, descarta el suyo y devuelve la fila ganadora, de modo que el registro es idempotente (`yaRegistrada = true`). `CuotaSocio` guarda el período (año y mes), el monto, el vencimiento, la fecha de pago y un estado que solo puede ser `Pendiente` o `Pagada`; una restricción `CHECK` exige que la fecha de pago exista si y solo si la cuota está pagada. La condición de "vencida" o "morosa" no se guarda: se deriva al consultar como "pendiente con vencimiento anterior al día local de hoy" (el mismo día del vencimiento todavía no está vencida).

Ambas tablas llevan filtros globales: el personal ve las filas de su gimnasio, el Socio las propias y el Administrador de la Plataforma las de todos los gimnasios, porque no son datos de salud. La escritura de la plataforma se rechaza en los atributos de los controladores y en los servicios. Para mostrar el nombre del socio en los listados de gestión, el repositorio proyecta solo nombre y apellido sin el filtro de socios (que excluye a la plataforma porque la ficha tiene datos de salud); las filas de asistencia y de cuota ya vienen filtradas por gimnasio, y ninguna columna de salud sale de esa consulta.

El QR contiene un token firmado de corta duración. El servidor arma 25 bytes (versión, identificador del socio y vencimiento en segundos Unix), les agrega un HMAC-SHA256 truncado a 12 bytes con una clave de configuración (`Asistencia:ClaveQr`, que no está en el repositorio) y codifica el resultado en Base32 (60 caracteres en mayúsculas y dígitos 2 a 7). La duración por defecto es de 120 segundos. La PWA del Socio dibuja el QR en el cliente con la biblioteca `qrcode-generator` (versión fija 2.0.4, licencia MIT, sin dependencias) como un único trazo SVG, muestra el código en bloques de cuatro caracteres y pide uno nuevo cuando vence. La pantalla del personal ofrece la cámara solo si el navegador implementa `BarcodeDetector`; siempre ofrece el campo para tipear el código (se ignoran guiones, espacios y minúsculas) y la elección manual del socio. El token no incluye el gimnasio: quien lo valida solo encuentra al socio si pertenece a su propio gimnasio, por el filtro global; en caso contrario se responde 404 sin revelar que existe.

El Dueño genera las cuotas de un período indicando monto y vencimiento: se crean para los socios con estado administrativo `Activo` que todavía no tienen cuota en ese período, de modo que repetir la operación no duplica (además, un índice único parcial sobre `(socio_id, anio, mes)` lo impide en la base). El pago se registra a mano, con la fecha del día o una anterior, y queda en la auditoría junto con la generación. No hay pasarela de pago.

## Decisiones técnicas

- **D-11-1. Nombre del socio para la plataforma.** La regla 3 del pedido exige que el Administrador de la Plataforma vea todo lo que no sea dato de salud. Las pantallas de asistencia y cuotas muestran nombre y apellido del socio también a la plataforma, por proyección explícita de esas dos columnas (`AsistenciaCuotaRepository.cs`, `NombresGestion`). No se modificó el filtro global de `Socio` ni se abrió ningún otro endpoint de socios a la plataforma.
- **D-11-2. Plataforma sin gimnasio indicado = todos.** En los listados de asistencia y cuotas, si la plataforma no indica `gimnasioId` se devuelven los de todos los gimnasios (pedido: "solo lectura entre gimnasios"); si lo indica, se valida que exista (404 si no). Para el Dueño y el Personal Trainer se reutiliza `GimnasioDestino`: otro gimnasio responde 404.
- **D-11-3. Personal Trainer y asistencia.** El Personal Trainer registra la asistencia de cualquier socio de su gimnasio (no solo de sus asignados), porque el registro de ingreso es una tarea de recepción y no involucra datos de salud. En la pantalla, la lista para el registro manual es la que ya devuelve `GET /api/Socio` para su rol (sus asignados); con el QR o el código puede registrar a cualquier socio del gimnasio.
- **D-11-4. Estado administrativo `Inactivo`.** No impide registrar asistencia (el estado es informativo desde la 9a-2b), pero sí excluye al socio de la generación de cuotas, que el pedido limita a socios activos.
- **D-11-5. QR deshabilitado sin clave.** Si `Asistencia:ClaveQr` no tiene al menos 32 bytes, los endpoints del QR responden 400 con un mensaje claro y el registro manual sigue disponible. Se eligió así, y no un error al arrancar como con `Jwt:Key`, para no impedir el uso del resto de la API en un entorno sin la clave. Las pruebas de integración usan una clave de prueba fija (`GymBroApiFactory.ClaveQrDePrueba`), que no es un secreto.
- **D-11-6. Formato compacto del token.** Base32 en lugar de un JWT para que el código entre en el modo alfanumérico del QR (un QR más chico y fácil de leer) y se pueda tipear. Aun así son 60 caracteres: ver DV-16.
- **D-11-7. Dependencia nueva.** `qrcode-generator@2.0.4` (publicada el 07/08/2025, MIT, sin dependencias), fijada sin rango. Es necesaria porque el pedido exige dibujar el QR en el cliente; escribir un codificador QR propio agregaría más código a mantener.
- **D-11-8. Origen de los datos.** Ambas tablas llevan `origen_datos` (por defecto `Real`) para que el generador sintético de la 7e marque sus filas como `Sintetico`.

## Texto propuesto para 4.6 (verificación)

Pruebas unitarias nuevas (`GymBro.Tests/AsistenciaCuotasTests.cs`, 13): token vigente, formato Base32 de 60 caracteres aceptado con guiones y minúsculas, vencimiento exacto en el segundo límite, firma inválida con otra clave o con un carácter alterado, textos malformados, QR deshabilitado sin clave de 32 bytes y morosidad derivada en cuatro casos (vencida ayer, vence hoy, futura y pagada).

Pruebas de integración nuevas contra PostgreSQL descartable (`GymBro.Tests.Integration/AsistenciaCuotasTests.cs`): flujo completo del QR con idempotencia (el segundo registro por QR y uno manual devuelven la misma fila; queda una sola en la base); código alterado, vencido, firmado con otra clave o vacío rechazado con 400 sin efectos; código tipeado en bloques y minúsculas aceptado; socio sin gimnasio sin QR; aislamiento entre gimnasios (el personal de otro gimnasio recibe 404 al validar un QR ajeno, al registrar manualmente un socio ajeno y al pedir la lista de otro gimnasio, y su lista no contiene filas ajenas); la plataforma lee entre gimnasios y no registra (403); seis registros simultáneos del mismo socio dejan una sola fila y una sola respuesta "nueva"; generación de cuotas solo para activos y sin duplicados al repetirla; morosidad derivada (vencida ayer sí, vence hoy no, el pago la quita y un segundo pago responde 400; la base solo contiene los estados `Pendiente` y `Pagada`); aislamiento de cuotas (otro Dueño recibe 404 al leer o pagar cuotas ajenas; el Socio ve solo las suyas; la plataforma lee las de ambos gimnasios y recibe 403 al generar o pagar; el Personal Trainer recibe 403); validación de datos inválidos con 400; y reversibilidad de la migración (`Down` hasta `Etapa7d_ProtocoloPiernas` quita las dos tablas y `Up` las vuelve a crear). La matriz de autorización suma las celdas de los once endpoints nuevos para los cinco perfiles.

Pruebas del cliente nuevas: rutas por rol (`app.routes.spec.ts`), trazado del QR (`core/util/qr.spec.ts`) y pantallas (`features/gestion/asistencia-cuotas.spec.ts`: QR dibujado y estado de cuotas del Socio; sin `BarcodeDetector` no se ofrece la cámara y siempre el código; registro por código y por elección manual con aviso de "ya registrado"; la plataforma ve el selector de gimnasios y no el registro; el Dueño ve pendientes y vencidas y registra un pago; la vista de morosos; la plataforma lee sin generar ni pagar).

## Cifras de la verificación

Sobre el commit `468872d` (código de `8e846fe` más la corrección de dos advertencias de nulabilidad en las pruebas nuevas; `etapa11-*.txt`):

| Verificación | Línea base (`etapaCIERRE-base`) | Etapa 11 |
|---|---|---|
| `dotnet build` | 0 errores, 1 advertencia (CS2008 de `GymBro.IA.csproj`, previa) | 0 errores, la misma advertencia previa |
| Pruebas unitarias | 342 | 355 (+13) |
| Pruebas de integración | 469 | 536 (+67: pruebas de asistencia y cuotas, celdas nuevas de la matriz y configuración) |
| `ng build` | correcto | correcto |
| Pruebas del cliente | 229 (tras la 8e) | 250 (+21) |
| `npm audit --omit=dev` | — | 0 vulnerabilidades (`etapa11-npm-audit.txt`) |
| SQL de la migración (Up y Down, sin conexión) | — | `etapa11-migracion.sql.txt`, códigos de salida 0 y 0 |

## Desvíos y limitaciones

- **DV-16 (lectura del QR).** La lectura con cámara depende de `BarcodeDetector`, que no está en todos los navegadores (por ejemplo, no está disponible de forma general en Firefox ni en Safari de escritorio al momento de escribir este informe; no se verificó navegador por navegador). Sin esa interfaz, la pantalla lo indica y ofrece tipear el código o elegir al socio. Tipear 60 caracteres es incómodo: en la práctica, sin cámara, el registro manual por elección del socio es la alternativa. La lectura con cámara real **no se verificó** (solo pruebas de componente que simulan la presencia o ausencia de la interfaz).
- **Clave del QR.** Para usar el QR en el entorno local hay que configurar `Asistencia:ClaveQr` (por ejemplo, `dotnet user-secrets set "Asistencia:ClaveQr" "<al menos 32 caracteres>" --project GymBro.Api`). No se configuró en la máquina del desarrollador desde Claude Code.
- **Reloj del cliente.** La cuenta regresiva del QR usa el reloj del celular; si está desfasado, el aviso puede no coincidir con el vencimiento real, que siempre decide el servidor.

## Pendientes

- Aplicar la migración en `gymbro_db` (lo hace el desarrollador al levantar la API).
- Probar la lectura con cámara en los celulares del gimnasio de la Personal Trainer.

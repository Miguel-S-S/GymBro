using System.Text;
using System.Threading.RateLimiting;
using GymBro.Api.Infrastructure;
using GymBro.Api.Security;
using GymBro.Application.Abstractions;
using GymBro.Application.Services;
using GymBro.Domain.Repositories;
using GymBro.Domain.Security;
using GymBro.Infrastructure.ExternalServices;
using GymBro.Infrastructure.Persistence;
using GymBro.Infrastructure.Persistence.Seeders;
using GymBro.Infrastructure.Repositories;
using GymBro.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IIaService, IaService>(client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:8000");
});

// Add services to the container.
builder.Services.AddDbContext<GymBroDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
    o => o.UseVector())
    .UseSnakeCaseNamingConvention());

// inyeccion de dependencias
builder.Services.AddScoped<ISocioRepository, SocioRepository>();
builder.Services.AddScoped<ISocioService, SocioService>();
builder.Services.AddScoped<IMedicionRepository, MedicionRepository>();
builder.Services.AddScoped<IMedicionService, MedicionService>();
builder.Services.AddScoped<IEjercicioRepository, EjercicioRepository>();
builder.Services.AddScoped<IEjercicioCatalogoService, EjercicioCatalogoService>();
builder.Services.AddScoped<IPrescripcionEntrenamientoRepository, PrescripcionEntrenamientoRepository>();
builder.Services.AddScoped<ISesionEntrenamientoRepository, SesionEntrenamientoRepository>();
builder.Services.AddScoped<ISesionEntrenamientoService, SesionEntrenamientoService>();
builder.Services.AddScoped<IBuscadorSemanticoService, GymBro.Infrastructure.Services.BuscadorSemanticoService>();

// --- Identidad, multi-tenant y acceso ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, HttpUsuarioActual>();
builder.Services.AddSingleton<IHasheadorContrasena, HasheadorContrasena>();
builder.Services.AddScoped<IGeneradorToken, GeneradorTokenJwt>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IGimnasioRepository, GimnasioRepository>();
builder.Services.AddScoped<IVinculacionRepository, VinculacionRepository>();
builder.Services.AddScoped<IAsignacionRepository, AsignacionRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISocioAccessService, SocioAccessService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGimnasioService, GimnasioService>();
builder.Services.AddScoped<IPersonalService, PersonalService>();
builder.Services.AddScoped<IVinculacionService, VinculacionService>();
builder.Services.AddScoped<IRevisionPrescripcionService, RevisionPrescripcionService>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Seccion));
var jwt = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Configure Jwt:Key con al menos 32 caracteres (user-secrets o variable de entorno Jwt__Key).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // se usan los nombres cortos: sub, role, gimnasio_id, socio_id
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimNames.Name,
            RoleClaimType = ClaimNames.Role
        };
    });

// Seguro por defecto: todo endpoint exige sesion salvo los marcados con [AllowAnonymous].
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Freno a fuerza bruta sobre login/registro.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddExceptionHandler<ManejadorExcepciones>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Bloque de Sembrado de Datos
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<GymBroDbContext>();

    await context.Database.MigrateAsync();

    // Ruta a los archivos
    string datasetPath = @"C:\Users\miguel\source\repos\GymBro\GymBro.IA\gymbro_datasets";

    // --- NUEVOS LOGS DE DEPURACION ---
    Console.WriteLine("========================================");
    Console.WriteLine($"[DEBUG] Buscando datasets en: {datasetPath}");
    Console.WriteLine($"[DEBUG] ¿La carpeta existe?: {Directory.Exists(datasetPath)}");
    Console.WriteLine("========================================");

    await DataSeeder.SeedAsync(context, datasetPath);

    await AccesoSeeder.SeedSuperAdministradorAsync(context, services.GetRequiredService<IHasheadorContrasena>(),
        app.Configuration["SuperAdmin:Email"], app.Configuration["SuperAdmin:Password"]);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
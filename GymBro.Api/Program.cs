using GymBro.Application.Services;
using GymBro.Domain.Repositories;
using GymBro.Infrastructure.ExternalServices;
using GymBro.Infrastructure.Persistence;
using GymBro.Infrastructure.Persistence.Seeders;
using GymBro.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IIaService, IaService>(client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:8000");
});


// Add services to the container.
builder.Services.AddDbContext<GymBroDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
    o => o.UseVector())//habilita el mapeo de vector en la base de datos PostgreSQL
    .UseSnakeCaseNamingConvention()); 

//inyeccion de depenpenderncias
builder.Services.AddScoped<ISocioRepository, SocioRepository>();
builder.Services.AddScoped<ISocioService, SocioService>();
builder.Services.AddScoped<IMedicionRepository, MedicionRepository>();
builder.Services.AddScoped<IMedicionService, MedicionService>();
builder.Services.AddScoped<IBuscadorSemanticoService, GymBro.Infrastructure.Services.BuscadorSemanticoService>();

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

    // --- NUEVOS LOGS DE DEPURACIÓN ---
    Console.WriteLine("========================================");
    Console.WriteLine($"[DEBUG] Buscando datasets en: {datasetPath}");
    Console.WriteLine($"[DEBUG] ¿La carpeta existe?: {Directory.Exists(datasetPath)}");
    Console.WriteLine("========================================");

    await DataSeeder.SeedAsync(context, datasetPath);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

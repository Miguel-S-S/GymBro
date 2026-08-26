using GymBro.Domain.Repositories;
using GymBro.Infrastructure.Persistence;
using GymBro.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using GymBro.Application.Services;
using GymBro.Infrastructure.ExternalServices;

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

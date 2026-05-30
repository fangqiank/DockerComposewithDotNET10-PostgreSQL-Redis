using Microsoft.EntityFrameworkCore;
using Products.Api.Data;
using Products.Api.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core + PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

// Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Cache"));

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-apply migrations in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// OpenAPI + Scalar
app.MapOpenApi();
app.MapScalarApiReference();

// Product endpoints
app.MapProductEndpoints();

app.Run();

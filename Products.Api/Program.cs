using Microsoft.EntityFrameworkCore;
using Products.Api.Data;
using Products.Api.Endpoints;
using Products.Api.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core + PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

// Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Cache"));

// TimeProvider for testable time operations
builder.Services.AddSingleton(TimeProvider.System);

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Global exception handler
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

// API Key authentication
app.UseApiKeyAuthentication();

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

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Products.Api.Cache;
using Products.Api.Data;
using Products.Api.Models;

namespace Products.Api.Endpoints;

public static class ProductEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static RouteGroupBuilder MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products");

        // GET /products - list all (cached)
        group.MapGet("/", async (AppDbContext db, IDistributedCache cache, CancellationToken ct) =>
        {
            var cached = await cache.GetStringAsync(CacheKeys.AllProducts, ct);
            if (cached is not null)
                return Results.Text(cached, "application/json");

            var products = await db.Products
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(ct);

            var json = JsonSerializer.Serialize(products, JsonOptions);
            await cache.SetStringAsync(CacheKeys.AllProducts, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }, ct);

            return Results.Text(json, "application/json");
        });

        // GET /products/{id} - get by id (cached)
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, IDistributedCache cache, CancellationToken ct) =>
        {
            var cacheKey = CacheKeys.ProductById(id);
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
                return Results.Text(cached, "application/json");

            var product = await db.Products.FindAsync([id], ct);
            if (product is null)
                return Results.NotFound();

            var json = JsonSerializer.Serialize(product, JsonOptions);
            await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }, ct);

            return Results.Text(json, "application/json");
        });

        // POST /products - create
        group.MapPost("/", async (CreateProductRequest request, AppDbContext db, IDistributedCache cache, CancellationToken ct) =>
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Stock = request.Stock,
                Category = request.Category,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            await InvalidateCache(cache, product.Id, ct);

            return Results.Created($"/products/{product.Id}", product);
        });

        // PUT /products/{id} - update
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, AppDbContext db, IDistributedCache cache, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null)
                return Results.NotFound();

            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.Stock = request.Stock;
            product.Category = request.Category;
            product.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            await InvalidateCache(cache, product.Id, ct);

            return Results.NoContent();
        });

        // DELETE /products/{id} - soft delete
        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, IDistributedCache cache, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null)
                return Results.NotFound();

            product.IsDeleted = true;
            product.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            await InvalidateCache(cache, product.Id, ct);

            return Results.NoContent();
        });

        return group;
    }

    private static async Task InvalidateCache(IDistributedCache cache, Guid productId, CancellationToken ct)
    {
        await cache.RemoveAsync(CacheKeys.ProductById(productId), ct);
        await cache.RemoveAsync(CacheKeys.AllProducts, ct);
    }
}

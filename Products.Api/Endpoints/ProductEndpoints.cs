using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Products.Api.Cache;
using Products.Api.Data;
using Products.Api.Models;

namespace Products.Api.Endpoints;

public static class ProductEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RouteGroupBuilder MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products");

        // GET /products - list all (cached, paginated)
        group.MapGet("/", async (
            int? page,
            int? pageSize,
            AppDbContext db,
            IDistributedCache cache,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var p = Math.Max(page ?? 1, 1);
            var ps = Math.Clamp(pageSize ?? 20, 1, 100);

            var cacheKey = $"{CacheKeys.AllProducts}:{p}:{ps}";
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
            {
                logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
                var cachedProducts = JsonSerializer.Deserialize<List<Product>>(cached, JsonOptions);
                return Results.Ok(cachedProducts);
            }

            logger.LogDebug("Cache miss for {CacheKey}, querying database", cacheKey);

            var products = await db.Products
                .OrderByDescending(p => p.CreatedAt)
                .Skip((p - 1) * ps)
                .Take(ps)
                .ToListAsync(ct);

            var json = JsonSerializer.Serialize(products, JsonOptions);
            await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }, ct);

            return Results.Ok(products);
        });

        // GET /products/{id} - get by id (cached)
        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            IDistributedCache cache,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var cacheKey = CacheKeys.ProductById(id);
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
            {
                logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
                var cachedProduct = JsonSerializer.Deserialize<Product>(cached, JsonOptions);
                return Results.Ok(cachedProduct);
            }

            logger.LogDebug("Cache miss for {CacheKey}, querying database", cacheKey);

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
                return Results.NotFound(new { error = "Product not found" });

            var json = JsonSerializer.Serialize(product, JsonOptions);
            await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }, ct);

            return Results.Ok(product);
        });

        // POST /products - create
        group.MapPost("/", async (
            CreateProductRequest request,
            AppDbContext db,
            IDistributedCache cache,
            TimeProvider timeProvider,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var validationErrors = ValidateCreateRequest(request);
            if (validationErrors.Count > 0)
                return Results.ValidationProblem(validationErrors.ToDictionary(e => e.Field, e => new[] { e.Message }));

            var now = timeProvider.GetUtcNow();
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Stock = request.Stock,
                Category = request.Category,
                IsDeleted = false,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime
            };

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created product {ProductId} - {ProductName}", product.Id, product.Name);

            await InvalidateCache(cache, product.Id, ct);

            return Results.Created($"/products/{product.Id}", product);
        });

        // PUT /products/{id} - update
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProductRequest request,
            AppDbContext db,
            IDistributedCache cache,
            TimeProvider timeProvider,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var validationErrors = ValidateUpdateRequest(request);
            if (validationErrors.Count > 0)
                return Results.ValidationProblem(validationErrors.ToDictionary(e => e.Field, e => new[] { e.Message }));

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
                return Results.NotFound(new { error = "Product not found" });

            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.Stock = request.Stock;
            product.Category = request.Category;
            product.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Updated product {ProductId} - {ProductName}", product.Id, product.Name);

            await InvalidateCache(cache, product.Id, ct);

            return Results.NoContent();
        });

        // DELETE /products/{id} - soft delete
        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            IDistributedCache cache,
            TimeProvider timeProvider,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
                return Results.NotFound(new { error = "Product not found" });

            product.IsDeleted = true;
            product.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Soft-deleted product {ProductId} - {ProductName}", product.Id, product.Name);

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

    private static List<(string Field, string Message)> ValidateCreateRequest(CreateProductRequest request)
    {
        var errors = new List<(string Field, string Message)>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(("Name", "Name is required."));
        else if (request.Name.Length > 200)
            errors.Add(("Name", "Name must be at most 200 characters."));

        if (request.Description?.Length > 2000)
            errors.Add(("Description", "Description must be at most 2000 characters."));

        if (request.Price <= 0)
            errors.Add(("Price", "Price must be greater than 0."));

        if (request.Stock < 0)
            errors.Add(("Stock", "Stock must be non-negative."));

        if (string.IsNullOrWhiteSpace(request.Category))
            errors.Add(("Category", "Category is required."));
        else if (request.Category.Length > 100)
            errors.Add(("Category", "Category must be at most 100 characters."));

        return errors;
    }

    private static List<(string Field, string Message)> ValidateUpdateRequest(UpdateProductRequest request)
    {
        var errors = new List<(string Field, string Message)>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(("Name", "Name is required."));
        else if (request.Name.Length > 200)
            errors.Add(("Name", "Name must be at most 200 characters."));

        if (request.Description?.Length > 2000)
            errors.Add(("Description", "Description must be at most 2000 characters."));

        if (request.Price <= 0)
            errors.Add(("Price", "Price must be greater than 0."));

        if (request.Stock < 0)
            errors.Add(("Stock", "Stock must be non-negative."));

        if (string.IsNullOrWhiteSpace(request.Category))
            errors.Add(("Category", "Category is required."));
        else if (request.Category.Length > 100)
            errors.Add(("Category", "Category must be at most 100 characters."));

        return errors;
    }
}

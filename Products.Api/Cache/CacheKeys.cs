namespace Products.Api.Cache;

public static class CacheKeys
{
    public const string AllProducts = "products:all";
    public static string ProductById(Guid id) => $"products:{id}";
}

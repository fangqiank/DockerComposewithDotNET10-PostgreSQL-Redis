# Products API / 产品 API

A containerized .NET 10 Minimal API for product CRUD operations, backed by PostgreSQL 18 and Redis 8.

基于 .NET 10 Minimal API 的产品管理 CRUD 服务，使用 PostgreSQL 18 持久化 + Redis 8 缓存。

![Architecture](products-architecture.svg)

## Tech Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET | 10 |
| Database | PostgreSQL (Alpine) | 18 |
| Cache | Redis (Alpine) | 8 |
| ORM | EF Core + Npgsql | 10 |
| Cache Provider | StackExchange.Redis + IDistributedCache | 2.x |
| API Docs | Scalar OpenAPI | 2.x |
| Container Orchestration | Docker Compose | - |

## Architecture

Three services on a shared Docker bridge network:

- **products.api** — .NET 10 Minimal API (ports 5000/5001 → 8080/8081). Multi-stage Docker build. Waits for DB health check before starting.
- **products.database** — PostgreSQL 18 Alpine. Data persisted via named volume `database-data`.
- **products.cache** — Redis 8 Alpine with password auth. Data persisted via named volume `cache-data`.

### Cache Strategy

Cache-aside pattern with `IDistributedCache`:

| Operation | Cache Key | TTL | Behavior |
|-----------|-----------|-----|----------|
| GET /products | `products:all` | 5 min | Read cache first, miss → query DB → cache result |
| GET /products/{id} | `products:{id}` | 5 min | Read cache first, miss → query DB → cache result |
| POST /products | — | — | Insert DB, invalidate `products:all` |
| PUT /products/{id} | — | — | Update DB, invalidate `products:{id}` + `products:all` |
| DELETE /products/{id} | — | — | Soft delete (IsDeleted=true), invalidate cache |

### Soft Delete

EF Core global query filter `HasQueryFilter(e => !e.IsDeleted)` ensures deleted products are excluded from all queries automatically.

## Quick Start

```bash
# Clone and start all services
docker compose up -d

# Rebuild after code changes
docker compose up -d --build

# Stop all services
docker compose down

# Stop and remove volumes
docker compose down -v
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Scalar Docs: `http://localhost:5000/scalar/v1`

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/products` | List all products (cached, ordered by newest first) |
| `GET` | `/products/{id}` | Get product by ID (cached) |
| `POST` | `/products` | Create a new product |
| `PUT` | `/products/{id}` | Update a product |
| `DELETE` | `/products/{id}` | Soft delete a product |

## Example Requests

### Create a product

```bash
curl -X POST http://localhost:5000/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Mechanical Keyboard",
    "description": "Cherry MX Brown switches, RGB backlight",
    "price": 149.99,
    "stock": 50,
    "category": "Peripherals"
  }'
```

### List all products

```bash
curl http://localhost:5000/products
```

### Get a single product

```bash
curl http://localhost:5000/products/{id}
```

### Update a product

```bash
curl -X PUT http://localhost:5000/products/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Mechanical Keyboard V2",
    "description": "Updated model with hot-swap sockets",
    "price": 169.99,
    "stock": 30,
    "category": "Peripherals"
  }'
```

### Delete a product (soft delete)

```bash
curl -X DELETE http://localhost:5000/products/{id}
```

## Product Entity

| Field | Type | Description |
|-------|------|-------------|
| Id | `Guid` | Primary key |
| Name | `string` | Product name (max 200) |
| Description | `string` | Product description (max 2000) |
| Price | `decimal` | Price (precision 18,2) |
| Stock | `int` | Available quantity |
| Category | `string` | Product category (max 100, indexed) |
| IsDeleted | `bool` | Soft delete flag |
| CreatedAt | `DateTime` | Creation timestamp (UTC) |
| UpdatedAt | `DateTime` | Last update timestamp (UTC) |

## Project Structure

```
Products.Api/
├── Cache/
│   └── CacheKeys.cs           # Cache key constants
├── Data/
│   └── AppDbContext.cs         # EF Core DbContext + soft delete filter
├── Endpoints/
│   └── ProductEndpoints.cs     # CRUD route group with caching
├── Migrations/                 # EF Core migrations
├── Models/
│   └── Product.cs              # Entity + request DTOs
├── Dockerfile                  # Multi-stage Docker build
├── Program.cs                  # Startup configuration
└── Products.Api.csproj         # NuGet packages
```

## Configuration

Environment variables injected via `docker-compose.yml` (see `.env` file):

| Variable | Description | Default |
|----------|-------------|---------|
| `DB_DATABASE` | PostgreSQL database name | `products` |
| `DB_USER` | PostgreSQL username | `postgres` |
| `DB_PASSWORD` | PostgreSQL password | `postgres` |
| `CACHE_PASSWORD` | Redis requirepass | `STRONG_PASSWORD-123` |

Connection strings are injected as:
- `ConnectionStrings__Database` → PostgreSQL host, port, db, user, password
- `ConnectionStrings__Cache` → Redis host, port, password

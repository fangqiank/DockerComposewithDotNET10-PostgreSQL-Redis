FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Products.Api/Products.Api.csproj", "Products.Api/"]
RUN dotnet restore "Products.Api/Products.Api.csproj"

COPY Products.Api/ Products.Api/
RUN dotnet publish "Products.Api/Products.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Products.Api.dll"]

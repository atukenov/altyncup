# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY backend/src/Yurt.Domain/Yurt.Domain.csproj src/Yurt.Domain/
COPY backend/src/Yurt.Application/Yurt.Application.csproj src/Yurt.Application/
COPY backend/src/Yurt.Infrastructure/Yurt.Infrastructure.csproj src/Yurt.Infrastructure/
COPY backend/src/Yurt.WebApi/Yurt.WebApi.csproj src/Yurt.WebApi/

# Restore dependencies
RUN dotnet restore src/Yurt.WebApi/Yurt.WebApi.csproj

# Copy all source code
COPY backend/src . /src/src/

# Build
RUN dotnet build -c Release --no-restore src/Yurt.WebApi/Yurt.WebApi.csproj

# Publish
RUN dotnet publish -c Release --no-build -o /app/publish src/Yurt.WebApi/Yurt.WebApi.csproj

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Expose port (Railway will set PORT env var, but we default to 8080)
EXPOSE 8080

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/swagger/index.html || exit 1

# Run the app
ENTRYPOINT ["dotnet", "Yurt.WebApi.dll"]

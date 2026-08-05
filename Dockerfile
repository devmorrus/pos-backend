# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files first to restore dependencies (leverage docker layer cache)
COPY MorrusPOS.sln ./
COPY src/MorrusPOS.Domain/MorrusPOS.Domain.csproj src/MorrusPOS.Domain/
COPY src/MorrusPOS.Application/MorrusPOS.Application.csproj src/MorrusPOS.Application/
COPY src/MorrusPOS.Infrastructure/MorrusPOS.Infrastructure.csproj src/MorrusPOS.Infrastructure/
COPY src/MorrusPOS.Api/MorrusPOS.Api.csproj src/MorrusPOS.Api/
COPY tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj tests/MorrusPOS.UnitTests/
COPY tests/MorrusPOS.IntegrationTests/MorrusPOS.IntegrationTests.csproj tests/MorrusPOS.IntegrationTests/

RUN dotnet restore

# Copy all source files and publish the API
COPY . .
RUN dotnet publish src/MorrusPOS.Api/MorrusPOS.Api.csproj -c Release -o /app/out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MorrusPOS.Api.dll"]

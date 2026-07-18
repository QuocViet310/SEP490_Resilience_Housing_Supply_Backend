# Stage 1: Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for dependency caching
COPY ["RHS.API/RHS.API.csproj", "RHS.API/"]
COPY ["RHS.Application/RHS.Application.csproj", "RHS.Application/"]
COPY ["RHS.Domain/RHS.Domain.csproj", "RHS.Domain/"]
COPY ["RHS.Infrastructure/RHS.Infrastructure.csproj", "RHS.Infrastructure/"]
RUN dotnet restore "RHS.API/RHS.API.csproj"

# Copy the remaining source files
COPY . .
WORKDIR "/src/RHS.API"
RUN dotnet build "RHS.API.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "RHS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose HTTP port
EXPOSE 8080
ENTRYPOINT ["dotnet", "RHS.API.dll"]

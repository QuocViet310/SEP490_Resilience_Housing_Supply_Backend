# Stage 1: Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Giới hạn MSBuild memory & CPU threads để tránh lỗi 139 (Out of Memory / Segfault trên Render 512MB RAM)
ENV MSBUILDDISABLENODEREUSE=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Copy project files for dependency caching
COPY ["RHS.API/RHS.API.csproj", "RHS.API/"]
COPY ["RHS.Application/RHS.Application.csproj", "RHS.Application/"]
COPY ["RHS.Domain/RHS.Domain.csproj", "RHS.Domain/"]
COPY ["RHS.Infrastructure/RHS.Infrastructure.csproj", "RHS.Infrastructure/"]
RUN dotnet restore "RHS.API/RHS.API.csproj" -maxcpucount:1

# Copy the remaining source files
COPY . .
WORKDIR "/src/RHS.API"
RUN dotnet build "RHS.API.csproj" -c Release -o /app/build -maxcpucount:1

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "RHS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false -maxcpucount:1

# Stage 3: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Cài đặt thư viện native đồ họa & font chữ Linux (bắt buộc cho QuestPDF, SkiaSharp & EPPlus)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libgdiplus \
    fonts-liberation \
    libicu-dev \
    && rm -rf /var/lib/apt-get/lists/*

COPY --from=publish /app/publish .

# Expose HTTP port
EXPOSE 8080
ENTRYPOINT ["dotnet", "RHS.API.dll"]

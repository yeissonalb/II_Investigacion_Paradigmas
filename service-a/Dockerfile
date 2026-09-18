# ==========================================
# Etapa 1: Build y publicación
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar capas de NuGet
COPY ["service-a.csproj", "./"]
RUN dotnet restore "service-a.csproj"

# Copiar el resto del código fuente y publicar
COPY . .
RUN dotnet publish "service-a.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Etapa 2: Runtime final optimizado
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV PORT=3001
ENV ASPNETCORE_URLS=http://+:3001
EXPOSE 3001

ENTRYPOINT ["dotnet", "service-a.dll"]

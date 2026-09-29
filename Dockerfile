# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY ["GimnasioApi.csproj", "./"]
RUN dotnet restore "GimnasioApi.csproj"

# Copiar el resto del código y compilar en modo Release
COPY . .
RUN dotnet publish "GimnasioApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render provee el puerto en la variable de entorno PORT (por defecto 10000 o 8080)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GimnasioApi.dll"]

# ===== STEP 1: BUILD =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia tudo para dentro
COPY . .

# Restaura pacotes
RUN dotnet restore

# Compila e publica em modo Release
RUN dotnet publish -c Release -o /app/publish


# ===== STEP 2: RUNTIME =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render usa a porta 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Nome EXATO do seu DLL
CMD ["dotnet", "Sistema_Suporte.dll"]

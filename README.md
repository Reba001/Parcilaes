Parcilaes

## Ambiente local (.NET 8)

Este repo trae una Web API mínima (`src/Parcilaes.Api`) y su proyecto de tests xUnit
(`tests/Parcilaes.Api.Tests`) como punto de partida para probar en local.

### Requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

Verificá la instalación:
```bash
dotnet --version   # 8.x.x
```

### Restaurar y compilar
```bash
dotnet restore
dotnet build
```

### Correr la API en local
```bash
dotnet run --project src/Parcilaes.Api
```
Por defecto queda escuchando en `https://localhost:7xxx` y `http://localhost:5xxx`
(los puertos exactos están en `src/Parcilaes.Api/Properties/launchSettings.json`).
Con Swagger habilitado en Development, la UI queda en `/swagger`.

Para forzar un puerto fijo:
```bash
ASPNETCORE_URLS="http://localhost:5080" dotnet run --project src/Parcilaes.Api
```

Para hot reload durante desarrollo:
```bash
dotnet watch run --project src/Parcilaes.Api
```

### Correr los tests
```bash
dotnet test
```

### Confiar el certificado HTTPS de desarrollo (una sola vez)
```bash
dotnet dev-certs https --trust
```

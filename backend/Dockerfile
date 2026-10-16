FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/FlexiCore.Api/FlexiCore.Api.csproj backend/src/FlexiCore.Api/
COPY backend/src/FlexiCore.Identity/FlexiCore.Identity.csproj backend/src/FlexiCore.Identity/
COPY backend/src/FlexiCore.SharedKernel/FlexiCore.SharedKernel.csproj backend/src/FlexiCore.SharedKernel/
COPY backend/src/FlexiCore.Tenancy/FlexiCore.Tenancy.csproj backend/src/FlexiCore.Tenancy/
COPY backend/src/FlexiCore.Licensing/FlexiCore.Licensing.csproj backend/src/FlexiCore.Licensing/

RUN dotnet restore backend/src/FlexiCore.Api/FlexiCore.Api.csproj

COPY backend/src/FlexiCore.Api/ backend/src/FlexiCore.Api/
COPY backend/src/FlexiCore.Identity/ backend/src/FlexiCore.Identity/
COPY backend/src/FlexiCore.SharedKernel/ backend/src/FlexiCore.SharedKernel/
COPY backend/src/FlexiCore.Tenancy/ backend/src/FlexiCore.Tenancy/
COPY backend/src/FlexiCore.Licensing/ backend/src/FlexiCore.Licensing/

RUN dotnet publish backend/src/FlexiCore.Api/FlexiCore.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish/ ./

USER $APP_UID
ENTRYPOINT ["dotnet", "FlexiCore.Api.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS migrations-build
WORKDIR /src

COPY backend/src/FlexiCore.Migrations/FlexiCore.Migrations.csproj backend/src/FlexiCore.Migrations/
RUN dotnet restore backend/src/FlexiCore.Migrations/FlexiCore.Migrations.csproj

COPY backend/src/FlexiCore.Migrations/ backend/src/FlexiCore.Migrations/
RUN dotnet publish backend/src/FlexiCore.Migrations/FlexiCore.Migrations.csproj \
    --configuration Release \
    --no-restore \
    --output /app/migrations \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrations
WORKDIR /app

COPY --from=migrations-build /app/migrations/ ./

USER $APP_UID
ENTRYPOINT ["dotnet", "FlexiCore.Migrations.dll"]

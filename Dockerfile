FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY .editorconfig global.json Directory.Build.props Directory.Packages.props Portfolio.sln ./
COPY src/Portfolio.Api/Portfolio.Api.csproj src/Portfolio.Api/
COPY src/Portfolio.Application/Portfolio.Application.csproj src/Portfolio.Application/
COPY src/Portfolio.Infrastructure/Portfolio.Infrastructure.csproj src/Portfolio.Infrastructure/
RUN dotnet restore src/Portfolio.Api/Portfolio.Api.csproj

COPY src/ src/
RUN dotnet publish src/Portfolio.Api/Portfolio.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Portfolio.Api.dll"]

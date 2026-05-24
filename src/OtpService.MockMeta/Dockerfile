FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY OtpService.sln ./
COPY src/OtpService.Core/*.csproj            src/OtpService.Core/
COPY src/OtpService.Infrastructure/*.csproj  src/OtpService.Infrastructure/
COPY src/OtpService.Providers/*.csproj       src/OtpService.Providers/
COPY src/OtpService.Api/*.csproj             src/OtpService.Api/
COPY src/OtpService.Ingestion/*.csproj       src/OtpService.Ingestion/
COPY src/OtpService.Delivery/*.csproj        src/OtpService.Delivery/
COPY src/OtpService.MockMeta/*.csproj        src/OtpService.MockMeta/
COPY tools/OtpService.LoadTest/*.csproj      tools/OtpService.LoadTest/
COPY tests/OtpService.UnitTests/*.csproj         tests/OtpService.UnitTests/
COPY tests/OtpService.IntegrationTests/*.csproj  tests/OtpService.IntegrationTests/

RUN dotnet restore src/OtpService.MockMeta/OtpService.MockMeta.csproj

COPY . .

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish src/OtpService.MockMeta/OtpService.MockMeta.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "OtpService.MockMeta.dll"]

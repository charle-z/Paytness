# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
ARG VERSION=0.1.0
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/Paytness/Paytness.csproj src/Paytness/packages.lock.json src/Paytness/
RUN dotnet restore src/Paytness/Paytness.csproj --locked-mode
COPY src/Paytness/ src/Paytness/
RUN dotnet publish src/Paytness/Paytness.csproj -c Release --no-restore --self-contained false \
    -p:UseAppHost=false -p:Version=$VERSION -p:ContinuousIntegrationBuild=true -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
ARG VERSION=0.1.0
LABEL org.opencontainers.image.title="Paytness" \
      org.opencontainers.image.description="Adversarial reliability testing for REST and webhook payment integrations" \
      org.opencontainers.image.version="$VERSION"
WORKDIR /app
COPY --from=build /out/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "paytness.dll"]

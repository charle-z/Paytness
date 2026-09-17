# syntax=docker/dockerfile:1
# Release runtime image. Build through ./eng/package-oci.sh so the application
# payload is produced by the pinned .NET SDK before Buildx assembles the image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
ARG VERSION=0.1.0
ARG REVISION=unknown
LABEL org.opencontainers.image.title="Paytness" \
      org.opencontainers.image.description="Adversarial reliability testing for REST and webhook payment integrations" \
      org.opencontainers.image.version="$VERSION" \
      org.opencontainers.image.revision="$REVISION"
WORKDIR /app
COPY app/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "paytness.dll"]

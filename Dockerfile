# syntax=docker/dockerfile:1.20

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS build

WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Hevy.Client/Hevy.Client.csproj src/Hevy.Client/packages.lock.json src/Hevy.Client/
COPY src/Hevy.Mcp/Hevy.Mcp.csproj src/Hevy.Mcp/packages.lock.json src/Hevy.Mcp/
RUN dotnet restore src/Hevy.Mcp/Hevy.Mcp.csproj --locked-mode

COPY src/ ./src/
ARG VERSION=0.0.0-dev
ARG REVISION=local
RUN dotnet publish src/Hevy.Mcp/Hevy.Mcp.csproj \
    --configuration Release \
    --no-restore \
    --output /out \
    -p:UseAppHost=false \
    -p:Version=${VERSION} \
    -p:SourceRevisionId=${REVISION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:70d6f993bf715a031f027832a19cfb7f894df66c8b5eb40be0aaee820ad5d119 AS final

ARG VERSION=0.0.0-dev
ARG REVISION=local
ARG SOURCE_URL=local

LABEL org.opencontainers.image.title="hevy-client" \
      org.opencontainers.image.description="Local-first Model Context Protocol server for the official Hevy API" \
      org.opencontainers.image.source="${SOURCE_URL}" \
      org.opencontainers.image.revision="${REVISION}" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.licenses="MIT"

WORKDIR /app
ENV HOME=/tmp \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=app:app /out/ ./

USER app
ENTRYPOINT ["dotnet", "Hevy.Mcp.dll"]

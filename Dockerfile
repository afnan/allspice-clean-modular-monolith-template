# syntax=docker/dockerfile:1
# Multi-stage build for the ApiGateway — the single deployable host (modules register into it).
# Build context is the repository root.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central build/version config first so a dependency-graph change invalidates the restore layer.
COPY Directory.Build.props Directory.Packages.props ./

# Copy the full source. (For very large solutions, copy only *.csproj first + `dotnet restore` to
# cache the restore layer, then copy the rest — omitted here for simplicity.)
COPY . .

ARG BUILD_CONFIGURATION=Release
RUN dotnet restore AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj
RUN dotnet publish AllSpice.CleanModularMonolith.ApiGateway/AllSpice.CleanModularMonolith.ApiGateway.csproj \
    -c "$BUILD_CONFIGURATION" -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Kestrel listens on 8080 (the .NET container default). Map/route to this internally; terminate TLS at the
# ingress/load balancer. Health probes: GET /alive (liveness), GET /health (readiness).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# PDF generation (Shared/...Pdf via PuppeteerSharp) needs Chromium + its native deps, which are NOT in this
# base image. If you use the PDF feature in-container, install them, e.g.:
#   RUN apt-get update && apt-get install -y --no-install-recommends \
#       libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libxkbcommon0 libxcomposite1 \
#       libxdamage1 libxfixes3 libxrandr2 libgbm1 libpango-1.0-0 libcairo2 libasound2 fonts-liberation \
#    && rm -rf /var/lib/apt/lists/*
# and point PuppeteerSharp at the system Chromium (or let it download a matching build on first use).

COPY --from=build /app/publish .

# Run as the non-root user provided by the .NET base image.
USER $APP_UID

ENTRYPOINT ["dotnet", "AllSpice.CleanModularMonolith.ApiGateway.dll"]

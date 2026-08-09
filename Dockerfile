# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Manifests first for better layer caching
COPY opencode-oai.slnx ./
COPY src/OpencodeOai/OpencodeOai.csproj src/OpencodeOai/
COPY tests/OpencodeOai.Tests/OpencodeOai.Tests.csproj tests/OpencodeOai.Tests/
RUN dotnet restore src/OpencodeOai/OpencodeOai.csproj -r linux-x64

# Sources
COPY src ./src

RUN dotnet publish src/OpencodeOai/OpencodeOai.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /app

# ── Runtime stage ─────────────────────────────────────────────────────────────
# AOT publish emits a self-contained native binary; use a slim base.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos '' --uid 10001 bridge \
    && apt-get update && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=bridge:bridge /app /app
USER bridge

ENV PORT=5000 \
    ASPNETCORE_URLS=http://0.0.0.0:5000

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:5000/health || exit 1

ENTRYPOINT ["/app/OpencodeOai"]

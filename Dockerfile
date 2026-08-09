# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Native AOT prerequisites: clang, zlib
# https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

# Manifest first for better layer caching
COPY src/OpencodeOai/OpencodeOai.csproj src/OpencodeOai/
RUN dotnet restore src/OpencodeOai/OpencodeOai.csproj -r linux-x64

# Sources
COPY src ./src

RUN dotnet publish src/OpencodeOai/OpencodeOai.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /app

# ── Runtime stage ─────────────────────────────────────────────────────────────
# Chiseled Ubuntu base: tiny, no shell, non-root `app` user pre-baked.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra AS runtime
WORKDIR /app

COPY --from=build --chown=app:app /app /app
USER app

ENV OPENCODE_OAI_PORT=5000 \
    ASPNETCORE_URLS=http://0.0.0.0:5000

EXPOSE 5000

ENTRYPOINT ["/app/OpencodeOai"]

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY src/Bidibip.Plugin.Sdk/Bidibip.Plugin.Sdk.csproj src/Bidibip.Plugin.Sdk/
COPY src/Bidibip/Bidibip.csproj src/Bidibip/
RUN dotnet restore src/Bidibip/Bidibip.csproj --runtime linux-musl-x64

COPY . .
RUN dotnet publish src/Bidibip/Bidibip.csproj \
    -c Release \
    -o /app \
    --runtime linux-musl-x64 \
    --self-contained \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
RUN adduser -D botuser
WORKDIR /app
RUN mkdir -p plugins && chown botuser:botuser plugins
USER botuser
COPY --from=build /app .
ENTRYPOINT ["./Bidibip"]

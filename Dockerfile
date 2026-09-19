# syntax=docker/dockerfile:1

FROM node:24-alpine AS web
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
RUN corepack enable
WORKDIR /src/apps/web
COPY apps/web/package.json apps/web/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY apps/web ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json .editorconfig ./
COPY apps/api/Directory.Build.props apps/api/Directory.Packages.props apps/api/
COPY apps/api/src/Taxonomy.Core/Taxonomy.Core.csproj apps/api/src/Taxonomy.Core/
COPY apps/api/src/Taxonomy.Ingest/Taxonomy.Ingest.csproj apps/api/src/Taxonomy.Ingest/
COPY apps/api/src/Taxonomy.Ingest.Cli/Taxonomy.Ingest.Cli.csproj apps/api/src/Taxonomy.Ingest.Cli/
COPY apps/api/src/Taxonomy.Api/Taxonomy.Api.csproj apps/api/src/Taxonomy.Api/
RUN dotnet restore apps/api/src/Taxonomy.Api \
 && dotnet restore apps/api/src/Taxonomy.Ingest.Cli
COPY apps/api/src apps/api/src
RUN dotnet publish apps/api/src/Taxonomy.Api -c Release -o /out/api --no-restore -p:OpenApiGenerateDocuments=false \
 && dotnet publish apps/api/src/Taxonomy.Ingest.Cli -c Release -o /out/ingest --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app/api
COPY --from=api /out/api ./
COPY --from=api /out/ingest /app/ingest
COPY --from=web /src/apps/web/dist ./wwwroot
COPY data/structure_released.xml /app/data/
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
CMD ["dotnet", "Taxonomy.Api.dll"]

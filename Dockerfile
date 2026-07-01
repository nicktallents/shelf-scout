# syntax=docker/dockerfile:1

FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /src
COPY global.json ./
WORKDIR /src/backend
COPY backend/*.slnx ./
COPY backend/ShelfScout.Api/*.csproj ./ShelfScout.Api/
COPY backend/ShelfScout.Api.Tests/*.csproj ./ShelfScout.Api.Tests/
RUN dotnet restore ShelfScout.slnx
COPY backend/ ./
COPY --from=frontend-build /src/backend/ShelfScout.Api/wwwroot ./ShelfScout.Api/wwwroot
RUN dotnet publish ShelfScout.Api/ShelfScout.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN adduser -D -u 1000 appuser && chown -R 1000:1000 /app
USER 1000
ENTRYPOINT ["dotnet", "ShelfScout.Api.dll"]

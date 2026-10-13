# Stage 1: build the React front end.
FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# Stage 2: publish the API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY api/ClaimDesk.Api/ClaimDesk.Api.csproj api/ClaimDesk.Api/
RUN dotnet restore api/ClaimDesk.Api/ClaimDesk.Api.csproj
COPY api/ClaimDesk.Api/ api/ClaimDesk.Api/
RUN dotnet publish api/ClaimDesk.Api/ClaimDesk.Api.csproj -c Release -o /app --no-restore

# Stage 3: the runtime image. The API serves the built front end from wwwroot.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /src/web/dist ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClaimDesk.Api.dll"]

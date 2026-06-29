FROM mcr.microsoft.com/dotnet/sdk:10.0 AS openapi
WORKDIR /src
COPY outfit_planner_back/ ./outfit_planner_back/
RUN dotnet build outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj \
    /p:OpenApiGenerateDocuments=true \
    /p:OpenApiDocumentsDirectory=/openapi

FROM node:24-alpine AS build
WORKDIR /app
ARG VITE_API_URL=/api
ENV VITE_API_URL=$VITE_API_URL
ENV OUTFIT_PLANNER_OPENAPI_DOCUMENT=/openapi/OutfitPlanner.Api.json
COPY outfit_planner_front/package*.json ./
RUN npm ci
COPY outfit_planner_front/ ./
COPY --from=openapi /openapi /openapi
RUN npm run build

FROM nginx:1.29-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY outfit_planner_front/nginx.conf /etc/nginx/conf.d/default.conf

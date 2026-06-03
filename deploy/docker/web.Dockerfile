FROM node:20-alpine AS build
WORKDIR /src
ARG VITE_API_BASE_URL=http://localhost:5000
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
COPY src/SysAssist.Web/package*.json ./
RUN npm ci
COPY src/SysAssist.Web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY deploy/docker/web.nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 4173

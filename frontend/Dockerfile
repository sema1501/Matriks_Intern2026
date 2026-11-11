# 1. aşama: React uygulamasını derler.
# package-lock.json npm 11 ile oluşturulduğu için Node 24 kullanılır.
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY public ./public
COPY src ./src
# REACT_APP_ değişkenleri npm run build sırasında koda gömülür, çalışırken değiştirilemez.
ARG REACT_APP_API_URL
ENV REACT_APP_API_URL=$REACT_APP_API_URL
RUN npm run build

# 2. aşama: yalnızca derlenmiş statik dosyaları nginx ile sunar.
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/build /usr/share/nginx/html
EXPOSE 80

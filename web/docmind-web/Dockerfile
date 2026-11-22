FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build:prod

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime

ENV API_UPSTREAM=http://api:8080 \
    API_BASE_URL="" \
    APP_ENVIRONMENT=production \
    CSP_CONNECT_SRC="'self'"

COPY --chown=101:101 nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --chown=101:101 nginx/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build --chown=101:101 /app/dist/docmind-web/browser /usr/share/nginx/html

USER root
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-runtime-config.sh \
    && chmod 755 /docker-entrypoint.d/40-runtime-config.sh \
    && chown 101:101 /usr/share/nginx/html/config.json
USER 101

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1

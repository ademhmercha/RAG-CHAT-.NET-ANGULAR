#!/bin/sh
set -eu

printf '{"apiBaseUrl":"%s","environment":"%s"}\n' \
    "${API_BASE_URL:-}" \
    "${APP_ENVIRONMENT:-production}" \
    > /usr/share/nginx/html/config.json

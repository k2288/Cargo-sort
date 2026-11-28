FROM node:22-alpine
WORKDIR /app
COPY package.json ./
COPY src ./src
COPY blocked-words.txt ./
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN apk add --no-cache su-exec && chmod +x /usr/local/bin/docker-entrypoint.sh && mkdir -p /data && chown node:node /data
ENV NODE_ENV=production PORT=8080 DB_PATH=/data/cargo.db UTC_OFFSET=210
VOLUME /data
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8080/health || exit 1
# Starts as root only to make the (mounted) data folder writable, then runs the server as the "node" user.
ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["node", "--disable-warning=ExperimentalWarning", "src/server.js"]

FROM node:22-alpine
WORKDIR /app
COPY package.json ./
COPY src ./src
COPY blocked-words.txt ./
ENV NODE_ENV=production PORT=8080 DB_PATH=/data/cargo.db UTC_OFFSET=210
VOLUME /data
EXPOSE 8080
USER node
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8080/health || exit 1
CMD ["node", "--disable-warning=ExperimentalWarning", "src/server.js"]

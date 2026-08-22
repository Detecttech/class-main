# ==========================================
# Stage 1: Build Teacher Web Portal (React Vite)
# ==========================================
FROM node:20-bookworm-slim AS web-portal-builder
WORKDIR /app/server/web-portal

COPY server/web-portal/package*.json ./
RUN npm install

COPY server/web-portal ./
RUN npm run build

# ==========================================
# Stage 2: Build Server TypeScript
# ==========================================
FROM node:20-bookworm-slim AS server-builder
WORKDIR /app/server

COPY server/package*.json ./
RUN npm install

COPY server/tsconfig.json ./
COPY server/src ./src
RUN npm run build

# ==========================================
# Stage 3: Production Runtime
# ==========================================
FROM node:20-bookworm-slim AS runner

# Install C++ build tools required by native better-sqlite3 bindings
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    make \
    g++ \
    curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy shared assets (character configs, etc.)
COPY shared ./shared

WORKDIR /app/server

# Install production dependencies only (compiles better-sqlite3 for Linux)
COPY server/package*.json ./
RUN npm install --omit=dev && npm cache clean --force

# Copy compiled artifacts from builders
COPY --from=server-builder /app/server/dist ./dist
COPY --from=web-portal-builder /app/server/web-portal/dist ./web-portal/dist

# Copy Unity WebGL build if present
COPY game-client/webgl-build /app/game-client/webgl-build

# Prepare persistent database storage directory
RUN mkdir -p /app/server/data

# Environment configuration
ENV NODE_ENV=production
ENV MODE=wan
ENV PORT=7777
ENV DB_PATH=/app/server/data/quizbattle.db

# Cloud Run defaults to 8080, but app listens on process.env.PORT
EXPOSE 7777 8080

# Persistent volume for SQLite data when running on Compute Engine or Docker
VOLUME ["/app/server/data"]

CMD ["node", "dist/index.js"]

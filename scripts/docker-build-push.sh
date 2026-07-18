#!/usr/bin/env bash
# Build and push linux/amd64 image to Docker Hub.
# Usage: ./scripts/docker-build-push.sh [tag]
# Requires: docker login

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

IMAGE="${DOCKER_IMAGE:-ingluigii/btw-template-pdf}"
TAG="${1:-latest}"
FULL="${IMAGE}:${TAG}"

echo "→ Building ${FULL} (linux/amd64)…"
docker build --platform linux/amd64 -t "${FULL}" .

echo "→ Pushing ${FULL}…"
docker push "${FULL}"

echo "✓ Published ${FULL}"

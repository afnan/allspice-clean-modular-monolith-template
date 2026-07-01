# Deployment

The whole system ships as **one container** — the `ApiGateway` host (modules register into it). `AppHost`
(Aspire) is for **local development only**; it provisions Postgres, Redis, Keycloak, Papercut SMTP, and
Azurite as dev containers and is not a production deployment artifact.

## Build the image

```bash
docker build -t allspice-gateway:latest .
```

(The build context is the repo root; see `Dockerfile`.)

## Run locally against your own infra

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__identitydb="Host=...;Database=identitydb;Username=...;Password=..." \
  -e ConnectionStrings__notificationsdb="Host=...;Database=notificationsdb;Username=...;Password=..." \
  -e ConnectionStrings__messagingdb="Host=...;Database=messagingdb;Username=...;Password=..." \
  allspice-gateway:latest
```

Health probes: `GET /alive` (liveness) and `GET /health` (readiness — fails until DB connectivity +
migrations are healthy).

## Kubernetes

`deploy/k8s/gateway.yaml` is a starting-point Deployment + Service with liveness/readiness probes,
non-root/read-only-root security context, and resource requests/limits. Supply runtime config
(connection strings, Keycloak, Redis, mail keys, blob storage) via the `allspice-gateway-secrets` Secret:

```bash
kubectl create secret generic allspice-gateway-secrets \
  --from-literal=ConnectionStrings__identitydb='...' \
  --from-literal=ConnectionStrings__notificationsdb='...' \
  --from-literal=ConnectionStrings__messagingdb='...'
kubectl apply -f deploy/k8s/
```

Keep `/alive` and `/health` off the public ingress — restrict them to the cluster/probe network.

> **Aspire-native option:** you can also deploy straight from the AppHost with the Aspire CLI / `azd`
> (`aspirate` or `azd up`). The manifest above is the platform-agnostic path.

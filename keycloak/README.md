# Keycloak realm import

`realm-import.json` is an **optional shortcut** for local setup — it creates the realm, an `Identity.Admin`
realm role, two public SPA portal clients (`erp-portal`, `main-website`), and a confidential
`identity-admin` service-account client used by the `KeycloakUserSyncJob`. It is a **starting point**: review
redirect URIs, secrets, and flows before any non-local use.

## Import it

In the Keycloak admin console (dev: <http://localhost:8080>, admin/admin): **Create realm → Resource file →**
select `keycloak/realm-import.json`. Or via the CLI:

```bash
# inside the running keycloak container
/opt/keycloak/bin/kc.sh import --file /path/to/realm-import.json
```

## After import — finish these manually

1. **Service-account roles for sync:** give the `identity-admin` client's service account the
   `realm-management` client roles `view-users` and `query-users` (Clients → identity-admin → Service account
   roles). The sync job needs these to enumerate Keycloak users.
2. **Rotate the secret:** replace `CHANGE_ME_dev_only` on `identity-admin` and set it as
   `Identity:Keycloak:ClientSecret` (user-secrets/env), never in source.
3. **Assign `Identity.Admin`** to whichever users should reach the Identity admin endpoints.

See [`GETTING_STARTED.md`](../GETTING_STARTED.md) for the full walkthrough and the manual alternative.

# Plan: Finalize invoice (docs/plans/finalize-invoice.md)

## Requirements

1. New endpoint `POST /billing/invoices/{id}/finalize` gated by permission
   `billing:invoices.manage` (declared in the Billing permission manifest).
2. Finalizing stamps `FinalizedAtUtc` from the injected clock (`TimeProvider`,
   passed into the aggregate as `nowUtc`).
3. An already-finalized invoice cannot be finalized again (conflict).
4. Unit tests for the finalize use case: success path and conflict path.

## Acceptance criteria

- Finalize returns the updated invoice DTO on success.
- Second finalize attempt returns 409 via the standard failure mapping.

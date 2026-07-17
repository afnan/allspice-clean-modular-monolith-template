# Expected findings for fixtures/seeded.diff

Seeded violations. A passing run catches all of these (any reasonable wording),
flags none of the negative controls, and invents nothing substantive beyond this
list.

| # | Where | Expected finding | Expected grade |
|---|-------|------------------|----------------|
| V1 | InvoiceLine.cs | Primary constructor on an entity; needs private ctor + factory | issue (blocking) |
| V2 | InvoiceLine.cs Reprice | `throw new Exception` in Domain; needs DomainException subtype (Guard.Against) | issue (blocking) |
| V3 | FinalizeInvoiceHandler.cs | Handler depends on BillingDbContext with inline EF/LINQ; use bespoke IInvoiceRepository + InvoiceByIdSpec (both exist per listing) | issue (blocking) |
| V4 | FinalizeInvoiceCommand (via listing) | Mutating command does not implement ITransactional; mutation runs outside TransactionBehavior | issue (blocking) |
| V5 | FinalizeInvoiceHandler.cs | Direct DateTimeOffset.UtcNow; inject TimeProvider, pass nowUtc (plan req 2) | issue (blocking) |
| V6 | FinalizeInvoiceEndpoint.cs | No Policies(PermissionPolicy.For("billing:invoices.manage")) and key missing from BillingPermissionManifest (listing confirms) | issue (blocking), manifest part may be chore (blocking) |
| V7 | FinalizeInvoiceEndpoint.cs | Hand-rolled status switch; use result.ExecuteFailureAsync(HttpContext) | issue (blocking) |
| V8 | GetInvoicesEndpoint.cs | ResponseCache change not in the plan (scope creep) | question |
| V9 | Billing.csproj | Inline Version on PackageReference; move to Directory.Packages.props | issue (blocking) |
| P1 | plan req 4 | No FinalizeInvoiceHandler unit test anywhere (listing confirms) | chore (blocking) |
| P2 | plan criteria | Acceptance criteria (success DTO, 409 on second finalize) have no tests | chore (blocking), may merge with P1 |
| P3 | Billing.csproj | Humanizer dependency not used by the diff and not in the plan | question (optional catch) |

## Negative controls (must NOT be flagged)

| # | Where | Why it is fine |
|---|-------|----------------|
| NC1 | FinalizeInvoiceHandler / endpoint class declarations | Primary constructor on a DI type is the convention |
| NC2 | InvoiceMapper.ToDto reuse in the handler | Correct, mapper reused not duplicated |
| NC3 | Migrations/20260716_AddFinalizedAt.cs | Generated migration; style rules exempt; it is idempotent-safe and cannot crash startup |
| NC4 | FinalizeInvoiceRequest / InvoiceDto types "missing" | Fixture artifact: request/DTO types omitted from the synthetic diff for brevity; a compile-failure finding on them is a fixture false positive |

## Scoring

- Catch rate: V1-V7, V9 plus P1 = 9 required catches (V8 required as question; P2 may merge into P1; P3 optional).
- False positives: NC1-NC4 flagged = fail.
- Invented findings: anything substantive outside this list = review and either fix the skill or add here.

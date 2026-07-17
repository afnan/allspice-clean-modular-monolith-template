---
name: allspice-clean-review
description: Use when reviewing a backend PR or diff in this .NET clean-architecture modular monolith (FastEndpoints, Mediator CQRS, Ardalis, Wolverine, DDD). Triggers: "/allspice-clean-review", "review PR", "review this diff", "check my backend PR", or any pre-merge check. Frontend diffs are out of scope.
---

# Clean-Architecture PR Review (backend)

Review a backend pull request or working diff in three passes: (1) spec/plan
alignment, (2) convention checklist, (3) general correctness. Report findings
graded with Conventional Comments. Read-only by default. Only post inline PR
comments or apply fixes when the user asks for it.

The full rule set lives in **[checklist.md](checklist.md)** in this skill folder.
Read it before reviewing and hand it to every review subagent. Its pillars: Clean
Architecture (dependencies point inward), DDD aggregates with private constructors
and static factories (never primary constructors on entities), CQRS via Mediator
with `ITransactional` mutations, bespoke repositories with Ardalis specifications
(query logic never inline in handlers), one authorization model
(`PermissionPolicy.For` plus the module permission manifest), Wolverine integration
events through the transactional outbox, injected `TimeProvider`, DRY, module
boundaries, and mandatory unit plus architecture tests. Flag speculative
abstraction and premature generality as hard as duplication (KISS/YAGNI).

Frontend diffs are out of scope: stop and say so.

## 0. Review discipline (apply throughout)

- **Scope to the diff.** Only flag lines this PR changed or directly affects. Do
  not review pre-existing code in untouched files. If you spot a serious
  pre-existing bug, mention it once in the summary, not as a line comment.
- **Verify before you flag.** Read the surrounding code and confirm the rule is
  actually broken. A pattern match is a lead, not a verdict. If you cannot confirm
  it from the diff plus context, use a `question` label, not `issue`.
- **Absence claims need proof.** Before flagging anything as *missing* (a handler
  test, a permission-manifest entry, a module registration step), search the whole
  repo for it (Grep/Glob). The diff cannot prove absence. If you cannot search the
  repo, downgrade the finding to `question`.
- **Confidence gate.** Only post findings you are confident are real. When unsure,
  drop it. A wrong comment costs more trust than a missed nitpick. An empty review
  is a valid result. Do not pad findings to look thorough.
- **Skip generated code.** EF migration files, `*.Designer.cs`, and model
  snapshots are exempt from style and convention rules. Only flag a migration for
  the substantive rules: not idempotent, or can crash startup.
- **Verify cited paths.** Before naming a file, constant, or test class in a
  finding, confirm it exists in the repo. Do not cite from memory.
- **The repo docs win.** `AGENTS.md` and `ARCHITECTURE.md` at the repo root are
  the source of truth. Read `AGENTS.md` before reviewing. If it conflicts with
  this skill, follow the repo docs and note the drift so this skill can be
  updated.

## 1. Resolve the diff

Argument may be a PR number (e.g. `/allspice-clean-review 48`) or empty.

- **PR number given:** `gh pr view <N>` for context, `gh pr diff <N>` for the diff.
- **No argument:** review the current branch vs its base. Detect the default
  branch (`git symbolic-ref refs/remotes/origin/HEAD` or
  `gh repo view --json defaultBranchRef`), then `git merge-base HEAD origin/<base>`
  and `git diff <merge-base>...HEAD`. If the branch has no commits ahead, fall
  back to `git diff` plus `git diff --staged`.

Read the full surrounding context of changed files when a hunk is ambiguous. Never
review a hunk in isolation.

### Large diffs (subagent protocol)

If the diff is large (roughly more than 15 files or 1500 changed lines), do not
review it all in one context. Split by module or by layer and dispatch a subagent
per chunk. Subagents do not inherit this skill, so each prompt must include:

1. An instruction to **read `checklist.md` from this skill folder first** (give the
   absolute path), plus the repo's `AGENTS.md`.
2. The chunk's diff (or the file list and base ref to diff themselves).
3. The discipline rules from section 0 (scope to diff, verify before flag,
   absence claims need repo search, skip generated code).
4. The required return format: a list of findings, each with `file`, `line`,
   `label` (Conventional Comments), `blocking` (true/false), `note` (one or two
   short sentences), and `confidence` (confirmed/likely/question).

Merge, dedupe, and grade centrally. Drop any subagent finding that fails the
confidence gate.

## 2. Spec / plan alignment pass

Check that the PR implements what was planned, before checking how it is built.

**Locate the spec or plan, in this order:**

1. A spec, plan, or issue the user points to.
2. The PR body and linked issues (`gh pr view <N> --json body,title`; linked
   issues via closing keywords).
3. Plan documents in the repo touched or referenced by the branch:
   `docs/plans/`, `docs/specs/`, `specs/`, `tasks/`, `.claude/plans/`.
4. Commit messages on the branch as a last resort.

If none is found, write one line in the summary: "No spec or plan found; alignment
pass skipped." That is not a finding against the PR.

**With a spec in hand, check four things:**

- **Coverage.** Every requirement and acceptance criterion is implemented, tested,
  or explicitly deferred in the PR description. A silently missing requirement is
  `chore (blocking)`, quoting the spec line it comes from.
- **Scope creep.** Substantive changes not traceable to the spec (a new endpoint,
  a schema change, a dependency) get a `question`: ask the author's intent, do not
  assume it is wrong. Trivial drive-by cleanups are fine, note them at most once.
- **Tests match criteria.** Each acceptance criterion maps to at least one test.
  A criterion with no test is `chore (blocking)`. Apply the absence-claim rule:
  search the test projects first.
- **Language match.** Domain names in code match the spec's language (entity,
  event, and permission-key names). Silent renames of spec concepts are
  `question`.

## 3. Convention checklist

Read **checklist.md** and apply every section, layer by layer: Domain,
repositories and specifications, Application, DRY, Infrastructure, messaging, Api,
authorization, identity and security, module boundaries, modern C#, tests. Run the
`Recipe:` searches in the checklist rather than eyeballing where a rule is
mechanically detectable.

## 4. General pass

Correctness bugs, null/edge cases, missing `await`, N+1 queries,
resource/connection leaks, auth bypass, injection (including unescaped ILIKE
patterns), race conditions, and swallowed exceptions. Guideline adherence is the
floor, not the ceiling.

## 5. Reporting and inline PR comments

### Grading (industry standard: Conventional Comments)

Grade every finding with a **Conventional Comments** label plus a blocking
decoration. Do **not** use color circles, emoji, or the words high/medium/low.

- `issue (blocking)` — a real defect or a hard-rule violation. Must be fixed before
  merge.
- `issue (non-blocking)` — a real problem that can be a fast follow-up.
- `suggestion (blocking)` / `suggestion (non-blocking)` — a proposed improvement.
- `question` — you need the author to clarify intent before judging.
- `nitpick (non-blocking)` — trivial, author's choice.
- `chore (blocking)` — a required task the PR is missing (add a test, register the
  module, declare the permission key in the manifest).

`blocking` means it must be resolved before merge. `non-blocking` means it can
merge or be a follow-up. That pair replaces high/medium/low.

### Posting to the PR (only when the user asks)

Default output is a printed report, nothing is posted. If the user asks to post on
the PR:

- **Dedupe first.** Fetch existing review comments
  (`gh api repos/{owner}/{repo}/pulls/<N>/comments --paginate`) and skip any
  file+line you have already commented on, so a re-run does not double-post.
- **Post one review, not N comments.** Batch all inline comments and the summary
  into a single review so the author gets one notification and the comments land
  atomically:
  ```
  gh api repos/{owner}/{repo}/pulls/<N>/reviews --input - <<'JSON'
  { "event": "COMMENT", "body": "...summary...",
    "comments": [
      { "path": "src/File.cs", "line": 42, "side": "RIGHT",
        "body": "issue (blocking): ..." }
    ] }
  JSON
  ```
- **Anchor rule.** Inline comments only attach to lines that appear in the diff;
  a line outside any hunk makes the API return 422. A finding on an unchanged
  line goes in the summary body instead. Multi-line comments need `start_line`
  plus `line`.
- Do not use `--approve` or `--request-changes` (event `APPROVE` /
  `REQUEST_CHANGES`) unless the user says so.

### Inline comment style (terse, plain English, no em dashes)

When you post an inline comment on a PR line, keep it short and plain. Use simple,
everyday English. Rules:

- Start with the label, for example `issue (blocking):`.
- Then one or two short sentences. Say what is wrong, then how to fix it.
- Plain words a junior dev understands on first read. No jargon dump.
- **No em dashes.** Use a full stop or a comma. No emoji, no color circles.
- If the fix is one line, show it in a fenced code block.

Good examples:

```
issue (blocking): This endpoint has no policy, so anyone can call it. Add Policies(PermissionPolicy.For("billing:invoices.manage")).
```
```
suggestion (non-blocking): Move this mapping into the aggregate's mapper so it is not written twice.
```
```
chore (blocking): New use case has no test. Add a handler test for the success and the not-found paths.
```
```
issue (blocking): This handler reads DateTime.UtcNow. Inject TimeProvider and pass nowUtc into the aggregate method.
```

Avoid: long paragraphs, hedging, restating the code, and em dashes.

### Summary (end of review)

Post or print a short summary: spec/plan alignment result first (aligned, gaps
listed, or "no spec found"), then findings grouped blocking first then
non-blocking, one bullet each. End with a verdict line:

`Blocking: N. Non-blocking: M. Verdict: ready to merge / needs changes / blocked.`
Then name the single most important thing to fix. If the diff is clean, say so
plainly. Do not invent findings.

## 6. Fix mode (opt-in)

Only when invoked with `--fix`, or the user asks to fix. After the report:

- Apply **only safe, mechanical fixes** for confirmed findings: add a missing
  `Policies(PermissionPolicy.For(...))` plus its manifest entry, add the `Async`
  suffix and thread `CancellationToken`, move a `PackageReference` version into
  `Directory.Packages.props`, replace a hand-rolled error switch with
  `result.ExecuteFailureAsync(HttpContext)`, swap a direct clock read for
  `TimeProvider`, seal a class, swap a raw enum for a `SmartEnum`.
- **Do not auto-fix judgment calls** (aggregate redesign, splitting a module,
  reworking a use case, changing a public contract). Leave those as comments.
- One focused change per finding, keep the diff minimal. Run
  `dotnet build` and the relevant tests if available (Definition of Done: 0
  warnings, tests green). Report what you changed and what you left for the
  author to decide.

## Maintaining this skill

Regression fixture lives in `fixtures/`: a seeded diff, a plan document, a repo
file listing, and `expected-findings.md`. After any edit to this skill or
checklist.md, re-run the fixture review (fresh subagent, skill files plus fixture
only) and compare against expected findings: every seeded violation caught, no
invented findings, negative controls not flagged.

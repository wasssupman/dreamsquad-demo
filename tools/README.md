# tools/

Session log analysis utilities for the Defense Tournament prototype.

## analyze_sessions.py

Aggregates `GameLogs/session-*.json` files and outputs H1/H2/H3 metrics
tied to `docs/PRD.md` §4.

```bash
# Default: read GameLogs/ next to project root, emit markdown to stdout
python3 tools/analyze_sessions.py

# JSON output for downstream processing
python3 tools/analyze_sessions.py --output json

# Custom logs directory
python3 tools/analyze_sessions.py --logs /path/to/logs
```

### Metrics

| Hypothesis | Signal | Interpretation |
|---|---|---|
| H1 | Pick Jaccard (early 3 vs last 3 sessions) | ↗ = 픽 수렴 |
| H1 | Avg score (early vs late) | ↗ = 학습 개선 |
| H2 | Skill timing σ per skill_id | ↓ = 사용 타이밍 수렴 |
| H2 | synergy.activations / peakCount avg | ↗ = 전략적 클러스터 |
| H2 | onPlace usage by effect | 사용 분포 |
| H3 | Outcome distribution + defeat timing | "얼마나 접전이었나" 정량 측면 |

H2 코스트/3분 긴장감 축과 H3 정성 인터뷰 축은 PRD §4에서 별도 프로토콜.

## verify_production_transition.py

Read-only audit for the rule-and-plan-oriented Demo → Production transition documents
and the eventual immutable one-time freeze. It is dormant by default and must not run
as part of Demo design, implementation, completion, CI, or hook workflows.
`--project-owner-authorized` declares that the current Project owner request explicitly
authorizes this audit; repository state, stale documents, recent commits, or agent
judgment do not constitute authorization.

```bash
# Audit the living structure, three JSON schemas, non-archive Markdown links,
# coverage values, official inventory, and deterministic in-memory partition hashes.
python tools/verify_production_transition.py prepare --project-owner-authorized

# Audit the single canonical completed freeze. The official CLI rejects an
# arbitrary path, a second freeze candidate, or a second audit-events root.
# This checks the exact required export
# catalog, frozen rule/link contracts, manifest files and hashes, both consumer
# receipts, and the three ordered Project owner audit events held outside the payload.
python tools/verify_production_transition.py cutover --project-owner-authorized \
  --freeze-dir docs/production-transition/freezes/<freeze-id> \
  --events-dir docs/production-transition/governance/audit-events

# Without explicit authorization, either mode reads no transition file or Git state
# and exits 0 with result=SKIP. This takes precedence over cutover argument checks.
python tools/verify_production_transition.py prepare

# Temp-directory positive and negative fixtures for both modes.
python -m unittest tools.test_verify_production_transition
```

`prepare` selects only `common/**`, `client/**`, `game-server/**`, and
`governance/transition-policy.md`. It excludes `archive/**`, `maintenance/**`,
exact `fixture`/`fixtures` path segments, evidence, governance schemas/plans, and the
transition root documents from the official inventory. Official paths must be
canonical relative POSIX Markdown paths without Unicode `Cc` control characters.
The policy is mapped in memory to
`references/transition-policy.md`.

Partition hashes use lowercase SHA-256 over canonical UTF-8 JSON: a path-sorted array
of `{audience, bytes, path, sha256}` rows. `cutover` recomputes the same hashes from
freeze bytes and requires its `(path, audience)` inventory to exactly match the same
versioned export catalog used by `prepare`; consistently rehashing an omitted or
uncatalogued document does not make it valid. Frozen rule fields and local Markdown
links are revalidated as well. A receipt's `file_count` and `byte_count` cover the
actual target delivery (`manifest + common + consumer + policy`). `assigned_bundle_sha256` remains
the consumer partition hash; manifest and common hashes have their own receipt fields.
The three Project owner events live in the single canonical external audit directory
`docs/production-transition/governance/audit-events` and are not part of the freeze
inventory or either target delivery. The low-level verifier used by unit tests may
audit temp fixtures; the authorized CLI accepts only the canonical one-shot roots.

The tool uses no Git command and never creates or changes a freeze, receipt, event,
manifest, production input, or report file. All package assembly and hashes are
in-memory only.

### Requirements

- Python 3.9+
- Standard library only (no pandas / numpy).

## verify_demo_transition_firewall.py

Read-only Demo governance check. Unlike the owner-gated transition verifier, this command is
safe for normal Demo validation because it inspects only the isolation boundary and never reads
transition registry freshness, reviews, decisions, watch-path drift, or Git history.

It rejects transition-only governance under active `docs/spec/`, authoritative transition references
or transition-specific checklists/follow-ups/completion gates in active Demo specs/plans, automatic
transition-verifier calls from CI/hooks/general scripts, missing dormant/owner/Demo-authority policy
markers, and runtime/package/settings references.

```bash
python tools/verify_demo_transition_firewall.py
python -m unittest tools.test_verify_demo_transition_firewall
```

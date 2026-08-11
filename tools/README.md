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

Validates the long-running Demo → Production preparation registry without writing a
freeze or either production repository. The verifier is dormant by default and must
not be run as part of Demo design, implementation, completion, CI, or hook workflows.
`--project-owner-authorized` is an explicit declaration that the current request from
the Project owner authorizes production-transition verification; repository state,
stale records, recent commits, or agent judgment do not constitute authorization.

```bash
# Structural, provenance, hash, freshness, review and package dry-run checks.
# Incomplete/stale/blocked preparation records are reported as warnings.
python tools/verify_production_transition.py prepare --project-owner-authorized

# Strict preflight. Requires cutover_candidate state, locked scope,
# complete/current/reviewed/ready records, decided blockers, and each included
# source artifact byte-identical to its tracked blob at candidate_source_commit.
python tools/verify_production_transition.py cutover --project-owner-authorized

# Without explicit Project owner authorization, either mode performs no registry,
# Git, or watched-path inspection and exits 0 with result=SKIP.
python tools/verify_production_transition.py prepare

# Negative fixtures for stale paths, blockers, area reviews, closure,
# path containment/collision, hashes and Shared equality.
python -m unittest tools.test_verify_production_transition
```

The output manifest and package hashes are deterministic in-memory dry-run data. Each
file keeps its stable record ID, and `governance_attestation` carries the selected
record gate metadata, exact review tuples, and relevant decisions. The tool never
creates `freezes/` or `docs/migration-input/`.

### Requirements

- Python 3.9+
- Standard library only (no pandas / numpy).

## verify_demo_transition_firewall.py

Read-only Demo governance check. Unlike the owner-gated transition verifier, this command is
safe for normal Demo validation because it inspects only the isolation boundary and never reads
transition registry freshness, reviews, decisions, watch-path drift, or Git history.

It rejects transition-only governance under active `docs/spec/`, authoritative transition references
from active Demo specs/plans, automatic transition-verifier calls from CI/hooks/general scripts,
missing dormant/owner/Demo-authority policy markers, and runtime/package/settings references.

```bash
python tools/verify_demo_transition_firewall.py
python -m unittest tools.test_verify_demo_transition_firewall
```

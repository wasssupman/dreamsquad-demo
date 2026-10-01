# tools/

Session log analysis utilities for the Defense Tournament prototype.

## analyze_sessions.py

Aggregates `GameLogs/session-*.json` files and outputs H1/H2/H3 metrics
tied to the prototype PRD §4 (`docs/PRD.md` was retired 2026-09-03 — see git history).

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

H2 코스트/3분 긴장감 축과 H3 정성 인터뷰 축은 구 PRD §4(은퇴, git 이력)의 별도 프로토콜.

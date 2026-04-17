#!/usr/bin/env python3
"""Analyze battle session JSON logs against PRD H1/H2/H3 hypotheses.

H1 (draft convergence): picks + scores should converge across repeated plays.
H2 (placement/skill axis): skill timing stddev should shrink, synergy activations
    should rise as the player learns the strategic payoff of clustering.
H3 (defeat attribution): outcome distribution + defeat timing for "was it
    close or blowout" context — the human-interview side is out of scope for
    this script.
"""

from __future__ import annotations

import argparse
import json
import statistics
import sys
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass
class Session:
    """One battle session parsed from a GameLogs JSON file."""
    path: Path
    session_id: str
    phase: str
    timestamp_start: str
    attack_deck_id: str
    duration_sec: float
    outcome: str
    enemies_reached_goal: int
    score: int
    draft_pool: list[str]
    draft_picked: list[str]
    skill_loadout: list[str]
    skill_usages: list[dict[str, Any]] = field(default_factory=list)
    synergy_activations: int = 0
    synergy_peak: int = 0
    on_place_usages: list[dict[str, Any]] = field(default_factory=list)
    placements: list[dict[str, Any]] = field(default_factory=list)


def _get(d: dict, path: str, default=None):
    """Safe nested dict read: `_get(data, 'result.score', 0)`."""
    cur: Any = d
    for part in path.split('.'):
        if not isinstance(cur, dict) or part not in cur:
            return default
        cur = cur[part]
    return cur if cur is not None else default


def load_sessions(logs_dir: Path) -> list[Session]:
    """Parse every `session-*.json` under `logs_dir` into Session dataclasses.

    Older Phase 0~3 logs are missing some fields (synergy / skill / onPlace);
    we substitute defaults rather than failing so historical logs remain usable.
    """
    sessions: list[Session] = []
    for p in sorted(logs_dir.glob("session-*.json")):
        try:
            with p.open(encoding="utf-8") as f:
                d = json.load(f)
        except (OSError, json.JSONDecodeError) as e:
            print(f"[WARN] skip {p.name}: {e}", file=sys.stderr)
            continue

        sessions.append(Session(
            path=p,
            session_id=_get(d, "session_id", ""),
            phase=_get(d, "phase", "unknown"),
            timestamp_start=_get(d, "timestamp_start", ""),
            attack_deck_id=_get(d, "attack_deck_id", ""),
            duration_sec=float(_get(d, "result.duration_sec", 0.0) or 0.0),
            outcome=_get(d, "result.outcome", "unknown"),
            enemies_reached_goal=int(_get(d, "result.enemies_reached_goal", 0) or 0),
            score=int(_get(d, "result.score", 0) or 0),
            draft_pool=list(_get(d, "draft.pool", []) or []),
            draft_picked=list(_get(d, "draft.picked", []) or []),
            skill_loadout=list(_get(d, "skill.loadout", []) or []),
            skill_usages=list(_get(d, "skill.usages", []) or []),
            synergy_activations=int(_get(d, "synergy.activations", 0) or 0),
            synergy_peak=int(_get(d, "synergy.peakCount", 0) or 0),
            on_place_usages=list(_get(d, "on_place_usages", []) or []),
            placements=list(_get(d, "placements", []) or []),
        ))
    sessions.sort(key=lambda s: s.timestamp_start)
    return sessions


# ------------------------------- H1 metrics ------------------------------- #

def _jaccard(a: list[str], b: list[str]) -> float:
    """Set-based Jaccard. Draft picks are unordered sets of 7 units."""
    sa, sb = set(a), set(b)
    if not sa and not sb:
        return 1.0
    union = sa | sb
    return len(sa & sb) / len(union) if union else 0.0


def _window_jaccard_avg(sessions: list[Session]) -> float:
    """Mean pairwise Jaccard of draft picks within a window."""
    if len(sessions) < 2:
        return 0.0
    pairs: list[float] = []
    for i in range(len(sessions)):
        for j in range(i + 1, len(sessions)):
            pairs.append(_jaccard(sessions[i].draft_picked, sessions[j].draft_picked))
    return statistics.mean(pairs) if pairs else 0.0


def analyze_h1(sessions: list[Session]) -> dict[str, Any]:
    """H1 checks whether repeated plays are making picks + scores converge.

    Convergence signals:
    - Late-window Jaccard > early-window Jaccard (picks settle).
    - Late-window avg score > early-window avg score (improvement).
    """
    result: dict[str, Any] = {
        "session_count": len(sessions),
        "deck_breakdown": dict(Counter(s.attack_deck_id for s in sessions)),
    }
    if len(sessions) < 6:
        result["windows"] = None
        result["note"] = "Need >=6 sessions to form early/late windows."
        return result

    early = sessions[:3]
    late = sessions[-3:]

    # Aggregate pick counts per window to compute rank deltas.
    early_picks: Counter[str] = Counter()
    late_picks: Counter[str] = Counter()
    for s in early:
        early_picks.update(s.draft_picked)
    for s in late:
        late_picks.update(s.draft_picked)

    rank_delta: list[dict[str, Any]] = []
    all_units = sorted(set(early_picks) | set(late_picks))
    for u in all_units:
        rank_delta.append({
            "unit": u,
            "early_count": early_picks.get(u, 0),
            "late_count": late_picks.get(u, 0),
            "delta": late_picks.get(u, 0) - early_picks.get(u, 0),
        })
    rank_delta.sort(key=lambda r: -abs(r["delta"]))

    result["windows"] = {
        "early": {
            "jaccard_avg": round(_window_jaccard_avg(early), 3),
            "score_avg": round(statistics.mean(s.score for s in early), 1),
            "top_picks": early_picks.most_common(5),
        },
        "late": {
            "jaccard_avg": round(_window_jaccard_avg(late), 3),
            "score_avg": round(statistics.mean(s.score for s in late), 1),
            "top_picks": late_picks.most_common(5),
        },
        "pick_rank_delta_top": rank_delta[:10],
    }
    return result


# ------------------------------- H2 metrics ------------------------------- #

def analyze_h2(sessions: list[Session]) -> dict[str, Any]:
    """H2 probes whether skill timing / synergy use are converging.

    Phase 5 observables:
    - Skill usage timing mean/stddev per skill id (shrinking stddev = learned).
    - Synergy activations / peakCount — rising = strategic clustering.
    - onPlace frequency per effect — shows engagement with placement effects.
    Coste system and 3분 긴장감은 Phase 5 §5 이후 구현, 여기서는 제외.
    """
    total_skill_uses = sum(len(s.skill_usages) for s in sessions)
    timing_by_skill: dict[str, list[float]] = {}
    for s in sessions:
        for u in s.skill_usages:
            sid = u.get("skill_id", "unknown")
            timing_by_skill.setdefault(sid, []).append(float(u.get("time", 0.0)))

    timing_summary: dict[str, dict[str, float]] = {}
    for sid, times in timing_by_skill.items():
        if len(times) == 0:
            continue
        timing_summary[sid] = {
            "count": len(times),
            "mean_sec": round(statistics.mean(times), 2),
            "stddev_sec": round(statistics.stdev(times), 2) if len(times) > 1 else 0.0,
        }

    synergy_activations = [s.synergy_activations for s in sessions]
    synergy_peak = [s.synergy_peak for s in sessions]

    # onPlace usage per effect (e.g. SlowPulse / BoostNearbyDefenders).
    on_place_by_effect: Counter[str] = Counter()
    for s in sessions:
        for u in s.on_place_usages:
            on_place_by_effect[u.get("effect", "?")] += 1

    placement_counts = [len(s.placements) for s in sessions]
    unique_types_per_session = [
        len({p.get("unit_type") for p in s.placements}) for s in sessions
    ]

    return {
        "skill_usages_total": total_skill_uses,
        "skill_usages_per_session_avg": round(
            total_skill_uses / max(len(sessions), 1), 2),
        "skill_timing_per_id": timing_summary,
        "synergy": {
            "activations_avg": round(statistics.mean(synergy_activations), 2) if synergy_activations else 0,
            "peak_avg": round(statistics.mean(synergy_peak), 2) if synergy_peak else 0,
        },
        "on_place_usage_by_effect": dict(on_place_by_effect),
        "placements": {
            "avg_per_session": round(statistics.mean(placement_counts), 2) if placement_counts else 0,
            "avg_unique_types": round(statistics.mean(unique_types_per_session), 2) if unique_types_per_session else 0,
        },
    }


# ------------------------------- H3 metrics ------------------------------- #

def analyze_h3(sessions: list[Session]) -> dict[str, Any]:
    """H3 quantitative support: outcome distribution and defeat shape.

    The qualitative "player can articulate why they lost" signal requires a
    separate interview coding step and is not derivable from logs alone.
    """
    outcomes: Counter[str] = Counter(s.outcome for s in sessions)
    by_outcome: dict[str, list[float]] = {}
    for s in sessions:
        by_outcome.setdefault(s.outcome, []).append(s.duration_sec)

    duration_by_outcome: dict[str, dict[str, float]] = {}
    for outcome, durations in by_outcome.items():
        duration_by_outcome[outcome] = {
            "count": len(durations),
            "mean_sec": round(statistics.mean(durations), 2),
        }

    defeats = [s for s in sessions if s.outcome == "defeat"]
    defeat_shape: dict[str, Any] = {}
    if defeats:
        defeat_shape = {
            "count": len(defeats),
            "avg_enemies_reached_goal": round(
                statistics.mean(s.enemies_reached_goal for s in defeats), 2),
            "avg_duration_sec": round(
                statistics.mean(s.duration_sec for s in defeats), 2),
            "duration_buckets": _bucketize(
                [s.duration_sec for s in defeats],
                edges=[30, 60, 90, 120, 150, 180],
            ),
        }

    return {
        "outcome_distribution": dict(outcomes),
        "duration_by_outcome": duration_by_outcome,
        "defeat_shape": defeat_shape,
    }


def _bucketize(values: list[float], edges: list[float]) -> dict[str, int]:
    """Count values falling into [0, edges[0]], [edges[0], edges[1]], ..."""
    buckets: dict[str, int] = {}
    prev = 0.0
    for edge in edges:
        buckets[f"{int(prev)}-{int(edge)}s"] = sum(1 for v in values if prev <= v < edge)
        prev = edge
    buckets[f"{int(prev)}s+"] = sum(1 for v in values if v >= prev)
    return buckets


# ------------------------------- rendering ------------------------------- #

def render_markdown(h1: dict, h2: dict, h3: dict) -> str:
    """Plain readable report — mirrors PRD §4 hypothesis framing."""
    out: list[str] = ["# Session Analysis Report\n"]

    # H1
    out.append("## H1 — Draft convergence\n")
    out.append(f"- Total sessions: **{h1['session_count']}**")
    out.append(f"- Decks played: {h1['deck_breakdown']}")
    if h1.get("windows"):
        e = h1["windows"]["early"]
        l = h1["windows"]["late"]
        arrow = _arrow(e["jaccard_avg"], l["jaccard_avg"])
        out.append("")
        out.append("### Early (first 3) vs Late (last 3)")
        out.append(
            f"- Pick Jaccard similarity: **{e['jaccard_avg']}** → **{l['jaccard_avg']}** {arrow}"
            + "  _(higher late = picks converging)_"
        )
        arrow = _arrow(e["score_avg"], l["score_avg"])
        out.append(
            f"- Avg score: **{e['score_avg']}** → **{l['score_avg']}** {arrow}"
            + "  _(higher late = learning)_"
        )
        out.append(f"- Early top picks: {e['top_picks']}")
        out.append(f"- Late top picks: {l['top_picks']}")
        deltas = h1["windows"]["pick_rank_delta_top"]
        if deltas:
            out.append("- Top rank shifts (late - early):")
            for d in deltas[:5]:
                out.append(f"  - {d['unit']}: {d['early_count']} → {d['late_count']} (Δ{d['delta']:+})")
    else:
        out.append(f"- {h1.get('note', '')}")
    out.append("")

    # H2
    out.append("## H2 — Placement / skill axis (partial — cost + 3min 타이머는 타 범위)\n")
    out.append(f"- Skill uses: total {h2['skill_usages_total']}, per-session avg {h2['skill_usages_per_session_avg']}")
    if h2["skill_timing_per_id"]:
        out.append("- Skill timing (mean / stddev):")
        for sid, meta in h2["skill_timing_per_id"].items():
            out.append(
                f"  - `{sid}`: n={meta['count']}, μ={meta['mean_sec']}s, σ={meta['stddev_sec']}s"
                + "  _(σ ↓ = converging)_"
            )
    out.append(f"- Synergy activations avg: {h2['synergy']['activations_avg']}, peak avg: {h2['synergy']['peak_avg']}")
    out.append(f"- onPlace usages by effect: {h2['on_place_usage_by_effect']}")
    p = h2["placements"]
    out.append(f"- Placements: {p['avg_per_session']} avg / session, {p['avg_unique_types']} unique types avg")
    out.append("")

    # H3
    out.append("## H3 — Defeat attribution (quantitative side)\n")
    out.append(f"- Outcome distribution: {h3['outcome_distribution']}")
    out.append("- Duration by outcome:")
    for outcome, meta in h3["duration_by_outcome"].items():
        out.append(f"  - `{outcome}`: n={meta['count']}, μ={meta['mean_sec']}s")
    if h3["defeat_shape"]:
        d = h3["defeat_shape"]
        out.append(
            f"- Defeats: n={d['count']}, avg enemies reaching goal={d['avg_enemies_reached_goal']},"
            f" avg duration={d['avg_duration_sec']}s"
        )
        out.append(f"- Defeat duration buckets: {d['duration_buckets']}")
    out.append("")
    out.append(
        "> _H3 final verdict requires post-play interview coding — "
        "see PRD §4.3 for the qualitative protocol._"
    )
    return "\n".join(out)


def _arrow(early: float, late: float) -> str:
    if late > early:
        return "↗"
    if late < early:
        return "↘"
    return "="


# ------------------------------- main ------------------------------- #

def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--logs",
        type=Path,
        default=Path(__file__).resolve().parent.parent / "GameLogs",
        help="Directory with session-*.json files",
    )
    parser.add_argument(
        "--output",
        choices=["markdown", "json"],
        default="markdown",
    )
    args = parser.parse_args(argv)

    if not args.logs.exists():
        print(f"[ERROR] logs dir not found: {args.logs}", file=sys.stderr)
        return 2

    sessions = load_sessions(args.logs)
    if not sessions:
        print(f"[WARN] no sessions in {args.logs}", file=sys.stderr)
        return 1

    h1 = analyze_h1(sessions)
    h2 = analyze_h2(sessions)
    h3 = analyze_h3(sessions)

    if args.output == "json":
        print(json.dumps({"h1": h1, "h2": h2, "h3": h3}, indent=2, ensure_ascii=False))
    else:
        print(render_markdown(h1, h2, h3))
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""skill-data-table unit 4 — 이전 dry-run 을 **헤드리스로**(Unity 없이) 돌린다.

정본은 Unity 메뉴 `Wassup/BattleCore/Skill Data Table/이전 dry-run (표만 쓴다)`(= `LegacyBindingMigration`)다. 이 스크립트는
같은 규칙을 에셋 YAML 위에서 다시 돌려 **승인 전 표**를 만들고, 라이브 굽기 스냅샷(`Tests/EditModeAssets/Fixtures/*_bake_snapshot.txt`)과
「이전 뒤 굽기가 싣는 겸직 칸 값」을 대조한다(왕복 검사).

단일 출처: 종류 × 칸 표는 `Scripts/Data/Effects/EffectSlots.cs` 의 `Of` 스위치를 **파싱해서** 쓴다(사본을 두지 않는다).
enum 번호는 `Scripts/BattleCore/Trigger/TriggerKinds.cs` 를 파싱한다.

사용: python3 tools/skill-data-table/dry_run.py [출력 md 경로]
에셋을 쓰지 않는다. 출력 = 표 1 파일(+ 표준출력 요약).
"""
import os
import re
import sys

import yaml

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "Assets/_Project/Data")
SCRIPTS = os.path.join(ROOT, "Assets/_Project/Scripts")
FIX = os.path.join(ROOT, "Assets/_Project/Tests/EditModeAssets/Fixtures")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs/spec/skill-data-table/dry-run/dry_run_table_headless.md")


def script_guid(rel):
    with open(os.path.join(SCRIPTS, rel + ".meta")) as f:
        return re.search(r"guid: (\w+)", f.read()).group(1)


G = {
    "card": script_guid("Data/Dreamcatcher/DreamcatcherCard.cs"),
    "unit": script_guid("Data/DefenderUnitData.cs"),
    "enemy": script_guid("Data/AttackUnitData.cs"),
    "unitskill": script_guid("Data/Abilities/UnitSkillAbility.cs"),
    "shield": script_guid("Data/Abilities/ShieldCastAbility.cs"),
    "skill": script_guid("Data/SkillData.cs"),
    "projectile": script_guid("Data/ProjectileData.cs"),
    "pattern": script_guid("Data/ProjectilePatternData.cs"),
    "hazard": script_guid("Data/HazardSO.cs"),
    "blocker": script_guid("Data/Authoring/BlockingHazardSO.cs"),
}
KIND_OF_SCRIPT = {v: k for k, v in G.items()}


def parse_enum(path, name):
    src = open(path).read()
    body = re.search(r"enum %s\s*:\s*byte\s*\{(.*?)\n    \}" % name, src, re.S).group(1)
    out = {}
    for m in re.finditer(r"^\s*(\w+)\s*=\s*(\d+),", body, re.M):
        out[int(m.group(2))] = m.group(1)
    return out


KINDS = os.path.join(SCRIPTS, "BattleCore/Trigger/TriggerKinds.cs")
EFFECT = parse_enum(KINDS, "EffectKind")
TRIGGER = parse_enum(KINDS, "TriggerKind")
EFFECT_ID = {v: k for k, v in EFFECT.items()}


def parse_slots():
    src = open(os.path.join(SCRIPTS, "Data/Effects/EffectSlots.cs")).read()
    body = src[src.index("switch (kind)"):src.index("default: return false;")]
    table, pending = {}, []
    for line in body.split("\n"):
        cases = re.findall(r"case EffectKind\.(\w+):", line)
        if not cases:
            continue
        pending += cases
        if "return true" in line:
            slots = {k: "None" for k in "mtd"}
            for k, s in re.findall(r"\b([mtd]) = EffectSlot\.(\w+)", line):
                slots[k] = s
            for c in pending:
                table[c] = (slots["m"], slots["t"], slots["d"])
            pending = []
    return table


SLOTS = parse_slots()
INT_SLOTS = {"Count", "RadiusTiles", "RangeTiles", "StackCap", "DensityRadiusTiles", "LandingRingTiles"}
SLOT_FIELD = {"Damage": "damage", "Shield": "shield", "Percent": "percent", "Mul": "mul", "Count": "count",
              "RadiusTiles": "radius_tiles", "RangeTiles": "range_tiles", "DurationSec": "duration_sec",
              "FlightSec": "flight_sec", "StackCap": "stack_cap", "Speed": "speed",
              "DensityRadiusTiles": "density_radius_tiles", "LandingRingTiles": "landing_ring_tiles"}
CC_NEW = {0: "Stun", 1: "Impulse", 2: "Sleep"}          # 옛 DcCcKind → 스킬 번호 이름(Stun 0→3 · Impulse 1→1 · Sleep 2→4)
STACK_NEW = {0: "Fire", 1: "Ice", 2: "Bleed", 3: "Poison"}
BUFF = ["AttackDamage", "AttackSpeed", "EffectiveHealth", "MoveSpeed", "CostRate", "DamageVsCc"]
SHIELD_FILTER = {0: "Self", 1: "All", 2: "MinHealth"}
SKILL_EFFECT = ["SlowField", "PowerSurge", "RapidFire", "Tornado", "Meteor", "Portal"]
ACTIVE_KIND = {"Meteor": "ActiveMeteor", "SlowField": "ActiveSlowField", "PowerSurge": "ActivePowerSurge",
               "RapidFire": "ActiveRapidFire", "Tornado": "ActiveTornado", "Portal": "ActivePortal"}


# ── 에셋 YAML ──────────────────────────────────────────────────────────

def load_assets():
    by_guid, objs = {}, []
    wanted = set(G.values())
    for dp, _, fs in os.walk(DATA):
        for f in fs:
            if not f.endswith(".asset"):
                continue
            p = os.path.join(dp, f)
            txt = open(p, encoding="utf-8").read()
            m = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", txt)
            if not m or m.group(1) not in wanted:
                continue
            txt = re.sub(r"^%.*$", "", txt, flags=re.M)
            txt = re.sub(r"^--- !u!\d+ &\d+.*$", "---", txt, flags=re.M)
            doc = next(d for d in yaml.safe_load_all(txt) if d)
            mb = doc["MonoBehaviour"]
            with open(p + ".meta") as mf:
                g = re.search(r"guid: (\w+)", mf.read()).group(1)
            o = {"kind": KIND_OF_SCRIPT[m.group(1)], "path": os.path.relpath(p, ROOT), "data": mb, "guid": g}
            by_guid[g] = o
            objs.append(o)
    objs.sort(key=lambda o: o["path"])
    return by_guid, objs


BY_GUID, OBJS = load_assets()


def ref(v):
    if isinstance(v, dict) and v.get("guid"):
        return BY_GUID.get(v["guid"])
    return None


def f(d, k, default=0):
    v = d.get(k, default)
    return default if v is None else v


def num(x):
    x = float(x)
    return ("%.3f" % x).rstrip("0").rstrip(".") if x != int(x) else str(int(x))


# ── 이전 규칙(`LegacyBindingMigration` · `EffectSlots.FromLegacy` 와 같은 규칙) ──

def moved_damage(p, notes):
    kind = EFFECT[f(p, "kind")]
    if kind == "EmitProjectilePattern":
        pat = ref(p.get("pattern"))
        if not pat:
            notes.append("발사 명세가 비었다 — 옮길 피해 0(옛 굽기도 거절)")
            return 0.0
        pd = pat["data"]
        barrel = ref(pd.get("barrel"))
        blocker = ref(barrel["data"].get("spawnBlocker")) if barrel else None
        dmg = float(f(pd, "damage", 10))
        if blocker:
            ex = max(0.0, float(f(blocker["data"], "explodeDamage")))
            notes.append(f"U10 — 길막 '{blocker['data']['m_Name']}' 폭발 피해 {num(ex)} → 효과 damage(명세 damage {num(dmg)} 는 안 쓰였다)")
            return ex
        notes.append(f"U10 — 명세 '{pd['m_Name']}' damage {num(dmg)} → 효과 damage")
        return dmg
    if kind == "SpawnHazard":
        hz = ref(p.get("hazard"))
        if not hz:
            notes.append("장판이 비었다 — 옮길 피해 0(옛 굽기도 거절)")
            return 0.0
        dots = [float(f(e, "param1")) for e in (hz["data"].get("effects") or []) if f(e, "kind") == 2]
        if len(dots) > 1:
            notes.append(f"장판 '{hz['data']['m_Name']}' DoT 가 둘 이상 — 옛 굽기도 거절(옮길 피해 0)")
            return 0.0
        dot = dots[0] if dots else 0.0
        notes.append(f"U10 — 장판 '{hz['data']['m_Name']}' DoT {num(dot)} → 효과 damage")
        return dot
    return 0.0


def put(v, slot, x, legacy, notes, kind):
    if slot == "None":
        if x != 0:
            notes.append(f"{kind} 는 옛 {legacy} 칸을 안 읽는다 — 값 {num(x)} 버림(옛 굽기는 해시에 실었다 · 해시 변화)")
        return
    if slot in INT_SLOTS:
        n = int(x)
        if n != x:
            notes.append(f"옛 {legacy} {num(x)} → {slot} 정수 칸 — 소수부 버림(손실)")
        v[SLOT_FIELD[slot]] = n
    else:
        v[SLOT_FIELD[slot]] = float(x)


def from_legacy(p, card_owner, notes):
    kind = EFFECT[f(p, "kind")]
    v = {"kind": kind, "buff_stat": BUFF[f(p, "buffStat")], "telegraph": bool(f(p, "telegraph")),
         "tick_sec": float(f(p, "tickIntervalSec")), "cone_half_deg": float(f(p, "coneHalfAngleDeg")),
         "cc_kind": CC_NEW.get(f(p, "ccKind"), "Stun"), "stack_kind": STACK_NEW.get(f(p, "stackKind"), "Bleed")}
    if kind not in SLOTS:
        notes.append(f"종류 {kind} 는 효과 표에 들지 않는다 — 옮기지 않는다")
        return v
    m, t, d = SLOTS[kind]
    mag = float(f(p, "magnitude"))
    if kind == "SelfStatBuff" and not card_owner:
        mag = (mag - 1) * 100
        notes.append("SelfStatBuff 유닛·적 저작 배율 → % (소유자 인코딩 통일)")
    put(v, m, mag, "magnitude", notes, kind)
    put(v, t, float(f(p, "tileRange")), "tileRange", notes, kind)
    put(v, d, float(f(p, "duration")), "duration", notes, kind)
    if kind in ("SelfBlink", "UltimateLeap"):
        v["damage"] = float(f(p, "slamDamage"))
        v["radius_tiles"] = int(f(p, "slamTileRange"))
    else:
        if f(p, "slamDamage"):
            notes.append("slamDamage 는 도약 전용 — 옛 굽기도 무시했다(버림)")
        if f(p, "slamTileRange"):
            notes.append("slamTileRange 는 도약 전용 — 옛 굽기는 해시에 실었다(버림 · 해시 변화)")
    if kind == "SelfOrbitProjectile":
        v["count"] = int(f(p, "orbitCount"))
    elif f(p, "orbitCount"):
        notes.append("orbitCount 는 궤도 화염구 전용 — 옛 굽기도 무시했다(버림)")
    moved = moved_damage(p, notes)
    if kind in ("EmitProjectilePattern", "SpawnHazard"):
        v["damage"] = moved
    return v


def to_legacy(v):
    kind = v["kind"]
    if kind not in SLOTS:
        return None
    m, t, d = SLOTS[kind]
    g = lambda s: 0 if s == "None" else v.get(SLOT_FIELD[s], 0)
    return (float(g(m)), int(g(t)), float(g(d)))


def range_to_tiles(r):
    r = float(r)
    return 0 if r <= 0 else int(r) + (0 if int(r) == r else 1)


def sanitize(raw, notes):
    s = (raw or "").strip().lower()
    s = re.sub(r"[^a-z0-9_]", "_", s)
    if not s or not s[0].isalpha():
        s = "e_" + s
    if s != raw:
        notes.append(f"id '{raw}' → '{s}'(스네이크 규칙)")
    return s


USED = set()


def unique(i, notes):
    u, n = i, 2
    while u in USED:
        u = f"{i}_{n}"
        n += 1
    USED.add(u)
    if u != i:
        notes.append(f"id '{i}' 충돌 → '{u}'")
    return u


def trig(t):
    t = t or {}
    parts = [TRIGGER[f(t, "kind")]]
    if f(t, "period"):
        parts.append(f"period {f(t, 'period')}")
    if f(t, "periodSeconds"):
        parts.append(f"period_sec {num(f(t, 'periodSeconds'))}")
    if f(t, "fraction"):
        parts.append(f"fraction {num(f(t, 'fraction'))}")
    if f(t, "subject"):
        parts.append("subject Any")
    if f(t, "gate"):
        parts.append(f"gate HpBelow/{['Self', 'EventTarget'][f(t, 'gateSubject')]} {num(f(t, 'gateValue'))}")
    return parts


def row(plan_rows, slot, source, t_parts, fire_cap, v, refs, notes, old_label, new_label):
    if fire_cap:
        t_parts = t_parts + [f"fire_cap {fire_cap}"]
    if old_label != new_label:
        notes.append(f"라벨 변화(해시 밖): 「{old_label}」 → 「{new_label}」")
    plan_rows.append({"slot": slot, "source": source, "trigger": " · ".join(t_parts), "values": v, "refs": refs,
                      "notes": notes, "old_label": old_label})


def refs_of(p):
    out = []
    pr = ref(p.get("projectile"))
    pa = ref(p.get("pattern"))
    hz = ref(p.get("hazard"))
    au = p.get("auraPrefab") or {}
    if pr:
        out.append(f"projectile_id {pr['data'].get('id')}")
    if pa:
        out.append(f"pattern_id {pa['data'].get('id')}")
    if hz:
        out.append(f"hazard_id {hz['data']['m_Name']}")
    if au.get("guid"):
        out.append("(뷰) aura")
    return out


def plan():
    owners = []
    cards = [o for o in OBJS if o["kind"] == "card"]
    units = [o for o in OBJS if o["kind"] == "unit"]
    enemies = [o for o in OBJS if o["kind"] == "enemy"]
    for c in cards:
        d = c["data"]
        cid = d.get("id") or ""
        typ = f(d, "type")  # 0 Squad · 1 Unit · 2 Active
        o = {"kind": "card", "id": cid, "path": c["path"], "rows": [], "notes": [], "owner": []}
        if typ == 2:
            s = ref(d.get("skill"))
            if not s:
                o["notes"].append("액티브인데 SkillData 가 없다")
                owners.append(o)
                continue
            sd = s["data"]
            eff = SKILL_EFFECT[f(sd, "effect")]
            kind = ACTIVE_KIND[eff]
            notes = []
            v = {"kind": kind}
            m, t, dd = SLOTS[kind]
            mag, dur, warn = float(f(sd, "magnitude", 1)), float(f(sd, "durationSec", 1)), float(f(sd, "warningSec"))
            put(v, m, mag, "magnitude", notes, kind)
            put(v, t, range_to_tiles(f(sd, "range")), "range", notes, kind)
            put(v, dd, (warn if warn > 0 else 0.0) if kind == "ActiveMeteor" else dur, "duration", notes, kind)
            if kind == "ActiveMeteor" and dur:
                notes.append(f"메테오 durationSec {num(dur)} 는 옛 굽기도 버렸다(지속 = 낙하 예고) — 옮기지 않음")
            refs = []
            pr = ref(sd.get("projectile"))
            if pr:
                refs.append(f"projectile_id {pr['data'].get('id')}")
            v["_id"] = unique(sanitize(sd.get("id"), notes), notes)
            row(o["rows"], 0, "skill", ["Cast"], 1, v, refs, notes, f"액티브 '{cid}'", f"액티브 '{cid}'")
            o["owner"].append(f"cooldown_sec = {num(f(sd, 'cooldownSec', 10))} · needs_two_tiles = {int(bool(f(sd, 'needsTwoTiles')))}")
            o["notes"].append(f"SkillData 표시 칸(displayName · description · uiTint · cost={f(sd, 'cost', 2)})은 옮기지 않는다(`tables.md` §11 밖)")
            owners.append(o)
            continue
        mech = d.get("mechanics") or []
        if typ == 0:
            if mech:
                o["notes"].append(f"Squad 카드 mechanics {len(mech)} 줄 — 옛 굽기도 안 읽었다(옮기지 않음)")
            if d.get("effects"):
                o["notes"].append(f"스쿼드 스탯 효과 {len(d['effects'])} 줄 = 카드 자식 값(`CardStatEffects`) — 그대로")
            if o["notes"]:
                owners.append(o)
            continue
        bounty = any(EFFECT[f(m_["payload"], "kind")] == "BountyMark" for m_ in mech)
        o["owner"].append(f"hostKinds = {'Enemy' if bounty else 'Defender'}")
        if d.get("attackMods"):
            o["notes"].append(f"공격 수식자 {len(d['attackMods'])} 줄 = 카드 자식 값(`CardAttackMods`) — 그대로")
        base = sanitize(cid, o["notes"])
        for i, m_ in enumerate(mech):
            notes = []
            v = from_legacy(m_["payload"], True, notes)
            v["_id"] = unique(f"{base}_{i}" if len(mech) > 1 else base, notes)
            lab = f"카드 '{cid}' mechanic {i}"
            fc = 1 if v["kind"] == "UltimateLeap" else 0
            row(o["rows"], i, f"mechanics[{i}]", trig(m_.get("trigger")), fc, v, refs_of(m_["payload"]), notes, lab, lab)
        owners.append(o)

    for u in units:
        d = u["data"]
        abil = [ref(a) for a in (d.get("abilities") or [])]
        skill = next((a for a in abil if a and a["kind"] == "unitskill"), None)
        shield = next((a for a in abil if a and a["kind"] == "shield"), None)
        mech = (skill["data"].get("mechanics") or []) if skill else []
        if not mech and not shield:
            continue
        name = d["m_Name"]
        o = {"kind": "unit", "id": d.get("id"), "path": u["path"], "rows": [], "notes": [], "owner": []}
        aid = sanitize((skill["data"].get("id") if skill else None) or d.get("id"), o["notes"])
        for i, m_ in enumerate(mech):
            notes = []
            v = from_legacy(m_["payload"], False, notes)
            v["_id"] = unique(f"{aid}_{i}" if len(mech) > 1 else aid, notes)
            lab = f"{name} mechanic {i}"
            fc = 1 if v["kind"] == "UltimateLeap" else 0
            row(o["rows"], i, f"UnitSkillAbility '{skill['data']['m_Name']}'.mechanics[{i}]", trig(m_.get("trigger")), fc, v,
                refs_of(m_["payload"]), notes, lab, lab)
        if shield:
            sd = shield["data"]
            cd, amt = float(f(sd, "cooldown")), float(f(sd, "amount"))
            if cd <= 0 or amt <= 0:
                o["notes"].append("ShieldCastAbility cooldown/amount <= 0 — 옛 굽기도 건너뛰었다")
            else:
                slot = len(o["rows"])
                rng = float(f(d, "attackRange", 3))
                rad = range_to_tiles(rng)
                notes = [f"U14 — 실드 반경 = 사거리 {num(rng)} 파생 {rad}칸을 **고정값**으로(사거리를 바꿔도 안 따라간다)",
                         "해시 변화 1칸: coneSinCos 0,0 → 0,1(옛 전용 굽기가 반각을 안 구웠다 · 실드는 반각을 안 읽는다 — 동작 무변)"]
                v = {"kind": "GrantShield", "shield": amt, "radius_tiles": rad, "count": max(1, int(f(sd, "targetCount", 1))),
                     "shield_filter": SHIELD_FILTER[f(sd, "filter")], "includes_self": True, "cc_kind": "Slow(0 — 비움)",
                     "stack_kind": "None(0 — 비움)"}
                v["_id"] = unique(sanitize(sd.get("id") or aid + "_shield", notes), notes)
                row(o["rows"], slot, f"ShieldCastAbility '{sd['m_Name']}'", ["PeriodicTimer", f"period_sec {num(cd)}"], 0, v, [],
                    notes, f"{name} 실드 캐스트", f"{name} mechanic {slot}")
        owners.append(o)

    for e in enemies:
        d = e["data"]
        mech = d.get("nightmareMechanics") or []
        if not mech:
            continue
        name = d["m_Name"]
        o = {"kind": "enemy", "id": d.get("id"), "path": e["path"], "rows": [], "notes": [], "owner": []}
        base = sanitize(d.get("id"), o["notes"])
        slot = 0
        for i, m_ in enumerate(mech):
            p = m_["payload"]
            if EFFECT[f(p, "kind")] == "SplitOnDeath":
                child = ref(p.get("splitUnit"))
                cnt = float(f(p, "magnitude"))
                o["owner"].append(f"split_unit = {child['data'].get('id') if child else 'null'} · split_count = {int(cnt)}")
                o["notes"].append(f"mechanics[{i}] SplitOnDeath → 적 고유 값" + ("" if int(cnt) == cnt else " · 자식 수 비정수(손실)"))
                if i < len(mech) - 1:
                    o["notes"].append("라벨 번호 변화(해시 밖): 분열 뒤 줄의 「mechanic N」이 당겨진다")
                continue
            notes = []
            v = from_legacy(p, False, notes)
            v["_id"] = unique(f"{base}_{slot}", notes)
            fc = 1 if v["kind"] == "UltimateLeap" else 0
            row(o["rows"], slot, f"mechanics[{i}]", trig(m_.get("trigger")), fc, v, refs_of(p), notes,
                f"{name} mechanic {i}", f"{name} mechanic {slot}")
            slot += 1
        owners.append(o)
    return owners


# ── 왕복 검사: 이전 뒤 굽기가 싣는 겸직 칸 == 라이브 굽기 스냅샷 ──────────────

def snapshot_rules():
    rules = {}
    for fn in ("binding_bake_snapshot.txt", "card_bake_snapshot.txt"):
        txt = open(os.path.join(FIX, fn), encoding="utf-8").read()
        for block in re.split(r"\n(?=\[rule )", txt):
            m = re.match(r"\[rule \d+\] (.*)", block)
            if m:
                rules[m.group(1)] = dict(re.findall(r"^(\w+)=(.*)$", block, re.M))
    return rules


CARD_TRANSFORMED = {"SelfStatBuff", "SelfBuffLethal", "DreamCocoon", "BountyMark", "PlacementAura"}


def roundtrip(owners):
    snap = snapshot_rules()
    checked, bad, missing = 0, [], []
    for o in owners:
        for r in o["rows"]:
            v = r["values"]
            if v["kind"] in CARD_TRANSFORMED or v["kind"] in ("HeavyStrike", "RecallAttachedToFront"):
                continue  # 굽기가 % → 배율 / 공격 수식자로 접는다 — 겸직 칸 그대로가 아니다
            s = snap.get(r["old_label"])
            if s is None:
                missing.append(r["old_label"])
                continue
            leg = to_legacy(v)
            if leg is None:
                continue
            m, t, d = leg
            got = (float(s["magnitude"]), int(s["tileRange"]), float(s["duration"]))
            want = (m, max(0, t), max(0.0, d))
            checked += 1
            if abs(got[0] - want[0]) > 1e-4 or got[1] != want[1] or abs(got[2] - want[2]) > 1e-4:
                bad.append(f"{r['old_label']}: 스냅샷 M/T/D {got} ≠ 이전 뒤 {want}")
            if "damage" in v and v["kind"] in ("EmitProjectilePattern", "SpawnHazard", "SelfBlink", "UltimateLeap"):
                sd = float(s.get("damage", 0))
                if abs(sd - v["damage"]) > 1e-4:
                    bad.append(f"{r['old_label']}: 스냅샷 damage {sd} ≠ 효과 damage {v['damage']}")
    return checked, bad, missing


def values_text(v):
    parts = []
    for k in ("damage", "shield", "percent", "mul", "count", "radius_tiles", "range_tiles", "duration_sec", "flight_sec",
              "tick_sec", "stack_cap", "speed", "cone_half_deg", "density_radius_tiles", "landing_ring_tiles"):
        if v.get(k):
            parts.append(f"{k} {num(v[k])}")
    if v["kind"] in ("ApplyCcToTarget", "AreaCc"):
        parts.append(f"cc_kind {v['cc_kind']}")
    if v["kind"] in ("ApplyStackToTarget", "AreaApplyStack"):
        parts.append(f"stack_kind {v['stack_kind']}")
    if v["kind"] in ("SelfStatBuff", "DreamCocoon", "AllyStatAura", "OpponentStatAura"):
        parts.append(f"buff_stat {v['buff_stat']}")
    if v.get("shield_filter"):
        parts.append(f"shield_filter {v['shield_filter']} · includes_self 1")
    if v.get("telegraph"):
        parts.append("telegraph 1")
    return parts


FLAG = ("손실", "해시 변화", "확인", "버림", "충돌", "규칙 밖")


def main():
    owners = plan()
    checked, bad, missing = roundtrip(owners)
    rows = sum(len(o["rows"]) for o in owners)
    flagged = sum(1 for o in owners for r in o["rows"] if any(w in n for n in r["notes"] for w in FLAG))
    by_kind = {}
    for o in owners:
        by_kind[o["kind"]] = by_kind.get(o["kind"], 0) + len(o["rows"])
    lines = ["# skill-data-table unit 4 — 이전 dry-run 표(헤드리스 · 승인 전)", "",
             "> 생성: `python3 tools/skill-data-table/dry_run.py` — 에셋 YAML 위에서 `LegacyBindingMigration` 과 같은 규칙을 돌렸다(에셋 0).",
             "> 정본 = Unity 메뉴 `Wassup/BattleCore/Skill Data Table/이전 dry-run (표만 쓴다)` → `dry_run_table.md`. 둘이 다르면 Unity 쪽이 맞다.",
             "",
             f"소유자 {len(owners)} · 효과 줄 {rows}(카드 {by_kind.get('card', 0)} · 유닛 {by_kind.get('unit', 0)} · 적 {by_kind.get('enemy', 0)} · **병합 0** — U13) · 깃발 달린 줄 {flagged}",
             "",
             f"**왕복 검사**(이전 뒤 굽기가 싣는 겸직 칸 magnitude · tileRange · duration + 피해 = 라이브 굽기 스냅샷): 대조 {checked} 줄 · 불일치 {len(bad)} · 스냅샷에 없는 줄 {len(missing)}"
             + " (카드 %·배율 인코딩 5종 · 강공 · 인수인계(손패 선언)는 굽기가 값을 접어 대조 밖 — `BindingSpecBakeTests` 가 Unity 에서 전 칸을 잰다)",
             ""]
    for b in bad:
        lines.append(f"- ❌ {b}")
    for m in missing:
        lines.append(f"- (스냅샷에 없음 — 옛 굽기가 건너뛴 줄?) {m}")
    lines += ["", "깃발 = 손실 · 해시 변화 · 확인 필요 · 버림. 「라벨 변화」는 해시 밖(굽기 스냅샷 텍스트만). 효과 에셋 = `Assets/_Project/Data/Effects/Effect_{effect_id}.asset`.", ""]
    for o in owners:
        lines.append(f"## {o['kind']} `{o['id']}` — {o['path']}")
        lines.append("")
        for n in o["owner"] + o["notes"]:
            lines.append(f"- {n}")
        if o["rows"]:
            lines += ["", "| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |", "|---|---|---|---|---|---|---|"]
            for r in o["rows"]:
                v = r["values"]
                vt = " · ".join(values_text(v) + r["refs"]) or "—"
                lines.append(f"| {r['slot']} | {r['source']} | `{v['_id']}` | {v['kind']} | {r['trigger']} | {vt} | {'<br>'.join(r['notes'])} |")
        lines.append("")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))
    print(f"owners={len(owners)} rows={rows} flagged={flagged} roundtrip checked={checked} bad={len(bad)} missing={len(missing)} -> {os.path.relpath(OUT, ROOT)}")
    for b in bad:
        print("BAD", b)
    for m in missing:
        print("MISSING", m)


if __name__ == "__main__":
    main()

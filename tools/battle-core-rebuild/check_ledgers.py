#!/usr/bin/env python3
"""battle-core-rebuild unit 0 · 항목 7 — 장부 ↔ 코드 대조.

  python3 Tools/battle-core-rebuild/check_ledgers.py            # 대조 (불일치 = exit 1)
  python3 Tools/battle-core-rebuild/check_ledgers.py --generate # 장부 초안 생성(기존 분류는 보존)

대조 대상:
  ledgers/bridge-methods.md  ↔  Assets/_Project/Scripts/Bridge/BattleBridge*.cs 의 메서드 선언
  ledgers/bridge-fields.md   ↔  같은 파일들의 [SerializeField] 선언 + BattleScene.unity 의 브리지 블록 키
누락(코드에 있는데 장부에 없음)·유령(장부에 있는데 코드에 없음) 둘 다 실패.
"""
import re, sys, os, glob, collections
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BRIDGE_GLOB = os.path.join(ROOT, 'Assets/_Project/Scripts/Bridge/BattleBridge*.cs')
SCENE = os.path.join(ROOT, 'Assets/_Project/Scenes/BattleScene.unity')
BRIDGE_META = os.path.join(ROOT, 'Assets/_Project/Scripts/Bridge/BattleBridge.cs.meta')
LEDGER_DIR = os.path.join(ROOT, 'docs/spec/battle-core-rebuild/ledgers')
METHODS_MD = os.path.join(LEDGER_DIR, 'bridge-methods.md')
FIELDS_MD = os.path.join(LEDGER_DIR, 'bridge-fields.md')

METHOD_RE = re.compile(
    r'^\s*(?:\[[^\]]+\]\s*)*'                                   # attributes
    r'(?:public|private|protected|internal)\s+'                # access
    r'(?:static\s+|override\s+|virtual\s+|readonly\s+|unsafe\s+|partial\s+|async\s+|new\s+)*'
    r'(?!class\b|struct\b|enum\b|interface\b|delegate\b|event\b)'
    r'[\w<>\[\],\.\?\s]+?\s+'                                  # return type
    r'(?P<name>[A-Za-z_]\w*)\s*(?:<[^>]+>)?\s*\((?P<params>[^)]*)\)\s*(?:where\b[^{]*)?(?:\{|=>)',
    re.M)
FIELD_RE = re.compile(r'\[SerializeField[^\]]*\]\s*(?:\[[^\]]+\]\s*)*(?:private|public|internal|protected)?\s*(?:readonly\s+)?[\w<>\[\],\.\?]+\s+(?P<name>\w+)\s*(?:=|;)', re.S)

def strip_comments(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return re.sub(r'//[^\n]*', '', s)

def code_methods():
    out = []
    for f in sorted(glob.glob(BRIDGE_GLOB)):
        src = strip_comments(open(f, encoding='utf-8').read())
        rel = os.path.basename(f)
        for m in METHOD_RE.finditer(src):
            name = m.group('name')
            if name in ('if','for','foreach','while','switch','catch','using','return','lock'): continue
            arity = 0 if not m.group('params').strip() else m.group('params').count(',') + 1
            out.append((rel, f"{name}/{arity}"))
    return out

def code_fields():
    out = []
    for f in sorted(glob.glob(BRIDGE_GLOB)):
        src = strip_comments(open(f, encoding='utf-8').read())
        for m in FIELD_RE.finditer(src):
            out.append((os.path.basename(f), m.group('name')))
    return out

def scene_fields():
    guid = re.search(r'guid: (\w+)', open(BRIDGE_META).read()).group(1)
    s = open(SCENE, encoding='utf-8').read()
    blocks = re.split(r'(?m)^--- !u!', s)
    for b in blocks:
        if f'guid: {guid}' in b and b.startswith('114'):
            body = b.split('\n', 1)[1]
            keys = re.findall(r'^  (\w+):', body, re.M)
            skip = {'m_ObjectHideFlags','m_CorrespondingSourceObject','m_PrefabInstance','m_PrefabAsset','m_GameObject','m_Enabled','m_EditorHideFlags','m_Script','m_Name','m_EditorClassIdentifier'}
            return [k for k in keys if k not in skip]
    return []

def ledger_keys(path):
    if not os.path.exists(path): return {}
    rows = {}
    for line in open(path, encoding='utf-8'):
        if not line.startswith('| ') or line.startswith('| #') or line.startswith('|--'): continue
        cells = [c.strip() for c in line.strip().strip('|').split('|')]
        if len(cells) < 4: continue
        key = cells[1].strip('`')
        rows[key] = cells
    return rows

def generate():
    os.makedirs(LEDGER_DIR, exist_ok=True)
    # methods
    old = ledger_keys(METHODS_MD)
    ms = code_methods()
    by_file = collections.OrderedDict()
    for rel, key in ms: by_file.setdefault(rel, []).append(key)
    def guess(key, rel):
        n = key.split('/')[0]
        rules = [
            (r'^Drain', '뷰 풀 / 담당자 구독 (이벤트로 접힘)'), (r'^Enqueue', '삭제 (코어 내부 호출)'),
            (r'^(Try|Can)(Begin|Place|Retire|Relocate)|Placement|Footprint|Occupy|Release', 'PlacementService'),
            (r'Wave|Spawn(Unit|Wave)|Bonus|Pull', 'WaveScheduler'), (r'Cost', 'CostLedger'),
            (r'Goal|Stability|Stress|Heart|Core(Shield|Burst)', 'HeartMeter'), (r'Score|Tally|Kill', 'ScoreLedger'),
            (r'Timer|EndMatch|Submit|StartBattle|StopBattle|Teardown|BeginPlacement', 'MatchClock'),
            (r'Dreamcatcher|Card|Attach|Awakening|Hand|Dc[A-Z]', 'HandDeck'), (r'Gimmick|Meteor|Resignation|Pickup', 'GimmickHost'),
            (r'Map|Flow|Nav|Tile|Cell', 'MapRuntime (코어)'), (r'Skill|Cast|Immediate', 'BindingRegistry / TriggerDispatcher'),
            (r'Create|Entity|Bake|Attach(SimEntityId)', '삭제 (코어 스폰 = BattleWorld.Spawn*)'),
            (r'View|Visual|Vfx|Sync|Mirror|Lift|Shadow|Beam|Anim', '뷰 풀'), (r'Config|Hash|Collect', 'MatchDefinitionBuilder'),
            (r'Debug|Log|Trace', '디버그/로그 (도구 처분표)'),
        ]
        for pat, owner in rules:
            if re.search(pat, n): return owner
        return '미정'
    lines = ['# 장부 — 브리지 메서드 귀속표 (unit 0 · 항목 6 · bridge-methods.md)', '',
             '> 생성/갱신: `python3 Tools/battle-core-rebuild/check_ledgers.py --generate`. 키 = `이름/인자수`. 「새 주인」 열은 사람이 고친다(재생성 시 보존). 「미정」은 조각 E 진입 전 0 이어야 한다. 접두사 휴리스틱 초안이므로 **틀린 귀속이 있을 수 있다** — 유닛별로 옮길 때 그 파일의 행을 확정한다.', '',
             f'총 {len(ms)} 선언 · 파일 {len(by_file)}', '']
    for rel, keys in by_file.items():
        lines += [f'## {rel} ({len(keys)})', '', '| # | 메서드 | 새 주인 | 비고 |', '|---|---|---|---|']
        for i, k in enumerate(keys, 1):
            prev = old.get(k)
            owner = prev[2] if prev and prev[2] else guess(k, rel)
            note = prev[3] if prev and len(prev) > 3 else ''
            lines.append(f'| {i} | `{k}` | {owner} | {note} |')
        lines.append('')
    open(METHODS_MD, 'w', encoding='utf-8').write('\n'.join(lines))
    # fields
    old = ledger_keys(FIELDS_MD)
    fs = code_fields(); sc = scene_fields()
    scene_set = set(sc)
    lines = ['# 장부 — 브리지 직렬화 필드 귀속표 (unit 0 · 항목 6 · bridge-fields.md)', '',
             '> 생성/갱신: 같은 스크립트 `--generate`. 코드 `[SerializeField]` 선언과 `BattleScene.unity` 브리지 블록 키를 대조한다. 「새 주인」= 담당자별 SO 또는 뷰 풀 컴포넌트(unit 5 에서 새 씬에 배선). 「씬 값」은 unit 5 이사 시 대조표로 쓴다.', '',
             f'코드 선언 {len(fs)} · 씬 블록 키 {len(sc)}', '',
             '| # | 필드 | 파일 | 씬에 있음 | 새 주인 | 비고 |', '|---|---|---|---|---|---|']
    for i, (rel, name) in enumerate(fs, 1):
        prev = old.get(name)
        owner = prev[4] if prev and len(prev) > 4 and prev[4] else '미정'
        note = prev[5] if prev and len(prev) > 5 else ''
        lines.append(f'| {i} | `{name}` | {rel} | {"○" if name in scene_set else "—"} | {owner} | {note} |')
    only_scene = [k for k in sc if k not in {n for _, n in fs}]
    if only_scene:
        lines += ['', '### 씬에만 있는 키(코드 선언 없음 — public 필드 또는 stale)', '', ', '.join(f'`{k}`' for k in only_scene)]
    open(FIELDS_MD, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print(f'generated: methods {len(ms)}, fields {len(fs)} (scene keys {len(sc)})')

def check():
    ok = True
    ms = {k for _, k in code_methods()}; lm = set(ledger_keys(METHODS_MD).keys())
    miss, ghost = ms - lm, lm - ms
    if miss or ghost:
        ok = False; print(f'[bridge-methods] 누락 {len(miss)} · 유령 {len(ghost)}')
        for k in sorted(miss)[:20]: print('   누락', k)
        for k in sorted(ghost)[:20]: print('   유령', k)
    else: print(f'[bridge-methods] OK ({len(ms)})')
    fs = {n for _, n in code_fields()}; lf = set(ledger_keys(FIELDS_MD).keys())
    miss, ghost = fs - lf, lf - fs
    if miss or ghost:
        ok = False; print(f'[bridge-fields] 누락 {len(miss)} · 유령 {len(ghost)}')
        for k in sorted(miss)[:20]: print('   누락', k)
        for k in sorted(ghost)[:20]: print('   유령', k)
    else: print(f'[bridge-fields] OK ({len(fs)}, 씬 키 {len(scene_fields())})')
    if ok:
        pend = sum(1 for r in ledger_keys(METHODS_MD).values() if r[2] == '미정')
        print(f'[bridge-methods] 미정 {pend} (조각 E 진입 조건: 0)')
    return 0 if ok else 1

if __name__ == '__main__':
    if '--generate' in sys.argv: generate(); sys.exit(check())
    sys.exit(check())

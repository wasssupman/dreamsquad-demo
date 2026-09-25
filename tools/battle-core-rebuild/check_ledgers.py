#!/usr/bin/env python3
"""battle-core-rebuild unit 0 · 항목 7 — 장부 ↔ 코드 대조.

  python3 Tools/battle-core-rebuild/check_ledgers.py            # 대조 (불일치 = exit 1)
  python3 Tools/battle-core-rebuild/check_ledgers.py --generate # 장부 초안 생성(기존 분류는 보존)
  python3 tools/battle-core-rebuild/check_ledgers.py --retire-assets   # 8c — 자산 도달성(GUID 폐포) + 옛 경로 문자열
  python3 tools/battle-core-rebuild/check_ledgers.py --owners          # 8c — 「새 주인」·「실현 위치」 칸이 코드 심볼로 해석되나
  python3 tools/battle-core-rebuild/check_ledgers.py --retire-prune <export>  # 8c — export 사본에서 퇴역 목록을 지운다(Retire.Check lane 앞)

대조 대상:
  ledgers/bridge-methods.md  ↔  Assets/_Project/Scripts/Bridge/BattleBridge*.cs 의 메서드 선언
  ledgers/bridge-fields.md   ↔  같은 파일들의 [SerializeField] 선언 + BattleScene.unity 의 브리지 블록 키
누락(코드에 있는데 장부에 없음)·유령(장부에 있는데 코드에 없음) 둘 다 실패.
브리지가 지워진 뒤(unit 9)에는 두 장부가 이력으로 동결되고, 기본 대조는 「미정 0」만 본다.
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
    if not glob.glob(BRIDGE_GLOB):
        # unit 9 — 브리지는 옛 전투와 함께 지워졌다. 두 장부는 이력(동결 행)이고 대조할 코드가 없다.
        # 「새 주인」 칸이 코드로 실현됐는지는 `--owners` 가 계속 묻는다.
        nm, nf = len(ledger_keys(METHODS_MD)), len(ledger_keys(FIELDS_MD))
        pend = sum(1 for r in ledger_keys(METHODS_MD).values() if r[2] == '미정')
        print(f'[bridge-methods] 브리지 퇴역(unit 9) — 장부 {nm}행 동결 · 미정 {pend}')
        print(f'[bridge-fields] 브리지 퇴역(unit 9) — 장부 {nf}행 동결')
        return 0 if pend == 0 else 1
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

# ─────────────────────────────────────────────────────────────────────────────
# battle-core-rebuild unit 8c — 「지울 수 있다」의 기계 증명 셋.
#   ① 코드 도달성은 여기서 재지 않는다 — `--retire-prune` 로 export 사본을 가지치고 `headless/Retire.Check.csproj`
#      를 컴파일한다(컴파일러에게 묻는다).
#   ② `--retire-assets` = 뿌리(빌드 설정의 씬 · Resources · ProjectSettings 참조 · retire-set 의 dev 씬)에서 `guid:` 를
#      전이로 따라간 폐포 안에 퇴역 스크립트·자산이 있으면 실패 + 남는 코드의 옛 경로 문자열.
#   ③ `--owners` = 장부의 「새 주인」(bridge-methods) · 새 주인 심볼(bridge-fields 비고) · 「실현 위치」(rule-holders)가
#      **남는 코드의 심볼**로 해석되나. 「삭제」는 심볼이 필요 없다. 「미실현」은 실패다(배정만 되고 실체가 없다).
RETIRE_MD = os.path.join(LEDGER_DIR, 'retire-set.md')
ALIASES_MD = os.path.join(LEDGER_DIR, 'owner-aliases.md')
RULES_MD = os.path.join(LEDGER_DIR, 'rule-holders.md')

def retire_blocks(path=RETIRE_MD):
    """```retire / ```hold / ```roots 펜스 블록의 줄(저장소 루트 상대 경로). `#` 줄·빈 줄은 무시."""
    out = {'retire': [], 'hold': [], 'roots': []}
    cur = None
    for line in open(path, encoding='utf-8'):
        t = line.strip()
        if cur is None:
            m = re.match(r'^```(retire|hold|roots)\s*$', t)
            if m: cur = m.group(1)
            continue
        if t.startswith('```'): cur = None; continue
        if t and not t.startswith('#'): out[cur].append(t)
    return out

def expand(entries, root=ROOT):
    """항목(파일 또는 `/` 로 끝나는 폴더)을 실제 파일 목록으로. .meta 는 뺀다(짝으로 따라간다)."""
    files = []
    for e in entries:
        p = os.path.join(root, e.rstrip('/'))
        if os.path.isdir(p):
            for dp, _, fn in os.walk(p):
                files += [os.path.join(dp, f) for f in fn if not f.endswith('.meta')]
        elif os.path.exists(p):
            files.append(p)
    return files

def retire_prune(target):
    target = os.path.abspath(target)
    # 목록은 **이 스크립트가 사는 트리**의 retire-set.md 에서 읽고, 지우는 곳은 target 이다. git 트리는 거부한다 —
    # 워크트리를 가지치면 되돌릴 수 없는 삭제가 된다(export 사본에는 .git 이 없다).
    if target == os.path.abspath(ROOT) or os.path.exists(os.path.join(target, '.git')):
        print('✖ git 트리는 가지치지 않는다 — git archive 로 만든 export 사본을 넘겨라'); return 2
    b = retire_blocks(); n = 0
    for e in b['retire'] + b['hold']:
        p = os.path.join(target, e.rstrip('/'))
        for q in (p, p + '.meta'):
            if os.path.isdir(q):
                import shutil; shutil.rmtree(q); n += 1
            elif os.path.exists(q):
                os.remove(q); n += 1
    print(f'[retire-prune] {target} 에서 {n} 항목을 지웠다(retire {len(b["retire"])} + hold {len(b["hold"])} 줄)')
    return 0

def _cs_lines(files):
    return sum(sum(1 for _ in open(f, encoding='utf-8', errors='ignore')) for f in files if f.endswith('.cs'))

def retire_counts():
    b = retire_blocks()
    r = expand(b['retire']); h = expand(b['hold'])
    return {'retire_files': len(r), 'retire_cs': sum(f.endswith('.cs') for f in r), 'retire_lines': _cs_lines(r),
            'hold_files': len(h), 'hold_lines': _cs_lines(h)}

GUID_RE = re.compile(rb'guid: ?([0-9a-f]{32})')
YAML_EXT = ('.unity', '.prefab', '.asset', '.mat', '.controller', '.anim', '.overrideController', '.playable', '.mask',
            '.spriteatlas', '.spriteatlasv2', '.lighting', '.mixer', '.physicMaterial', '.physicsMaterial2D', '.terrainlayer',
            '.brush', '.signal', '.preset', '.guiskin', '.fontsettings', '.renderTexture', '.flare', '.cubemap', '.giparams',
            '.shadervariants', '.inputactions', '.uss', '.uxml', '.tss', '.shadergraph', '.shadersubgraph', '.vfx')

def guid_map():
    g2p, p2g = {}, {}
    for dp, _, fn in os.walk(os.path.join(ROOT, 'Assets')):
        for f in fn:
            if not f.endswith('.meta'): continue
            try: m = re.search(r'guid: ([0-9a-f]{32})', open(os.path.join(dp, f), errors='ignore').read(4000))
            except OSError: continue
            if m:
                rel = os.path.relpath(os.path.join(dp, f[:-5]), ROOT)
                g2p[m.group(1)] = rel; p2g[rel] = m.group(1)
    return g2p, p2g

def _refs(rel):
    full = os.path.join(ROOT, rel); out = set()
    if os.path.isdir(full): return out
    if rel.endswith(YAML_EXT):
        try:
            b = open(full, 'rb').read()
            if b[:5] == b'%YAML' or not rel.endswith(('.unity', '.prefab', '.asset')):
                out |= {x.decode() for x in GUID_RE.findall(b)}
        except OSError: pass
    try:
        mb = open(full + '.meta', 'rb').read(); own = GUID_RE.search(mb)
        out |= {x.decode() for x in GUID_RE.findall(mb)}
        if own: out.discard(own.group(1).decode())
    except OSError: pass
    return out

def asset_roots(extra):
    roots = []
    bs = open(os.path.join(ROOT, 'ProjectSettings/EditorBuildSettings.asset'), encoding='utf-8').read()
    for enabled, path in re.findall(r'- enabled: (\d)\n\s+path: (\S+)', bs):
        if enabled == '1': roots.append(path)
    for dp, _, fn in os.walk(os.path.join(ROOT, 'Assets')):
        parts = os.path.relpath(dp, ROOT).split(os.sep)
        if 'Resources' in parts and 'Editor' not in parts:
            roots += [os.path.relpath(os.path.join(dp, f), ROOT) for f in fn if not f.endswith('.meta')]
    return roots + list(extra)

def closure(roots, g2p):
    seen, parent, stack = set(), {}, list(roots)
    ps = os.path.join(ROOT, 'ProjectSettings')
    for f in os.listdir(ps):
        if f.endswith('.asset'):
            for g in GUID_RE.findall(open(os.path.join(ps, f), 'rb').read()):
                q = g2p.get(g.decode())
                if q: stack.append(q); parent.setdefault(q, 'ProjectSettings/' + f)
    while stack:
        p = stack.pop()
        if p in seen: continue
        seen.add(p)
        for g in _refs(p):
            q = g2p.get(g)
            if q and q not in seen: parent.setdefault(q, p); stack.append(q)
    return seen, parent

OLD_PATH_RE = re.compile(r'Scenes/BattleScene(?:\.unity|/)|"BattleScene"|Scripts/Battle/|Scripts/Bridge/|Editor/Battle/|Tests/Golden"')

def _code_only(src):
    src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    return '\n'.join(re.sub(r'^\s*//.*$', '', l) for l in src.split('\n'))

def retire_assets():
    b = retire_blocks(); ok = True
    g2p, p2g = guid_map()
    retired = {os.path.relpath(f, ROOT) for f in expand(b['retire'] + b['hold'])}
    roots = asset_roots(b['roots'])
    seen, parent = closure(roots, g2p)
    hit = sorted(p for p in seen if p in retired)
    if hit:
        ok = False; print(f'[retire-assets] ✖ 뿌리 폐포 안에 퇴역 항목 {len(hit)}')
        for p in hit[:30]:
            chain, q = [p], p
            while q in parent and len(chain) < 6: q = parent[q]; chain.append(q)
            print('   ', ' ← '.join(chain))
    else:
        scripts = sum(1 for p in retired if p.endswith('.cs'))
        print(f'[retire-assets] OK — 뿌리 {len(roots)} · 폐포 {len(seen)} · 퇴역 스크립트 {scripts} 중 폐포 안 0')
    # 남는 코드의 옛 경로 문자열(주석 줄 제외) — 옛 씬·옛 폴더·옛 코퍼스를 문자열로 부르면 지우는 순간 런타임에 깨진다.
    strays = []
    for dp, _, fn in os.walk(os.path.join(ROOT, 'Assets/_Project')):
        for f in fn:
            if not f.endswith('.cs'): continue
            full = os.path.join(dp, f); rel = os.path.relpath(full, ROOT)
            if rel in retired: continue
            for i, line in enumerate(_code_only(open(full, encoding='utf-8', errors='ignore').read()).split('\n'), 1):
                if OLD_PATH_RE.search(line): strays.append(f'{rel}:{i}: {line.strip()[:120]}')
    if strays:
        ok = False; print(f'[retire-assets] ✖ 남는 코드에 옛 경로 문자열 {len(strays)}')
        for s_ in strays[:30]: print('   ', s_)
    else: print('[retire-assets] OK — 남는 코드의 옛 씬·옛 폴더 경로 문자열 0')
    # 머리말 수치가 목록과 맞나(목록을 고치고 수치를 안 고치는 드리프트)
    c = retire_counts(); head = open(RETIRE_MD, encoding='utf-8').read()
    if c['retire_files'] == 0 and c['hold_files'] == 0:
        # unit 9 — 목록이 전부 지워졌다. 총계 줄은 삭제 전 측정(이력)이라 대조하지 않는다.
        print('[retire-assets] OK — 퇴역 목록 전부 삭제됨(남은 항목 0) · 총계 줄은 삭제 전 측정 이력')
        return 0 if ok else 1
    m = re.search(r'총계: 퇴역 (\d+) 파일 · C# (\d+) 파일 · (\d+) 줄', head)
    want = (c['retire_files'], c['retire_cs'], c['retire_lines'])
    if not m or tuple(int(x) for x in m.groups()) != want:
        ok = False; print(f'[retire-assets] ✖ retire-set.md 총계 줄이 목록과 다르다 — 목록 기준: 「총계: 퇴역 {want[0]} 파일 · C# {want[1]} 파일 · {want[2]} 줄」')
    else: print(f'[retire-assets] OK — 총계 {want[0]} 파일 · C# {want[1]} · {want[2]} 줄 · 보류 {c["hold_files"]} 파일 {c["hold_lines"]} 줄')
    return 0 if ok else 1

# ── 심볼 해석 ────────────────────────────────────────────────────────────────
DECL_RE = re.compile(r'\b(?:class|struct|interface|enum|record)\s+([A-Za-z_]\w*)|\bdelegate\s+[\w<>\[\],.\s]+?\s+([A-Za-z_]\w*)\s*\(')

def symbol_index():
    b = retire_blocks()
    retired = {os.path.abspath(f) for f in expand(b['retire'] + b['hold'])}
    types, files = collections.defaultdict(list), {}
    for base in ('Assets/_Project/Scripts', 'Assets/_Project/Editor', 'Assets/_Project/Modules'):
        for f in glob.glob(os.path.join(ROOT, base, '**/*.cs'), recursive=True):
            if os.path.abspath(f) in retired: continue
            src = strip_comments(open(f, encoding='utf-8', errors='ignore').read())
            files[f] = set(re.findall(r'\b[A-Za-z_]\w*\b', src))
            for a, b2 in DECL_RE.findall(src): types[a or b2].append(f)
    basenames = {os.path.basename(f) for f in files}
    return types, files, basenames

def resolve(sym, idx):
    types, files, basenames = idx
    sym = sym.strip().strip('`').split('(')[0].split(' ')[0].rstrip('.,:;')
    if not sym: return False
    if sym.endswith('.cs'): return os.path.basename(sym) in basenames
    parts = [p for p in re.split(r'\.', sym) if p]
    for i, p in enumerate(parts):
        base = p.split('<')[0]
        if base in types:
            if i + 1 >= len(parts): return True
            member = parts[i + 1].split('<')[0]
            return any(member in files[f] for f in types[base])
    return False

def load_aliases():
    """owner-aliases.md 의 표: | 별칭 | 심볼 목록(백틱, `*` 는 타입 이름 와일드카드) | 뜻 |"""
    al = {}
    if not os.path.exists(ALIASES_MD): return al
    for line in open(ALIASES_MD, encoding='utf-8'):
        if not line.startswith('| ') or line.startswith('| 별칭') or line.startswith('|--'): continue
        c = [x.strip() for x in line.strip().strip('|').split('|')]
        if len(c) >= 2: al[c[0].strip('「」')] = re.findall(r'`([^`]+)`', c[1])
    return al

def _alias_ok(targets, idx):
    types = idx[0]
    for t in targets:
        if '*' in t:
            rx = re.compile('^' + re.escape(t).replace('\\*', '\\w*') + '$')
            if not any(rx.match(n) for n in types): return False
        elif not resolve(t, idx): return False
    return bool(targets)

def classify_cell(cell, idx, aliases):
    """→ ('삭제'|'미실현'|'심볼'|'별칭'|'실패', 토큰). 문법(8c 구현 5): 첫 백틱 토큰 = 코드 심볼. 백틱이 없으면
    맨 앞 식별자를 심볼로 본다. 자유 범주어는 owner-aliases.md 의 별칭일 때만 허용."""
    t = cell.strip()
    if t.startswith(('삭제', '없음')): return '삭제', t
    if t.startswith('미실현'): return '미실현', t
    m = re.search(r'`([^`]+)`', t)
    if m and t.index('`') == 0:
        return ('심볼' if resolve(m.group(1), idx) else '실패'), m.group(1)
    lead = re.match(r'^([A-Za-z_][\w.]*)', t)
    if lead and resolve(lead.group(1), idx): return '심볼', lead.group(1)
    for k in sorted(aliases, key=len, reverse=True):
        if t.startswith(k): return ('별칭' if _alias_ok(aliases[k], idx) else '실패'), k
    if m: return ('심볼' if resolve(m.group(1), idx) else '실패'), m.group(1)
    return '실패', (lead.group(1) if lead else t[:30])

def _table_rows(path, key_re=None):
    rows, sec = [], None
    for line in open(path, encoding='utf-8'):
        h = re.match(r'^## (.+)', line)
        if h: sec = h.group(1).strip(); continue
        if not line.startswith('| ') or line.startswith('| #') or line.startswith('|--'): continue
        c = [x.strip() for x in line.strip().strip('|').split('|')]
        if key_re and not re.match(key_re, c[0]): continue
        rows.append((sec, c))
    return rows

def owners():
    idx = symbol_index(); aliases = load_aliases(); ok = True
    for k, v in aliases.items():
        if not _alias_ok(v, idx): ok = False; print(f'[owners] ✖ 별칭 「{k}」 의 심볼 {v} 중 해석 안 되는 것이 있다')
    def report(name, results):
        nonlocal ok
        cnt = collections.Counter(r[0] for r in results)
        bad = [r for r in results if r[0] in ('실패', '미실현')]
        print(f'[owners] {name}: ' + ' · '.join(f'{k} {v}' for k, v in sorted(cnt.items())))
        if bad:
            ok = False
            for kind, key, tok in bad[:60]: print(f'    ✖ {kind:3} {key} → {tok[:90]}')
    res = []
    for sec, c in _table_rows(METHODS_MD):
        if len(c) < 3 or not c[1].startswith('`'): continue
        kind, tok = classify_cell(c[2], idx, aliases); res.append((kind, c[1].strip('`'), tok))
    report('bridge-methods 「새 주인」', res)
    res = []
    for sec, c in _table_rows(FIELDS_MD, r'^\d+$'):
        if len(c) < 6: continue
        if c[4].startswith('삭제'): res.append(('삭제', c[1], c[4])); continue
        note = c[5]
        m = re.search(r'새 주인 = `([^`]+)`', note) or re.search(r'`([^`]+)`', note)
        if note.startswith('미실현'): res.append(('미실현', c[1].strip('`'), note)); continue
        res.append((('심볼' if m and resolve(m.group(1), idx) else '실패'), c[1].strip('`'), m.group(1) if m else note[:40]))
    report('bridge-fields 새 주인(비고 첫 심볼)', res)
    res = []; nrows = 0
    for sec, c in _table_rows(RULES_MD, r'^[A-Z]{1,2}\d+$'):
        nrows += 1
        if len(c) < 6: res.append(('실패', c[0], '「실현 위치」 열 없음')); continue
        kind, tok = classify_cell(c[4], idx, aliases)
        if kind == '삭제' and len(c[4].strip()) <= 3: kind, tok = '실패', '삭제 근거 없음'
        res.append((kind, c[0], tok))
    report(f'rule-holders 「실현 위치」({nrows}행)', res)
    return 0 if ok else 1

if __name__ == '__main__':
    if '--retire-prune' in sys.argv:
        i = sys.argv.index('--retire-prune'); sys.exit(retire_prune(sys.argv[i + 1]))
    rc = 0
    if '--retire-assets' in sys.argv: rc |= retire_assets()
    if '--owners' in sys.argv: rc |= owners()
    if '--retire-assets' in sys.argv or '--owners' in sys.argv: sys.exit(rc)
    if '--generate' in sys.argv: generate(); sys.exit(check())
    sys.exit(check())

#!/usr/bin/env bash
# skill-data-table unit 8 — **헤드리스 상시 효과 이전 dry-run**(Unity 없음 · 에셋 쓰기 0 · 과도기 도구 — 단계 B 에서 지운다).
#
# 워크트리 에셋 YAML 에서 카드 입력 · 기존 효과 id 를 뽑고(python), **실제 소스**(`Scripts/**` — 순수 계획 `AlwaysOnEffectMigration`)를
# 한 실행 파일로 컴파일해 Unity 메뉴 dry-run 과 같은 함수(Build → Report)로 표를 쓴다. 참조 dll = 이 워크트리 Library(Retire.Check 와 같은 집합).
# 사용: dry_run_part2.sh [출력 md = docs/spec/skill-data-table/dry-run/dry_run_part2.md]
# ⚠ 임시 폴더에 빌드하고 끝나면 지운다(디스크 — KEEP_DRYRUN=1 이면 남긴다).
set -euo pipefail
REPO=$(cd "$(dirname "$0")/../.." && pwd)
OUTMD=${1:-$REPO/docs/spec/skill-data-table/dry-run/dry_run_part2.md}
UNITY=${UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents}
TMP=$(mktemp -d "${TMPDIR:-/tmp}/dryrun2-XXXXXX")
if [ "${KEEP_DRYRUN:-0}" != "1" ]; then trap 'rm -rf "$TMP"' EXIT; fi
DEF=$(python3 -c "import re,sys;s=open(sys.argv[1]).read();print(re.search(r'<DefineConstants>(.*?)</DefineConstants>',s).group(1))" "$REPO/tools/battle-core-rebuild/headless/Retire.Check.csproj")

cat > "$TMP/extract.py" <<'PY'
import os, re, glob, json, sys
repo = sys.argv[1]; out = sys.argv[2]
root = os.path.join(repo, 'Assets/_Project/Data')
guid2path = {}
for m in glob.glob(root + '/**/*.meta', recursive=True):
    with open(m) as f:
        for line in f:
            if line.startswith('guid:'):
                guid2path[line.split()[1]] = m[:-5]; break
def field(t, n, indent='  '):
    m = re.search(r'^' + indent + n + r': (.*)$', t, re.M)
    return m.group(1).strip() if m else None
def block(t, name):
    m = re.search(r'^  ' + name + r':(.*?)(?=^  [A-Za-z_]+:)', t, re.M | re.S)
    return m.group(1) if m else ''
effects = {}
for p in glob.glob(root + '/Effects/*.asset'):
    t = open(p, encoding='utf-8').read()
    if not re.search(r'Wassup\.Data\.EffectData$', t, re.M): continue
    eid = field(t, 'id'); kind = int(field(t, 'kind', '    '))
    af = field(t, 'allyFilter', '    ')
    effects[p] = dict(id=eid, kind=kind, allyFilter=int(af) if af is not None else 0)
cards = []
for p in sorted(glob.glob(root + '/Dreamcatcher/*.asset')):
    t = open(p, encoding='utf-8').read()
    if not re.search(r'Wassup\.Data\.DreamcatcherCard$', t, re.M): continue
    rel = os.path.relpath(p, repo)
    effs = [dict(kind=int(k), percent=float(v)) for k, v in re.findall(r'- kind: (\d+)\n    percent: ([-\d.eE]+)', block(t, 'effects'))]
    mods = [dict(kind=int(a), count=int(b), tileRange=int(c), damageMul=float(d)) for a, b, c, d in
            re.findall(r'- kind: (\d+)\n    count: (-?\d+)\n    tileRange: (-?\d+)\n    damageMul: ([-\d.eE]+)', block(t, 'attackMods'))]
    bl = block(t, 'bindings')
    bguids = re.findall(r'effect: \{fileID: 11400000, guid: (\w+)', bl)
    bfx = [effects.get(guid2path.get(g, '')) for g in bguids]
    aura_ids, aura_filters = [], []
    for e in bfx:
        if e and e['kind'] == 8 and e['id'] not in aura_ids:
            aura_ids.append(e['id']); aura_filters.append(e['allyFilter'])
    cards.append(dict(id=field(t, 'id'), source=rel, type=int(field(t, 'type')), axis=int(field(t, 'axis')),
                      hosts=int(field(t, 'hostKinds') or 1), effects=effs, mods=mods, bindingCount=len(bguids),
                      hasAlwaysOn=any(e and 39 <= e['kind'] <= 42 for e in bfx),
                      auraIds=aura_ids, auraFilters=aura_filters))
json.dump(dict(cards=cards, effectIds=sorted(e['id'] for e in effects.values())), open(out, 'w'), ensure_ascii=False, indent=1)
print('cards', len(cards), 'effects', len(effects))
PY
python3 "$TMP/extract.py" "$REPO" "$TMP/input.json"

cat > "$TMP/Program.cs" <<'CS'
// 헤드리스 dry-run 하네스(스크래치 · 커밋하지 않음): 추출 JSON → 순수 계획 AlwaysOnEffectMigration(실제 소스 그대로 컴파일) → 마크다운.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Wassup.Data;

public static class Program
{
    public static int Main(string[] args)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
        var inputs = new List<AlwaysOnEffectMigration.CardInput>();
        foreach (var c in doc.RootElement.GetProperty("cards").EnumerateArray())
        {
            var effects = new List<CardEffect>();
            foreach (var e in c.GetProperty("effects").EnumerateArray())
                effects.Add(new CardEffect { kind = (CardBuffKind)e.GetProperty("kind").GetInt32(), percent = (float)e.GetProperty("percent").GetDouble() });
            var mods = new List<DcAttackModSpec>();
            foreach (var m in c.GetProperty("mods").EnumerateArray())
                mods.Add(new DcAttackModSpec { kind = (DcAttackModKind)m.GetProperty("kind").GetInt32(), count = m.GetProperty("count").GetInt32(),
                                               tileRange = m.GetProperty("tileRange").GetInt32(), damageMul = (float)m.GetProperty("damageMul").GetDouble() });
            var auraIds = new List<string>();
            foreach (var a in c.GetProperty("auraIds").EnumerateArray()) auraIds.Add(a.GetString());
            var auraFilters = new List<CardTargetAxis>();
            foreach (var a in c.GetProperty("auraFilters").EnumerateArray()) auraFilters.Add((CardTargetAxis)a.GetInt32());
            inputs.Add(new AlwaysOnEffectMigration.CardInput
            {
                Id = c.GetProperty("id").GetString(), Source = c.GetProperty("source").GetString(),
                Type = (CardType)c.GetProperty("type").GetInt32(), Axis = (CardTargetAxis)c.GetProperty("axis").GetInt32(),
                Hosts = (HostKinds)c.GetProperty("hosts").GetInt32(), Effects = effects.ToArray(), AttackMods = mods.ToArray(),
                BindingCount = c.GetProperty("bindingCount").GetInt32(), HasAlwaysOnRows = c.GetProperty("hasAlwaysOn").GetBoolean(),
                PlacementAuraEffectIds = auraIds.ToArray(), PlacementAuraFilters = auraFilters.ToArray(),
            });
        }
        var ids = new List<string>();
        foreach (var e in doc.RootElement.GetProperty("effectIds").EnumerateArray()) ids.Add(e.GetString());
        var plan = AlwaysOnEffectMigration.Build(inputs, ids);
        File.WriteAllText(args[1], AlwaysOnEffectMigration.Report(plan, args[2]), new System.Text.UTF8Encoding(false));
        Console.WriteLine($"rows {plan.Rows.Count} · aura {plan.AuraFilters.Count} · notes {plan.Notes.Count}");
        return 0;
    }
}
CS

cat > "$TMP/DryRun.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><LangVersion>9</LangVersion><Nullable>disable</Nullable>
    <AssemblyName>DryRunPart2</AssemblyName><EnableDefaultCompileItems>false</EnableDefaultCompileItems><GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <NoWarn>\$(NoWarn);CS1701;CS1702;CS0618;CS0612;CS0649;CS0169;CS0414;CS0067;CS0162;CS0168;CS0219;CS8321;MSB3277;MSB3243;NU1701</NoWarn>
    <DefineConstants>$DEF</DefineConstants>
    <S>$REPO/Library/ScriptAssemblies</S>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$REPO/Assets/_Project/Scripts/**/*.cs" Exclude="$REPO/Assets/_Project/Scripts/SheetSync/**" />
    <Compile Include="$TMP/Program.cs" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="$UNITY/Resources/Scripting/Managed/UnityEngine/*.dll" Private="false" />
    <Reference Include="\$(S)/*.dll" Exclude="\$(S)/Wassup.Runtime.dll;\$(S)/Wassup.BattleCore.dll;\$(S)/Wassup.Skills.dll;\$(S)/Wassup.UnitAi.dll;\$(S)/Wassup.Tests.*.dll;\$(S)/Wassup.Editor.*.dll;\$(S)/Assembly-CSharp*.dll;\$(S)/*CodeGen*.dll;\$(S)/Wassup.DepthParallax.Tests.dll;\$(S)/Unity.Entities*.dll;\$(S)/Unity.Transforms*.dll;\$(S)/Unity.Serialization*.dll;\$(S)/Unity.Mathematics.Extensions.Hybrid.dll;\$(S)/Unity.PlasticSCM.Editor.Entities.dll" Private="false" />
    <Reference Include="$REPO/Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll" Private="false" />
  </ItemGroup>
</Project>
EOF
dotnet build "$TMP/DryRun.csproj" -c Debug -o "$TMP/bin" -nologo -v q 2>&1 | grep -E " error |오류 [0-9]+개|Build succeeded" | sort -u | tail -20
HEADREV=$(git -C "$REPO" rev-parse --short HEAD)
dotnet "$TMP/bin/DryRunPart2.dll" "$TMP/input.json" "$OUTMD" \
  "생성: 헤드리스 하네스 \`tools/skill-data-table/dry_run_part2.sh\`(워크트리 에셋 YAML 추출 → 실제 소스 AlwaysOnEffectMigration.Build/Report 컴파일 실행 · Unity 없음 · 에셋 경로 순) · 기준 커밋 $HEADREV · Unity 메뉴 dry-run 과 같은 함수"
echo "표: $OUTMD"

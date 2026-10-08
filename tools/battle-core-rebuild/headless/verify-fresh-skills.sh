#!/usr/bin/env bash
# unified-effect-layer unit 5 — 헤드리스 검증: 클린 export + **export 소스로 새로 구운 Somnia.Battle.Skills.dll**.
#
# 헤드리스 csproj 는 Skills dll 을 워크트리 `Library/ScriptAssemblies` 에서 받는다 — 에디터가 재컴파일하기 전이면 옛 dll 이라
# Skills 를 바꾼 커밋이 거짓 빨강/초록을 낸다. 이 스크립트는 export 의 Skills 소스로 dll 을 새로 구워 참조 폴더에 끼운다.
# ⚠ `BattleCoreUnity.Check` 는 여전히 옛 `Somnia.Battle.Runtime.dll` 을 참조한다 — Data/ 저작 타입에 새 필드·타입이 생긴 커밋은
#    그 lane 이 거짓 빨강(CS0246 · CS1061)이다. 그땐 전 소스 컴파일인 `Retire.Check` 가 증거다.
#
# 사용: verify-fresh-skills.sh [ref=HEAD] [덮어쓸 워크트리 파일 ...]
#   덮어쓸 파일을 주면 export 위에 워크트리 사본을 얹는다(커밋 전 검증용).
#   환경: VERIFY_OUT(출력 폴더) · UNITY_LIB(참조 dll 폴더, 기본 = 이 워크트리 Library/ScriptAssemblies)
#         · UNITY_ENGINE_DIR(엔진 모듈 폴더 — 6.6 부터 Unity.Mathematics 가 UnityEngine.MathematicsModule.dll 이다) · KEEP_VERIFY=1(끝나도 사본을 남긴다)
# ⚠ 리포 전체를 풀지 않는다 — 벤더 에셋까지 풀면 실행마다 ~0.9GB 가 쌓여 디스크를 채웠다(2026-09-28 ENOSPC).
#    헤드리스 csproj 가 읽는 경로만 푼다: Scripts · Editor · Tests · tools · 퇴역 장부(docs/spec/battle-core-rebuild).
#    실행이 끝나면 출력 폴더를 지운다(KEEP_VERIFY=1 이면 남긴다).
set -euo pipefail
REPO=$(cd "$(dirname "$0")/../../.." && pwd)
LIB=${UNITY_LIB:-$REPO/Library/ScriptAssemblies}
ENGINE=${UNITY_ENGINE_DIR:-"/c/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/Managed/UnityEngine"}
REF=${1:-HEAD}; shift || true
OUT=${VERIFY_OUT:-$(mktemp -d "${TMPDIR:-/tmp}/verify-XXXXXX")}
EXPORT=$OUT/export
ASM=$OUT/asm
mkdir -p "$EXPORT" "$ASM" "$OUT/skills"
if [ "${KEEP_VERIFY:-0}" != "1" ]; then trap 'rm -rf "$OUT"' EXIT; fi
git -C "$REPO" archive "$REF" -- Assets/_Project/Scripts Assets/_Project/Editor Assets/_Project/Tests tools docs/spec/battle-core-rebuild | tar -x -C "$EXPORT"
for f in "$@"; do mkdir -p "$EXPORT/$(dirname "$f")"; cp "$REPO/$f" "$EXPORT/$f"; done

# ① export 의 Skills 소스로 Somnia.Battle.Skills.dll 을 새로 굽는다(워크트리 Library 의 옛 dll 을 쓰지 않는다).
cat > "$OUT/skills/Somnia.Battle.Skills.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><Nullable>disable</Nullable>
    <AssemblyName>Somnia.Battle.Skills</AssemblyName><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo><ProduceReferenceAssembly>false</ProduceReferenceAssembly>
  </PropertyGroup>
  <ItemGroup><Compile Include="$EXPORT/Assets/_Project/Scripts/Skills/**/*.cs" /></ItemGroup>
  <ItemGroup><Reference Include="UnityEngine.MathematicsModule"><HintPath>$ENGINE/UnityEngine.MathematicsModule.dll</HintPath><Private>false</Private></Reference></ItemGroup>
</Project>
EOF
dotnet build "$OUT/skills/Somnia.Battle.Skills.csproj" -c Debug -o "$OUT/skills/bin" -nologo -v q 2>&1 | tail -3

# ② 참조 폴더 = Library 사본 + 새 Skills dll.
cp "$LIB"/*.dll "$ASM"/
cp "$OUT/skills/bin/Somnia.Battle.Skills.dll" "$ASM/Somnia.Battle.Skills.dll"

H=$EXPORT/tools/battle-core-rebuild/headless
P=(-p:UnityScriptAssemblies="$ASM" -p:UnityEngineDir="$ENGINE")
echo "── BattleCore.Tests (Category!=Golden)"
dotnet test "$H/BattleCore.Tests.csproj" --filter "Category!=Golden" "${P[@]}" -nologo 2>&1 | grep -E "error|Passed!|Failed!|통과|실패|합계|Total" | tail -20 || true
echo "── BattleCoreUnity.Check"
dotnet build "$H/BattleCoreUnity.Check.csproj" "${P[@]}" -nologo 2>&1 | grep -E " error |오류 [0-9]+개|Build succeeded|빌드했습니다" | sort -u | tail -20 || true
echo "── Retire.Check (가지치기 후)"
python3 "$EXPORT/tools/battle-core-rebuild/check_ledgers.py" retire-prune "$EXPORT" >/dev/null 2>&1 || echo "(retire-prune 실패/없음)"
dotnet build "$H/Retire.Check.csproj" "${P[@]}" -p:AllowUnpruned=true -nologo 2>&1 | grep -E " error |오류 [0-9]+개|Build succeeded|빌드했습니다" | sort -u | tail -20 || true
echo "out=$OUT"

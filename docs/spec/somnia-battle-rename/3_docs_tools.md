# 단위 3 — 문서 · 도구 · 메모리

코드가 정본인 설명 문서만 고친다(CLAUDE.md 「그 밖의 설명 문서가 코드와 다르면 그 문서가 낡은 것」). 닫힌 spec 은 이력이라 두고, 이 spec 의 README 가 대조표다.

| 문서 | 바뀌는 것 |
|---|---|
| `CLAUDE.md` | 어셈블리 이름(`Wassup.BattleCore` → `Somnia.Battle.BattleCore` 등) · 경로(`Scripts/BattleCore/` → `Runtime/Battle/Scripts/BattleCore/` · `Tests/EditModeCore/CoreArchitectureTests.cs` · `Editor/UnitStatImport` · `Data/…` · `Assets/Spine/version.txt`) · lane 이름 · 메뉴 루트. 제목의 별명 `(wassup)` 은 그대로 |
| `README.md` | 같은 종류 |
| `docs/blueprint/README.md` · `docs/reference/{battle-core-architecture,test-procedure,object-pipeline-map,map-stage-authoring,weapon-trail-authoring,…}.md` 6 · `lessons/` 2 | 어셈블리 · 경로 · 메뉴 |
| `docs/spec/README.md` | 「진행 중 / 직전 완료」 포인터 · lane 표 |
| `.claude/skills/enemy-wave-integration/SKILL.md` | 덱 경로 `Assets/_Project/Data/Decks/` → `Runtime/Battle/Data/Decks/` |
| `.claude/hooks/guardrails.mjs` · `agents/core-reviewer.md` · `skills/unity-{feature-wiring,vfx-integration}` | 단위 0 에서 이름, 여기서 경로 |
| `tools/battle/headless/*.csproj` 머리 주석 · `BattleCore.csproj` 의 맥 절대경로 기본값 | 단위 1 에서 이동, 여기서 서술 |
| 메모리 `somnia-migration-goal` · `clone-remote-and-unity-setup` | ④ 완료 · 새 lane 이름 · 메뉴 루트 |

확인: `grep -rn "Wassup\.\|_Project/Scripts\|_Project/Tests/EditMode[A-Z]\|_Project/Data\|Assets/Spine/" CLAUDE.md README.md docs/blueprint docs/reference .claude` 가 0.

## 구현 결과

(미착수)

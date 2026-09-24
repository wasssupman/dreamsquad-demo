# 8c — 브리지 폐쇄: 지울 수 있다는 증명 (조각 E · 3/3)

## 목적

**unit 9 가 「폴더를 지운다」 한 가지 일만 하게 만든다.** 지우는 순간 깨질 수 있는 것 — 살아남는 코드가 옛 타입을 부르는 곳, 옛 폴더 안에 사는 저작 타입(SO 스크립트 GUID), 옛 씬 폴더 안에 사는 자산, 장부가 「새 주인」이라 적었는데 실체가 없는 행 — 을 여기서 **기계로** 0 으로 만든다. `BattleBridge*.cs` 삭제 가능 조건 = 이 unit 의 완료 기준 전부.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 퇴역 집합 검사 | `tools/battle-core-rebuild/check_ledgers.py` `--retire-set` · 결과 `ledgers/retire-set.md`(신설) |
| 새 주인 실재 검사 | 같은 스크립트 `--owners` · `ledgers/rule-holders.md` 에 「실현 위치」 열 |
| 저작 타입 이사 | `Scripts/Battle/**` → `Scripts/Data/Authoring/`(`.meta` 동반 이동 — GUID 보존) |
| 옛 씬 폴더 자산 | `Scenes/BattleScene/{Duel,Street,Street_Day,Subway}.asset` → `Art/Theme/` 옆(`.meta` 동반) |
| 도구 장부 | `ledgers/tools.md` — 누락 1행 추가 |
| 파이프라인 맵 | `docs/reference/object-pipeline-map.md` 전면 재작성(README 「파이프라인 커버리지」의 약속) |

## 구현

1. **퇴역 집합은 손으로 적지 않고 도달성으로 뽑는다.** 뿌리 = 남는 씬(`OutgameScene`·`BattleCoreScene` + dev 씬 판정) · 자산(프리팹·SO)이 GUID 로 부르는 스크립트 · 남는 에디터 메뉴 · 남는 테스트 lane. 뿌리에서 닿지 않고 `BattleScene` 이나 옛 폴더에서만 닿는 `.cs` = 퇴역 집합. 검사 = **남는 파일이 퇴역 집합의 타입을 네임스페이스 기준으로 0 회 부른다**(같은 이름의 코어 사본 — `GridMath`·`NavGrid`·`StatKind` 등 — 을 옛 타입으로 오인하지 않도록 `using`·한정명으로 판별). 2026-09-25 실측으로 이미 드러난 「남는 쪽이 옛 것을 부르는」 곳:

   | 남는 파일 | 옛 타입(위치) |
   |---|---|
   | `BattleDriver.cs:64` · `BoardEffectDefinitionBuilder.cs:26·46·153·178` · `MatchViewAssets.cs:26~60` · `CoreHazardViewPool.cs:39·189` · `Data/ProjectileData.cs:166` · `Data/Abilities/HazardCastAbility.cs:17` · `Editor/BattleCore/CoreCardSelfCheckMenu.cs` | `BlockingHazardSO`(`Battle/Effects/` — **SO, 자산 3개가 스크립트 GUID 로 부른다**) |
   | `BoardEffectDefinitionBuilder.cs:72·100~121` · `Data/HazardSO.cs:10·20` | `HazardEffect`·`HazardShape`·`CcKind`·`DotElement`(`Battle/Effects/`) |
   | `Data/Abilities/HazardCastAbility.cs:15` | `HazardCastKind` |
   | `Data/StackModifierSO.cs` · `EffectTileData.cs` · `AttackOutput.cs` · `CombatDefinitionBuilder.cs` · `UI/Dreamcatcher/DreamcatcherCardText.cs` | `StatKind`·`StackKind`·`CombineOp`(`Battle/Effects/Modifiers/ModifierTypes.cs`) |
   | `DreamcatcherCardText.cs`(로비와 공유하는 단일 문안원 — 7c 「공유」) | `DcTrigger`·`NextAttackDoubleFire`·`DreamCocoon` |
   | `Data/AttackUnitData.cs:86` | `EnemyTargetDefaults.DefaultEnemyMask`(`Battle/Combat/EnemyTargetFilter.cs`) |

2. **저작 타입 이사.** 위 타입을 옛 폴더 밖으로 옮긴다. SO·MonoBehaviour 는 **파일과 `.meta` 를 같이** 옮겨 스크립트 GUID 를 보존한다(자산의 `m_Script` 가 GUID 로 부른다). ECS 컴포넌트와 한 파일에 있는 열거형(`CcKind` ⊂ `CcEffect.cs`, `DotElement` ⊂ `DotEffect.cs`)은 열거형만 떼어 낸다. **네임스페이스는 이 unit 에서 바꾸지 않는다** — 옛 전투가 아직 컴파일돼야 하고, 이름 정리는 unit 9 가 한 번에 한다. 옛 폴더를 건드리므로 커밋 첫 줄에 `[old-battle]`(동결 훅 — 규칙 변경 없음을 메시지에 적는다).
3. **옛 씬 폴더의 자산을 먼저 뺀다.** `Scenes/BattleScene/` 의 4개는 씬 부속이 아니라 **스테이지 프리팹 5개가 부르는 볼륨 프로필**이다(`MapStage_Duel`·`_Street`·`_StreetDay`·`_Subway`·`_Building`). unit 9 가 씬과 폴더를 같이 지우면 새 씬 스테이지의 포스트 효과가 조용히 빠진다.
4. **새 주인 실재 검사.** `bridge-methods` 367 · `bridge-fields` 91 · `rule-holders` 133 의 「삭제」 아닌 행마다 새 주인 이름이 코드 심볼로 존재해야 한다. 8a 가 드러낸 실체 없는 배정(bridge-fields 1·31·55)이 이 검사가 잡아야 할 모양이다. `rule-holders` 는 행마다 「실현 위치(파일:심볼)」를 적는다 — 「뷰」 31 행이 특히 그렇다(G17 처럼 「뷰」로 배정됐지만 앱 전역 훅이던 것).
5. **도구 장부 누락 1행.** `Presentation/ReachDebugGizmos.cs` — 브리지 전용 자료(`BattleBridge.DebugReachSphere`)를 그리는 디버그 도구인데 `tools.md` 11행에 없다. 처분 = **은퇴**: 새 코어의 도달 자는 `CoreMapOverlay`(링·채움·도형 가이드)가 그리고, 수치 정합은 `AttackReachParityTests`(20,000건)가 증언했다. `SimOrderDumpMenu`(4행)는 unit 9 그대로.
6. **파이프라인 맵 전면 재작성.** 아키타입 표마다 정거장 = 저작 SO → `MatchDefinitionBuilder` 정의표 행 → 코어 스폰/사건(`CoreEventKind` 번호) → 뷰 풀(`Core*`) → `ViewOrder` → 소멸 사건 회수. 옛 정거장(엔티티 조립·NativeQueue 드레인·브리지 폴링)은 한 줄 이력으로만. 5a·6b·6c 가 연 부분 표와 8a 의 보너스 포탈을 흡수한다.
7. **`Faction` 네임스페이스 결정(에이전트).** `Scripts/Skills/Faction.cs:11` 은 `Wassup.Battle.Units` 이고 리뷰어가 이것을 두 번 「죽은 import」로 오판했다(인계 §3-9·함정 16). 옛 폴더가 사라지면 이 이름은 **존재하지 않는 전투의 이름**이 된다 → unit 9 에서 `Wassup.Skills` 로 옮긴다(217 파일 기계적 치환). 여기서는 결정만 기록한다.

## 이식 제외

N/A — 규칙을 옮기지 않는다. 옮기는 것은 타입의 **집**뿐이다(값·열거 번호 무변 — `BuilderEnumPinTests`·`CoreSkillEnumPinTests` 가 증언).

## 파이프라인 커버리지

이 unit 이 `object-pipeline-map.md` 를 새로 쓴다(구현 6). 각 아키타입 표의 모든 정거장이 코어/새 층 심볼을 가리키고, 해당 없는 칸은 `N/A + 이유`.

## 완료 기준

- [ ] `python3 tools/battle-core-rebuild/check_ledgers.py --retire-set` exit 0 — 남는 파일의 옛 타입 호출 0 · 남는 자산의 퇴역 스크립트 GUID 참조 0. 퇴역 집합 목록(파일 수·줄 수)을 `ledgers/retire-set.md` 에 싣는다.
- [ ] `--owners` exit 0 — 「삭제」 아닌 행의 새 주인 전원 실재 · `rule-holders` 133 행 전부 실현 위치 또는 삭제 근거.
- [ ] 이사한 SO 의 자산이 그대로 열린다: `git diff --stat` 에 `.asset` 변경 0 · 이사한 `.cs.meta` 의 `guid:` 무변 · Assets lane(`BoardEffectAuthoringTests` 등) 초록.
- [ ] `grep -rln "guid: <볼륨 4개>" Assets/_Project/Art/Theme` 결과 불변(5 프리팹) · 새 씬 스테이지 포스트 효과 육안 무변.
- [ ] `object-pipeline-map.md` 에서 `BattleBridge`·`EntityManager`·`NativeQueue` 가 이력 줄 밖에 0.
- [ ] 헤드리스 3종 · EditMode 선행 2 외 빨강 0 · PlayMode 코어 초록 · 골든 11종 무변.
- [ ] `core-reviewer` APPROVE → main 머지(squash 금지, 푸시는 승인제) — **조각 E 경계 1**.

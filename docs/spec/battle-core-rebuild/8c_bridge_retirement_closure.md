# 8c — 브리지 폐쇄: 지울 수 있다는 증명 (조각 E · 3/3)

## 목적

**unit 9 가 「8c 가 퇴역으로 판정한 것을 지운다」 한 가지 일만 하게 만든다.** 지우는 순간 깨질 수 있는 것을 여기서 **기계로** 0 으로 만든다. 그런 것은 넷이다. ① 살아남는 코드가 옛 타입을 부르는 곳. ② 옛 폴더 안에 사는 저작 타입과 프리팹 컴포넌트(스크립트 GUID). ③ 옛 씬 폴더 안에 살거나 옛 씬 경로를 문자열로 든 자산·도구·테스트. ④ 장부가 「새 주인」이라 적었는데 실체가 없는 행. `BattleBridge*.cs` 를 지울 수 있는 조건 = 이 unit 의 완료 기준 전부.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 코드 도달성 검사 | `tools/battle-core-rebuild/headless/Retire.Check.csproj`(신설) — export 에서 퇴역 후보를 지우고 **컴파일한다** |
| 자산 도달성 검사 | `check_ledgers.py --retire-assets`(GUID 폐포) |
| 새 주인 실재 검사 | `check_ledgers.py --owners` + `ledgers/owner-aliases.md`(신설) |
| 퇴역 집합 | `ledgers/retire-set.md`(신설 — 검사 결과. unit 9 는 이 목록만 지운다) |
| 저작 타입·컴포넌트 이사 | `Scripts/Battle/**` → `Scripts/Data/Authoring/`(`.meta` 동반 이동 — GUID 보존) |
| 옛 씬 폴더 자산 | `Scenes/BattleScene/{Duel,Street,Street_Day,Subway}.asset` → `Art/Theme/` 옆(`.meta` 동반) + `Editor/MapStageDuelGenerator.cs:16~17` 경로 상수 |
| 도구 장부 | `ledgers/tools.md` — 누락 1행 |
| 파이프라인 맵 | `docs/reference/object-pipeline-map.md` 전면 재작성(README 「파이프라인 커버리지」 약속) |

## 구현

1. **코드 도달성은 C# 참조를 흉내 내지 않고 컴파일러에게 묻는다.** 기존 export 검증(인계 §5: `git archive <sha> … | tar -x`)을 그대로 쓴다. 그 사본에서 퇴역 후보를 지우고, `Wassup.Runtime` 전체 + 에디터 코드 + 남는 테스트 어셈블리를 Unity dll(`Library/ScriptAssemblies`·`UnityEngine`/`UnityEditor` Managed)에 대고 컴파일한다(`Retire.Check.csproj`). **오류 0 = 남는 코드가 후보를 안 부른다.** 이름이 같은 코어 사본(`GridMath`·`NavGrid`·`StatKind`…)을 혼동할 여지가 없다. 후보 초안 = `Scripts/Battle/**`·`Bridge/**`·`Editor/Battle/**`·옛 뷰·옛 UI·규칙 보유자 10 중 `TimeManager` 제외. 컴파일이 부른 곳을 알려 주면 그 파일은 **후보에서 빼거나**(남는다) **남는 쪽을 고친다**(이사·교체). 규모 추정: csproj ~70줄.
2. **자산 도달성은 GUID 폐포로 잰다.** 뿌리 = 빌드 설정의 씬(8b 뒤 `OutgameScene`·`BattleCoreScene`) + `Resources/**` + dev 씬(`MapTest`·`FluidScratch` — 남길지 이 unit 에서 판정). 이 뿌리에서 `guid:` 참조를 전이로 따라간 폐포 안에 퇴역 후보 스크립트의 GUID 가 있으면 실패다. ⚠ 「자산이 부르는 스크립트 전부」를 뿌리로 잡으면 옛 전용 프리팹도 뿌리가 돼 퇴역 집합이 비지 않는다. 그래서 뿌리는 씬·빌드·Resources 뿐이다. **에디터 코드의 씬 경로 문자열**(`"Scenes/BattleScene"`)도 같이 grep 한다. 규모 추정: `check_ledgers.py` 156줄 → ~300줄.
3. **2026-09-25 실측으로 이미 드러난 「남는 쪽이 옛 것을 부르는」 곳** — 구현 1·2 가 다 잡아야 하는 최소 목록:

   | 남는 쪽 | 옛 것 | 처분 |
   |---|---|---|
   | `BattleDriver.cs:64` · `BoardEffectDefinitionBuilder.cs:26·46·153·178` · `MatchViewAssets.cs:26~60` · `CoreHazardViewPool.cs:39·189` · `Data/ProjectileData.cs:166` · `Data/Abilities/HazardCastAbility.cs:17` · `CoreCardSelfCheckMenu.cs` | `BlockingHazardSO`(`Battle/Effects/` — SO, 자산 3이 GUID 로 부른다) | 이사 |
   | 길막 SO 3(`Blocker_BombBarrel`·`Hazard_Rock_1x1`·`_3x3`)의 `visualPrefab` → `Prefabs/Hazards/BlockingHazard_BombBarrel.prefab`·`_Placeholder.prefab` → 새 씬 `CoreHazardViewPool.cs:147` 이 `Instantiate` | 두 프리팹에 붙은 `BlockingHazardPresenter`(`Battle/Effects/`, **`using Unity.Entities`**) | ECS 의존을 걷고 이사 — 그대로 지우면 **Missing Script** |
   | `BoardEffectDefinitionBuilder.cs:72·100~121` · `Data/HazardSO.cs:10·20` | `HazardEffect`·`HazardShape`·`CcKind`(⊂ `CcEffect.cs`)·`DotElement`(⊂ `DotEffect.cs`) | 열거형만 떼어 이사 |
   | `Data/Abilities/HazardCastAbility.cs:15` | `HazardCastKind` | 이사 |
   | `Data/StackModifierSO.cs`·`EffectTileData.cs`·`AttackOutput.cs`·`DefenderUnitData.cs:5`·`CombatDefinitionBuilder.cs`·`DreamcatcherCardText.cs` | `StatKind`·`StackKind`·`CombineOp`(`ModifierTypes.cs`) | 이사 |
   | `DreamcatcherCardText.cs`(로비 공유 문안원 — 7c 「공유」) | `DcTrigger`·`NextAttackDoubleFire`·`DreamCocoon` | 이사(상수만) |
   | `Data/AttackUnitData.cs:86` | `EnemyTargetDefaults.DefaultEnemyMask` | 이사 |
   | `Data/WavePatternGenerator.cs:639` | `Wassup.Battle.Movement.WaypointRouting` | unit 9 가 생성기를 지우면 소멸(8a 구현 5) — 안 지우면 이사 |
   | `BattleCoreUnity/Cards/CoreDeckComposition.cs:29` | `SkillLoadoutController.FilterHiddenSkills`(규칙 보유자) | 순수 static 하나를 새 층으로 이사 |
   | `CoreMatchOutcomePresenter.cs:173` · `UI/ResultScreen.cs` | `MatchTally`(규칙 보유자) | 8a 가 어댑터를 걷은 뒤 `ResultScreen` 의 `MatchTally` 입력만 남는다 → 옛 씬과 함께 unit 9 |
   | `Presentation/ReachDebugGizmos.cs` · `IngameCharacterTest.cs` · `MenuPopup` dev 토글 | 브리지 전용 자료 · 옛 씬 실험대 | 구현 6 |
   | `Editor/MapStageDuelGenerator.cs:16~17` | `Scenes/BattleScene/Duel.asset`·`Street.asset` 경로 상수 | 볼륨 이사와 같은 커밋 |
   | `Editor/SpineUpgradeSmoke.cs:13` | `BattleScene.unity` 를 연다 | 새 씬으로 |
   | Assets lane: `MarkerPropStyleAssetTests.cs:15`·`DcAttachRequirementWiringTests.cs:23`(옛 씬을 텍스트로 읽는다) · `WaveSpawnLeadInTests.cs:36`(`AddComponent<BattleBridge>`) · `DirectionalVolleyIntegrationTests.cs:10~13`·`AuthoredTargetMaskTests.cs:8`(옛 ECS 시스템) · `DetectionRangeAuthoringTests.cs:4`·`DragonBreathAuthoringTests.cs:76`(옛 타입) · `AttackReachParityTests`(옛 ↔ 새 비교) | 옛 씬·옛 타입 | 새 씬·이사한 타입으로 고치거나, 짝 있는 은퇴(unit 9 구현 2) |
   | `DreamcatcherCardDragSlot.cs:672` · `TilemapMapView.cs:1251` · `ProjectileViewPool.cs:6` | 옛 타입 | 퇴역 후보인지 구현 1 이 판정 |

4. **이사 규칙.** SO·MonoBehaviour 는 **파일과 `.meta` 를 같이** 옮긴다(자산의 `m_Script` 가 GUID 로 부른다). **네임스페이스는 여기서 바꾸지 않는다** — 옛 전투가 아직 컴파일돼야 한다. 이름 정리는 unit 9 가 한 번에 한다. 옛 폴더를 건드리므로 커밋 첫 줄에 `[old-battle]` 을 달고(동결 훅), 규칙 변경이 없다고 적는다.
5. **새 주인 칸의 문법을 먼저 정한다.** 지금 `bridge-methods` 「새 주인」 칸은 92종이고 상당수가 자유문이다(「뷰 풀」「MapRuntime (코어)」「뷰 풀 / 담당자 구독 (이벤트로 접힘)」「디버그/로그 (도구 처분표)」). 규칙: 칸의 **첫 백틱 토큰** = 코드 심볼(`파일명.cs` 또는 `타입.멤버`). 자유 범주어는 `owner-aliases.md` 의 별칭 → 실제 심볼 목록으로만 허용한다(예: 「뷰 풀」 → `Core*ViewPool`·`Core*Presenter`·`Core*Spawner` 중 그 행이 가리키는 것). `--owners` = 모든 「삭제」 아닌 행의 첫 토큰이 심볼로 해석된다. `rule-holders` 133 행에는 「실현 위치」 열을 더한다 — 「뷰」 31 행이 특히 그렇다(G17 처럼 「뷰」로 배정됐지만 앱 전역 훅이던 것). 8a 가 드러낸 실체 없는 배정(bridge-fields 1·31·55)이 이 검사가 잡아야 할 모양이다. `rule-holders` G11 비고는 critic 반영 커밋에서 이미 정정했다(first-run-tutorial 은 76038c26 뒤에 지어졌다).
6. **도구·실험대 처분.** `ReachDebugGizmos` 는 `tools.md` 11행에 없던 디버그 도구다(브리지 전용 `BattleBridge.DebugReachSphere` 를 그린다). 처분 = **은퇴(에이전트 판정)**. 새 코어의 도달 자는 `CoreMapOverlay` 가 그리고, 수치 정합은 `AttackReachParityTests`(20,000건)가 증언했다. 옛 spec 결정의 인용이 아니라 대체물이 근거라서 표에 「에이전트 판정」이라 적는다. `IngameCharacterTest` 와 `MenuPopup` dev 토글 「캐릭터/포스트」(`:172~178`)는 옛 spec 에서 은퇴 결정을 찾는다. 없으면 사용자에게 묻고 답이 오기 전에는 새 씬으로 옮기지 않은 채 퇴역 후보에서 뺀다. `SimOrderDumpMenu`(4행)는 unit 9 그대로.
7. **파이프라인 맵 전면 재작성.** 아키타입 표마다 정거장을 이렇게 잇는다: 저작 SO → `MatchDefinitionBuilder` 정의표 행 → 코어 스폰/사건(`CoreEventKind` 번호) → 뷰 풀(`Core*`) → `ViewOrder` → 소멸 사건 회수. 옛 정거장은 한 줄 이력으로만 남긴다. 5a·6b·6c 의 부분 표와 8a 의 보너스 포탈을 흡수한다.
8. **`Faction` 네임스페이스(에이전트 결정 · unit 9 에서 실행).** `Scripts/Skills/Faction.cs:11`·`FactionRelation.cs` 가 `Wassup.Battle.Units` 이고, 리뷰어가 이것을 두 번 「죽은 import」로 오판했다(인계 §3-9·함정 16). 실측: 참조 파일 313 · 옛 폴더+옛 PlayMode 제외 157 · 옛 EditMode 까지 제외 87 — **unit 9 삭제 뒤 재측정한다.** 직렬화 영향은 0 이다(자산에 `Wassup.Battle` 네임스페이스 문자열 0 · `m_EditorClassIdentifier` 에 Faction 없음). 이득은 오판 방지와 「없는 전투의 이름」 제거다. 저비용 대안(`core-reviewer.md` 한 줄 + 기존 헤더)도 있지만, 옛 폴더가 사라진 뒤 `Wassup.Battle` 이 이 두 파일에만 남으면 다음 사람이 옛 전투의 잔재로 읽는다. 그래서 **옮긴다** — 규칙 diff 0 의 단독 커밋으로.

## 이식 제외

N/A — 규칙을 옮기지 않는다. 옮기는 것은 타입의 **집**뿐이다(값·열거 번호 무변 — `BuilderEnumPinTests`·`CoreSkillEnumPinTests` 가 증언).

## 파이프라인 커버리지

이 unit 이 `object-pipeline-map.md` 를 새로 쓴다(구현 7). 모든 정거장이 코어/새 층 심볼을 가리키고, 해당 없는 칸은 `N/A + 이유`.

## 완료 기준

- [ ] `Retire.Check.csproj` — 퇴역 후보를 지운 export 사본에서 Runtime + Editor + 남는 테스트 **컴파일 오류 0**. 후보 목록(파일 수·줄 수)을 `ledgers/retire-set.md` 에.
- [ ] `--retire-assets` exit 0 — 뿌리 폐포 안의 퇴역 스크립트 GUID 0 · 남는 에디터 코드의 옛 씬 경로 문자열 0.
- [ ] `--owners` exit 0 — 「삭제」 아닌 행 전원 심볼 해석 · `rule-holders` 133 행 실현 위치 또는 삭제 근거.
- [ ] 이사한 SO·컴포넌트의 자산이 그대로 열린다: `.asset`·`.prefab` diff 0 · 이사한 `.cs.meta` 의 `guid:` 무변 · 길막 프리팹 2 Missing Script 0 · Assets lane 초록.
- [ ] 볼륨 4개를 부르는 프리팹 5 불변 · `MapStageDuelGenerator` 실행 가능 · 새 씬 스테이지 포스트 효과 육안 무변.
- [ ] `object-pipeline-map.md` 에서 `BattleBridge`·`EntityManager`·`NativeQueue` 가 이력 줄 밖에 0.
- [ ] 헤드리스 3종 · EditMode 선행 2 외 빨강 0 · PlayMode 코어 초록 · 골든 11종 무변.
- [ ] `core-reviewer` APPROVE · **사용자 플레이 4차 통과(8b)** → main 머지(squash 금지, 푸시 승인제) — **조각 E 경계 1**. 이 머지로 동료·GitLab 이 새 전투를 받는다(README 결정 ③).

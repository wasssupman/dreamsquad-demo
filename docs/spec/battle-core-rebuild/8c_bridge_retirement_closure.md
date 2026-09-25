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

## 고친 것 (2026-09-25 구현)

| 무엇 | 어떻게 | 커밋 |
|---|---|---|
| 새 층·저작 SO 가 부르는데 옛 폴더에 살던 타입 | 파일째 `.meta` 동반 이동(`BlockingHazardSO` · `BlockingHazardPresenter` · `HazardShape` · `HazardEffect` · `HazardCastKind` · `DcTrigger`) + 열거형만 떼어 새 파일(`StatKind`·`StackKind`·`CombineOp` · `CcKind` · `DotElement` · `EnemyTargetDefaults`) → `Scripts/Data/Authoring/`. 네임스페이스·값·번호 무변 | `66c77be6c` `[old-battle]` |
| 길막 프리팹 2 의 컴포넌트가 ECS 를 참조 | `BlockingHazardPresenter` 의 `Entity` 보관을 걷었다(읽는 곳 0) · 옛 브리지 호출만 `Bind()` 로 | `66c77be6c` `[old-battle]` |
| 살아남는 파일 안에 섞인 옛 씬 전용 입력 | `CameraDirector`·`SoundManager` 의 `GameManager` 구독, `ResultScreen.Show(MatchTally)`, `WavePatternStripView.RebuildFromDeck` 를 `*.OldBattle.cs` 부분 파일로 분리 — unit 9 는 **파일만 지운다**(`partial void` 는 구현이 사라지면 호출째 빠진다) | `1f4f934f2` |
| 새 층이 규칙 보유자의 순수 함수를 부름 | `SkillLoadoutController.FilterHiddenSkills` 본문 → `CoreDeckComposition.FilterHiddenSkills`(옛 쪽은 위임) | `1f4f934f2` |
| 옛 씬 폴더 안 볼륨 4 · 옛 씬 경로를 든 도구·테스트 | 볼륨 → `Art/Theme/<맵>/`(GUID 보존) · `MapStageDuelGenerator` 경로 상수 · `SpineUpgradeSmoke` → `BattleCoreScene` · `MarkerPropStyleAssetTests`·`DcAttachRequirementWiringTests` → `BattleCoreScene` 의 후계 뷰. 빈 옛 씬 폴더는 지웠다 | `236497b39` |
| 코드·자산 도달성 · 새 주인 실재를 재는 기계 | `Retire.Check.csproj` · `check_ledgers.py --retire-prune`/`--retire-assets`/`--owners` | `e7a3cdf34` |
| 퇴역 집합 · 장부 | `ledgers/retire-set.md` · `owner-aliases.md` 신설 · bridge-methods 168행 심볼 보강 · 실체 없던 14행 「삭제」 정정(기존 결정 인용) · bridge-fields 15행 대조 + 28 「삭제」 · rule-holders 「실현 위치」 133행 · tools 12행 | `54d01eb2d` |
| 파이프라인 맵 | 아키타입 16표 전면 재작성 · 백틱 심볼 254개 해석 확인 | `7b3931631` |

## 이식 제외

N/A — 규칙을 옮기지 않았다. 옮긴 것은 타입의 **집**뿐이다(`BuilderEnumPinTests`·`CoreSkillEnumPinTests` 가 속한 Assets·Core lane 초록 — 아래 수치).

## 판정 (에이전트)

| 무엇 | 판정 | 근거 |
|---|---|---|
| 옛 웨이브 생성기(`WavePatternGenerator`)·`BonusWaveSchedule`·`BattleConfig`(+자산) | **퇴역**(unit 9 「잔여 이중화」의 컴파일 확인) | 스트립 덱 경로를 떼자 남는 소비처 0 — `Retire.Check` 오류 0. 8c 표의 `:639` 행은 이사하지 않고 생성기와 함께 사라진다 |
| `ReachDebugGizmos` | 은퇴 | 대체물 `CoreMapOverlay.PaintRange` · `AttackReachParityTests` — 옛 spec 결정의 인용이 아니다(`tools.md` 12행) |
| dev 씬 `MapTest`·`FluidScratch` | 남긴다 | 퇴역 스크립트를 하나도 안 부른다 · 자산 도달성 뿌리에 넣었다 |
| 장부의 실체 없는 배정 14행(로그 · 재배치 · 드래프트 · 유출 칸 · 자석 스냅 · 휴면 코드) | 「삭제」 정정 | 행마다 기존 결정을 인용했다(rules X28 · X21 · tools 11 · 계약 9 · 사용자 결정 2026-09-23 · 옛 호출처 0) |

## 사용자·리드 결정 필요

1. **보류 2 — `IngameCharacterTest` · `MenuPopup` dev 토글 「캐릭터/포스트」.** 옛 spec 에서 은퇴 결정을 못 찾았다(찾은 것은 「오브젝트를 껐다」 `camera-direction/15` 와 「실험대가 이미 증명했다」 `distance-based-range/20:47` 뿐). `retire-set.md` 「보류」 — (a) 옛 씬과 함께 은퇴 · (b) 새 씬으로 옮긴다. 둘 다 잎이라 어느 답이든 컴파일 증명은 그대로다.
2. **새 씬에 없는 옛 기능 — 「새 주인」은 배정됐는데 실체가 없다**(`--owners` 가 「미실현」으로 센다 · 새 층 호출처 0 을 grep 으로 확인). 전부 플레이어가 보거나 로그로 읽던 것이다:
   - 효과 타일 칸 표시(T15) — 규칙은 돌지만 판 위에 어느 칸인지 안 그린다
   - 궁극기 착지 예고 칸(T16·T17 · `ShowLandingTelegraph`) — 「예고 중 유닛을 빼는 것」이 그 스킬의 놀이다
   - 마음 붕괴 연출(`PlayCoreBurst` · `DrainGoalCollapsedEvents` — VFX + 슬로모 `coreBurstTimeScale`)
   - 배치 드래그 중 적 흐리게(`SetEnemiesDimmed`) · 적 체력 틴트(`EvaluateEnemyHealthTint`)
   - 소환사 유지 애니메이션(`SyncSummonerAnimationState`) · 방어유닛 AI 전이 트레이스(`TraceDefenderAiTransition`)
   - 사거리 칸 채움(T3·T13) — 새 오버레이는 링과 표식만 그린다. 옛 채움은 링이 있으면 투명이라 보이는 차이는 작을 수 있다
   → **리드 판단(2026-09-25): 8c 안에서 이식하지 않고 unit 9 앞의 필수 unit `8a2_view_transfer_remainder.md` 로 뗐다**(위 7행). ⚠ **사거리 칸 채움(T3·T13)은 8a2 목록에 없다** — 8a2 가 끝나도 이 2행이 남으면 `--owners` 는 exit 1 이다. 8a2 에 넣을지, 은퇴(사용자 결정)로 닫을지 정해야 한다.

## 검증 수치 (HEAD `7b3931631` · 2026-09-25)

| lane | 결과 | 기준선(8b) |
|---|---|---|
| 헤드리스 build · test · Check | 0 · **685** · 0 | 0 · 685 · 0 |
| `Retire.Check`(퇴역 309 줄 + 보류 2 줄 → 618 항목을 지운 export) | **오류 0** · 음성 대조(남는 `BlockingHazardSO.cs` 를 지우면 오류 26) | — |
| `check_ledgers.py`(기본) · `--retire-assets` | exit 0 · exit 0(뿌리 18 · 폐포 1,925 · 퇴역 스크립트 576 중 폐포 안 0 · 옛 경로 문자열 0) | exit 0 |
| `--owners` | **exit 1** — bridge-methods 미실현 7 · rule-holders 미실현 5(위 결정 2). 나머지 실패 0(심볼 278/85/114 · 삭제 82/6/14) | — |
| Unity EditMode Core + Assets | **1005/1007**(선행 2 `boomerang`·`bomb_man` 만) · 골든 무변 | 1005/1007 |
| `DreamSquadMobileBuildCliTests` | 63/63 | 63/63 |
| Unity PlayMode Core | **85/85** | 85/85 |
| 옛 PlayMode 부분집합 6 | 35/38 → `BonusWavePullTest` 3건 단독 재실행 13/13 → **38/38**(함정 20 — 코어 lane 직후 러너 잔류) | 38/38 |
| 이사 GUID | `.cs.meta`·볼륨 `.asset.meta` rename 100% · `.prefab`/`.asset`/`.unity` diff 0(`9d2d8083a..HEAD`) · 길막 프리팹 2 Missing Script 0 · 볼륨 부르는 프리팹 5 가 새 자리를 가리킨다(`VolumeProfile` 컴포넌트 7) · `MapStageDuelGenerator` 경로 상수 2 로드 성공 | — |

⚠ 스테이지 포스트 효과 **육안 대조는 하지 않았다** — 자산이 바이트 동일(rename 100%)하고 프리팹 참조가 같은 GUID 라 그림이 바뀔 경로가 없다. 플레이 4차에서 같이 본다.

## 완료 기준

- [x] `Retire.Check.csproj` — 퇴역 후보를 지운 export 사본에서 Runtime + Editor + 남는 테스트 **컴파일 오류 0**. 후보 목록(파일 수·줄 수)을 `ledgers/retire-set.md` 에. — 퇴역 590 파일(C# 574 · 113,228줄) + 보류 2(456줄) · 오류 0 (2026-09-25 `7b3931631`)
- [x] `--retire-assets` exit 0 — 뿌리 폐포 안의 퇴역 스크립트 GUID 0 · 남는 에디터 코드의 옛 씬 경로 문자열 0. — 남는 코드 전부(런타임·에디터·테스트)로 넓혀 0 (2026-09-25)
- [ ] `--owners` exit 0 — 「삭제」 아닌 행 전원 심볼 해석 · `rule-holders` 133 행 실현 위치 또는 삭제 근거. — **보류**: 133행 열은 채웠고 해석 실패 0 이지만 「미실현」 12행(bridge-methods 7 · rule-holders 5)이 남아 exit 1 — 10행은 8a2 가 닫는다 · T3·T13 은 처분 미정(「사용자·리드 결정 필요」 2)
- [x] 이사한 SO·컴포넌트의 자산이 그대로 열린다: `.asset`·`.prefab` diff 0 · 이사한 `.cs.meta` 의 `guid:` 무변 · 길막 프리팹 2 Missing Script 0 · Assets lane 초록. — 위 수치 표 (2026-09-25)
- [ ] 볼륨 4개를 부르는 프리팹 5 불변 · `MapStageDuelGenerator` 실행 가능 · 새 씬 스테이지 포스트 효과 육안 무변. — 프리팹 5 불변 · 경로 상수 로드 성공 ○ · **육안은 플레이 4차로 보류**(자산 바이트 동일)
- [x] `object-pipeline-map.md` 에서 `BattleBridge`·`EntityManager`·`NativeQueue` 가 이력 줄 밖에 0. — 0 · 본문 심볼 254 해석 (2026-09-25 `7b3931631`)
- [x] 헤드리스 3종 · EditMode 선행 2 외 빨강 0 · PlayMode 코어 초록 · 골든 11종 무변. — 0·685·0 · 1005/1007 · 85/85 · 옛 부분집합 38/38 (2026-09-25 `7b3931631`)
- [x] `core-reviewer` **APPROVE**(2026-09-25 — CRITICAL·HIGH 0 · MEDIUM 1 = T3·T13 처분(→ 8a2 행 8) · LOW 1 = `BlockingHazardPresenter.cs:270~278` `Shader.Find`+`new Material` 선행 위반, 후속 후보) · [ ] **사용자 플레이 4차 통과(8b)** → main 머지(squash 금지, 푸시 승인제) — **조각 E 경계 1**. 이 머지로 동료·GitLab 이 새 전투를 받는다(README 결정 ③).

리드 재검증 2026-09-25 — HEAD `e08b2ba4b` 클린 export: build 0 · test 685/685 · Check 0 · `check_ledgers.py` exit 0 · `--retire-assets`(워크트리) exit 0(뿌리 18 · 폐포 1925 · 폐포 안 퇴역 0 · 총계 590 일치) · `--owners` 미실현 12(전부 8a2 행)만 · `--retire-prune` 620 항목 → `Retire.Check` 오류 0(6.7초 실컴파일) · 음성 대조(`BlockingHazardSO.cs` 삭제) 오류 13. Unity EditMode 1005/1007(선행 2) · PlayMode 코어 85/85 · 옛 씬 부분집합 35/38 → `BonusWavePullTest` 단독 13/13(코어 lane 직후 재현 2회 — 함정 20, 옛 lane 은 unit 9 에서 사라진다) · CLI 63/63.

**리드 결정 2026-09-25(보류 2)**: `IngameCharacterTest` · `MenuPopup` dev 토글 「캐릭터/포스트」 = **(a) 옛 씬과 함께 은퇴** — 둘 다 플레이어 규칙이 아니라 개발 실험대이고, 실험의 목적(하이브리드 그림자 증명)은 달성됐으며 블롭 값은 8a 에서 SO 로 승격됐다. `retire-set.md` 의 `hold` 블록은 unit 9 가 `retire` 로 옮기며 총계 줄을 같이 갱신한다(사용자가 그 전에 뒤집으면 (b)). T3·T13 = 8a2 행 8(이식, 사용자 은퇴 선택 가능).

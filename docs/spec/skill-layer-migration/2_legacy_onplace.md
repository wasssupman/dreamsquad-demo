# 2 — 레거시 `OnPlaceEffectType` 9종

## 목적

**두 번째 어휘를 죽인다.** `BattleBridge.cs:5393~5590` 의 200줄 if/else 체인과
`OnPlaceEffectType` enum 11종, `DefenderUnitData` 의 flat 필드 7개가 여기서 사라진다.

`on-place-skill-rework` 계약 2 가 예약해 둔 「레거시 전량 이관」이 바로 이 작업이다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:5393~5590` — 체인 철거
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — enum + flat 필드
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — 문안 case 10개
- 에셋 12개 재저작

## 구현

1. ~~**그물이 절대 선행이다.** PlayMode 커버리지가 **9종 중 3종**뿐이다.~~
   **stale — 2026-08-26 실측으로 정정.** 그 사이에 그물이 붙었다:

   | 값 | 종류 | PlayMode 그물 | 라이브 저작 |
   |---|---|---|---|
   | 1 | `SlowPulse` | **없음** | **0개** |
   | 2 | `BoostNearbyDefenders` | `OnPlaceBoostNearbyTest` | 1 |
   | 3 | `BindNearby` | `OnPlaceBindNearbyTest` | 1 |
   | 4 | `MeleeBurst` | `OnPlaceRuleTriggerTest`(간접) | 1 |
   | 5 | `ForwardProjectile` | `OnPlaceForwardProjectileTest` | **4** |
   | 6 | `GainCost` | `OnPlaceGainCostTest` | 1 |
   | 7 | `ReduceSkillCooldown` | `OnPlaceReduceSkillCooldownTest` | 1 |
   | 8 | `ApplyStackNearby` | `OnPlaceApplyStackNearbyTest` | 1 |
   | 9 | `StunNearby` | `OnPlaceStunNearbyTest` | 1 |
   | 10 | `DotNearby` | `OnPlaceDotNearbyTest` | 1 |

   **선행 작업이 「그물 6개 신설」에서 「`MeleeBurst` 의 직접 그물 1개」로 줄었다.**
   그리고 **`SlowPulse` 는 이전 대상이 아니라 삭제 대상**이다 — 그물도 없고 라이브
   저작자도 0이라 옮길 것 자체가 없다(옮기면 아무도 안 쓰는 concrete 가 생긴다, 제약 8).
2. **신규로 필요한 payload kind 는 `AreaCc{DcCcKind}` 하나 정도**다.
   `MeleeBurst`(4)는 기존 `SelfTileAoe` 재사용, `StunNearby`(9)는 `ApplyCcToTarget`/`AreaSleep` 계열.
   기존 어휘를 먼저 쓴다(append 는 표현 불가일 때만).
3. **Mono 도메인 arm 2개를 판정대로 처리한다** — `GainCost`(`:5564`, `CostRuntime`) ·
   `ReduceSkillCooldown`(`:5571`, `skillRuntime`)은 **ECS 를 전혀 안 만진다.**
   토대 unit 0 이 「Mono 계열 intent」 또는 「예외」로 판정한 것을 따른다.
4. **`onPlacePush*` 3필드 + `ApplyOnPlacePush` 를 같이 뗀다** — **에셋 소비자 0**이 확인됐다
   (`Assets/_Project/Data` 전수 grep, nonzero 0건). 샷건맨이 마지막 소비자였고 규칙 경로로
   갈아탔다. 무비용 철거다.
5. **시트는 안전하다** — `Data/StatImport/UnitStatImportDto.cs` 에 onPlace 필드가 **0건**이라
   flat 필드 7개 삭제가 시트 왕복을 깨지 않는다.
   ⚠ 단 에셋 12개를 재저작하는 동안 **로그인 시트 임포트가 끼어들어 무관한 유닛 스탯이
   딸려 커밋될 수 있다.** 커밋 전 `git diff --stat` 로 확인한다.

## 완료 기준

- [ ] `MeleeBurst` 직접 그물 1개 신설 (나머지 8종은 이미 있다 — 위 표)
- [ ] `SlowPulse` 는 **삭제**한다(라이브 저작 0 · concrete 신설 금지)
- [ ] `OnPlaceEffectType` enum 과 `BattleBridge.cs:5393~5590` 체인이 **삭제**됐다
- [ ] `DefenderUnitData` flat 필드 7개 + `onPlacePush*` 3필드 + `ApplyOnPlacePush` 삭제
- [ ] `UnitKitSummary` 문안이 저작 SO 를 읽는다
- [ ] 에셋 12개 재저작 커밋에 **무관한 유닛 스탯 변경이 섞이지 않았다**
- [ ] Assets lane + PlayMode 배치 스킬 테스트 초록 + Play 육안(9종)

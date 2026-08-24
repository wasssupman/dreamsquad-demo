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

1. **그물이 절대 선행이다.** PlayMode 커버리지가 **9종 중 3종**뿐이다
   (`DotNearby`·`ApplyStackNearby`·`ForwardProjectile`). 나머지 6종은 무보호다.
   `on-place-skill-rework` 후속 후보가 *"이관 전에 테스트를 먼저 깔지 않으면 회귀를 못 잡는다"*
   고 명시해 뒀다.
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

- [ ] 9종 전부에 특성화 테스트가 선행됐고 초록이다 (3종 → 9종)
- [ ] `OnPlaceEffectType` enum 과 `BattleBridge.cs:5393~5590` 체인이 **삭제**됐다
- [ ] `DefenderUnitData` flat 필드 7개 + `onPlacePush*` 3필드 + `ApplyOnPlacePush` 삭제
- [ ] `UnitKitSummary` 문안이 저작 SO 를 읽는다
- [ ] 에셋 12개 재저작 커밋에 **무관한 유닛 스탯 변경이 섞이지 않았다**
- [ ] Assets lane + PlayMode 배치 스킬 테스트 초록 + Play 육안(9종)

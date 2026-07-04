# 3 — 전투 반입 (매치 효과 레지스트리 일반화)

## 목적

게임 시작 시 장착 스톤 효과를 아군 디펜더 전체(현재 + 이후 배치)에 매치 상시 버프로 적용한다. **ECS 변경 0** — 기존 StatModifier 채널 재사용.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` (`CardTargetAxis.All` 추가)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs` (`StartSquadMatch` + `StartTestModeMatch` 양쪽)
- BattleScene — GameManager 에 `DreamstoneCatalog` 참조 wiring
- new `Assets/_Project/Tests/PlayMode/DreamstoneCarryInSmokeTest.cs`

## 구현

- `CardTargetAxis` 에 `All` 추가 — **enum 끝에 append** (기존 에셋 직렬화 값 0~2 보존).
- **`MatchesDcAxis` 에 `case All: return true` 추가 — 독립 구현 단계로 취급**. 빠뜨리면 default → false 로 **조용히 no-op**(컴파일/런타임 에러 없이 스톤 무효). smoke 테스트가 이 분기를 실제로 통과해야 한다.
- **등록은 set-then-apply 패턴** (설계 크리틱 CRITICAL 반영 — `BeginPlacement()` 가 BattleBridge.cs:806 부근에서 `_activeDcEffects.Clear()` + `_dcStackCounter = 100` 리셋을 수행하므로, 배치 진입 전에 레지스트리에 직접 등록하면 전부 지워진다):
  - `public void SetDreamstones(IReadOnlyList<DreamstoneData> stones)` — pending 목록 저장만 한다 (`SetDefenderPool` 미러).
  - `BeginPlacement()` 가 클리어 **직후** pending 스톤을 `MapDcEffect` 변환 + `axis = All` + `stackId = _dcStackCounter++` 로 레지스트리에 적용한다. 매치 재시작 시 클리어→재적용이 한 지점에서 일어나 중복 누적(leak)이 구조적으로 불가능.
  - 이후 배치는 기존 `ApplyActiveDcEffectsTo` 훅이 자동 커버. 적용 시점에 배치된 디펜더는 없으므로 즉시 적용 루프는 사실상 no-op 이지만 `ApplyDreamcatcherCard` 와 동일 루프를 공유해도 무해.
- GameManager: `StartSquadMatch` 와 **`StartTestModeMatch`(별도 미러 메서드 — 누락 주의)** 양쪽에서 `SelectedSquad().stoneIds` → `DreamstoneCatalog.ById` 해석(null/빈칸 skip) → `bridge.SetDreamstones(...)`. 드래프트 폴백 경로는 호출 없음.
- 같은 스탯 스톤 4개도 stackId 가 각각 달라 additive 슬롯 4개로 공존(+30%). 버프(≥1)=additive, 감소형(<1)=multiplicative — 기존 `modifier-additive-authoring` 정책·one-directional 채널 불변식 그대로 (각 스톤은 stackId 당 단일 고정값이라 어떤 (stat,op,stackId) 채널도 1.0 경계를 straddle 하지 않음 — EffectiveHealth 스톤은 mult<1 로 항상 multiplicative).
- **알려진 기존 버그(범위 밖)**: headless 드림캐쳐 auto-pick 이 `SetPhase(Placement)` 시점(= `BeginPlacement` 클리어 직전, PlacementPhaseView.cs:56-58)에 등록돼 지워지는 문제. 스톤의 set-then-apply 와 독립이므로 이 spec 에서 고치지 않는다 → README 후속 후보. smoke 테스트는 카드 선택을 배치 시작 이후로 두어 이 경로와 얽히지 않게 설계.

## 완료 기준

- PlayMode smoke: 유니크 ATK 스톤 4개 장착 스쿼드로 시작 → 디펜더 배치 → 해당 엔티티 `ModifierStats.damageMul ≈ 1.30`
- 복합 스택: 스톤 적용 상태에서 같은 스탯 드림캐쳐 카드 1장 pick → 두 버프 공존 (`damageMul > 1.30`)
- 재시작 회귀: 매치 재시작(`BeginPlacement` 재호출) 후에도 1.30 유지 — 누적도 소실도 없음
- 기존 드림캐쳐 / 스쿼드 carry-in smoke 회귀 통과, 콘솔 새 에러 0

> 완료 확인(부분) 2026-07-04 — 리그 게이트 PASS: compile clean + EditMode 12/12 회귀 + PlayMode 2/2 (단독 1.30 / 복합 1.40 / 재시작 1.30 green). 투트랙 리뷰 반영(M2 pending 클리어, L1 주석). BattleScene stoneCatalog wiring + GameManager 경유 실게임 Play 검증 pending.

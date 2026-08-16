# 1 — 밸런스 리터럴 de-pin

## 목적

시트가 소유한 필드를 실에셋에서 읽어 리터럴로 못박은 단언을 구조·부호·상대 단언으로
전환한다. 판별 기준 = **DTO 가 덮는 필드만** (`UnitStatImportDto`: health · attackRange ·
atk→outputs[].magnitude · attackDamage · attackCooldown · hitDelaySec · cost · moveSpeed 등 /
`DcSheetImportDto`: effects.percent · mechanics.magnitude·tileRange·duration·triggerPeriod ·
skills.range·magnitude·durationSec·cooldownSec·cost). 시트가 안 덮는 저작 계약 리터럴
(패턴 각도·발수·프리팹 배선·아트 임포트·0-구조 단언)은 유지한다.

## 변경 대상 (전부 EditModeAssets/)

| 파일 | de-pin | 유지 |
|---|---|---|
| DirectionalVolleyIntegrationTests | attackRange 2f · magnitude 12f → >0 | projectile.speed(SO 소유) · hitDelaySec==0(메커니즘 구조) · 패턴 수치 전부 |
| DreamcatcherCatalogSyncTests | 4개 카드의 percent/magnitude/duration/tileRange/trigger.period 리터럴 → 부호·상대 단언 (성배의 −40% 는 **부호가 정체성** → Less(0) 로) | 등록 동기 가드 · type/kind/길이/아트 계약 · leakAllowanceCost(DTO 밖) |
| CatalogPlacementLayerTests | antiAir cooldown 0.2f·magnitude 7f · skimmer cooldown 0.2f → >0 | 층 마스크 · targetCount · faction |
| DreamcatcherCardAssetTextTests | structuredCount==44 → **전 카드 단언**(비정형 카드 목록이 빈다) — "모든 카드가 데이터 정형" 의도를 개수 스냅샷이 아니라 직접 검사로 | description 중복 금지 |

**비대상 정정** (오딧 지목이었으나 조사 결과 제외):
- `DreamstoneCatalogTests` — 64개·7.5% 등은 등급 공식(cap/4 · 0.8 · 0.6)에서 유도되는
  **설계 계약**(dreamstone-loadout unit 5)이고 시트 비관리. 유지.
- `DeckInfoDisplayTests:125` — 합성 픽스처 14개를 만들어 14개 렌더를 확인. 콘텐츠 무관.
- `SlimeSplitAuthoringTests` — 0-단언(구조). 유지.

## 구현

리터럴 자리에 의도를 남기는 단언으로 교체: 배율은 >1, 퍼센트 버프는 >0, 말루스는 <0,
지속·주기·범위는 >0. 주석은 "시트가 정본, 값은 자유 튜닝" 을 명시 (WaveKillBudgetPinTests
헤더 문체).

## 완료 기준

- [x] 대상 4파일에 DTO 필드 리터럴 단언 0건
- [x] Assets lane 155개 실행 — 실패는 기지(MultiGoal 4건)뿐, de-pin 테스트 전부 초록
      (전 카드 정형 단언도 초록 — 기존 44 pin 이 실제로 전체였음이 확인됨)
- [x] 코어 lane 무변화 (건드리지 않음)

2026-08-16 구현 + 기계 검증 완료.

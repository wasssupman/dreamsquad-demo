# 1. 클라이맥스 변주 격상 — 상시 변주 + 신규 레인 개방

## 목적

break 이후 «타입 혼합 + 조직된 그룹의 다중 레인 협공 가중»을 기존 `variantSlots` 재활용으로
구현한다. ⑴ 변주가 블록 가운데(1/3)가 아니라 **매 웨이브**(3/3) 적용, ⑵ 본 편성에 없는
laneGroup 의 변주 슬롯이 **미사용 레인을 연다**(현행: 본 레인으로 접힘). 둘 다 unit 0 의
break 필드가 게이트 — 라이브(off)는 접힘·가운데 규칙 그대로(byte-identical).

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `useVariant` 클라이맥스 분기 +
  `InheritLanes` 신규 레인 개방(게이트) + `ResolveBlockConcept` 스레딩
- `Assets/_Project/Data/WaveConcepts/Concept_Swarm.asset`·`Concept_Heavy.asset` —
  variantSlots `laneGroup 0 → 1` (협공 저작; off 덱은 접힘이라 무변경)
- `Assets/_Project/Tests/EditMode/WaveConceptVariantTests.cs` — 격상·개방·off 접힘 pin
- 스킬 갱신 트리거 표 — InheritLanes·변주 게이트 행 추가

## 구현

1. 기존 계약 ③(«변주 입구가 묶음 배정을 벗어나지 않는다»)은 **게이트 off 덱의 계약**으로
   스코프가 좁아진다 — 그 계약이 지키는 건 본 편성 레인의 안정성이고, 게이트 on 의 신규
   레인 개방은 기존 입구를 옮기지 않는다(추가만).
2. 신규 레인 배정은 rng 무소비 결정론: 미사용 레인을 낮은 번호부터, 같은 laneGroup 은 같은
   레인 공유, 소진 시 접힘 폴백.
3. 그라데이션 = 협공 빈도: break 전 1/3(가운데) → break 후 3/3(상시). 사용자 결정
   «조직된 그룹 협공 기준»의 구현이다 — 「평소」의 개체 분산 저작은 손대지 않는다.

## 완료 기준

- EditMode: 격상(break 후 전 웨이브 변주 / 전 가운데만)·신규 레인 개방(on)·접힘 유지(off)
  pin 초록, 기존 변주 pin(①~③) 무회귀

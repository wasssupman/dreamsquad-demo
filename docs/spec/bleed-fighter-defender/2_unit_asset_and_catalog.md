# 2 — 유닛 에셋 + 카탈로그 등록 + Play 검증

## 목적

난도질꾼을 로스터에 올리고 실전 체감을 확인한다.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_Slasher.asset` (신규)
- `Assets/_Project/Data/DefenderCatalog.asset` (units 배열 등록)

## 구현

1. **유닛**: id `slasher` · 표시명 "난도질꾼" · role Fighter · rarity Common · cost 2 · HP 350 ·
   attackRange 1 · attackCooldown 0.9 · hitDelaySec 0.3 · attackTargetCount 1 ·
   outputs `[Damage 8, ApplyStack(Bleed, mag 1, duration 4, stackMaxStack 5)]` · projectile 없음(근접).
2. **배치 스킬**: `onPlaceEffect ApplyStackNearby` · `onPlaceStackKind Bleed` · `onPlaceRange 2` ·
   `onPlaceMagnitude 1` · `onPlaceDuration 4`.
3. portrait/skeleton/deployVoice 는 placeholder 재사용(guid 유지 교체 전제). `desc` 는
   `UnitKitSummary` 폴백 확인 후 필요 시 한 줄 저작.
4. **카탈로그 등록** + `.meta` 짝 커밋. 시트 행 추가는 커밋 후.

## 완료 기준

- [ ] compile clean + `DcApplicabilityMatrixTests`/`UnitKitSummaryTests` green (전수 스캔 자동 편입)
- [ ] 에디터 Play: 배치 순간 주변 적들에게 출혈 도트가 붙고(데미지 넘버 지속 발생), 이후 근접 공격마다 도트가 갱신·중첩되는지
- [ ] 적이 사거리를 벗어나 이동 중에도 도트 데미지가 계속 닳는지 (차별점 체감 확인)

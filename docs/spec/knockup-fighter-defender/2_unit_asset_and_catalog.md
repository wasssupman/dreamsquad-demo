# 2 — 유닛 에셋 + 카탈로그 등록 + Play 검증 (심 선행)

## 목적

말파이트를 로스터에 올리고 광역 넉업의 실전 체감을 확인한다.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_Malphite.asset` (신규)
- `Assets/_Project/Data/DefenderCatalog.asset` (등록)

## 구현

1. **유닛**: id `malphite` · 표시명 "말파이트" · role Fighter · rarity Epic · cost 5 · HP 600 ·
   attackRange 1 · attackCooldown 2.0 · hitDelaySec 0.3 · attackTargetCount 3 ·
   outputs `[Damage 20]` · `knockupOnHitSec 0.8` · projectile 없음.
2. **배치 스킬**: `onPlaceEffect StunNearby` · `onPlaceRange 1` · `onPlaceDuration 0.8`.
3. portrait/skeleton/deployVoice placeholder 재사용(guid 유지 교체 전제). `desc` 는
   `UnitKitSummary` 폴백 확인 후 필요 시 저작 — 폴백이 knockup 필드를 모르면 저작이 정답.
4. **카탈로그 등록** + `.meta` 짝. 시트 행은 커밋 후.

## 완료 기준

- [ ] compile clean + `DcApplicabilityMatrixTests`/`UnitKitSummaryTests` green
- [ ] 에디터 Play: 3체 동시 히트 → 3체 모두 0.8s 정지(스턴), 쿨다운 2s 리듬으로 반복 — **떠오름 연출은 unit 3**
- [ ] 배치 순간 1타일 내 적 전원 정지 확인
- [ ] 보스 상대 넉업이 과하게 강하지 않은지 체감 메모 (면역은 후속 후보)

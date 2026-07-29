# 0 — 히트스캔 유닛 SO + 카탈로그 + 고속 틱 통합 테스트

## 목적

"0.2초마다 7 데미지" 심 모델을 데이터로 성립시키고 고정한다. 빔 비주얼(unit 1) 없이도
시뮬은 완결 — 이 unit 만으로 데미지가 실전에서 돈다.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_Busters.asset` (신규)
- `Assets/_Project/Data/DefenderCatalog.asset` (등록)
- `Assets/_Project/Tests/EditMode/` 또는 `Tests/PlayMode/` — 고속 틱 케이스 1개

## 구현

1. **유닛**: id `busters` · 표시명 "버스터즈" · role Ranger · rarity Epic · cost 4 · HP 160 ·
   attackRange 3 · attackCooldown 0.2 · **hitDelaySec 0**(계약 2) · attackTargetCount 1 ·
   outputs `[Damage 7]` · **projectile 비움**(히트스캔 사양 — 계약, 실수 아님을 에셋 desc 나
   spec 으로 남긴다).
2. **테스트**: 무투사체 원거리 유닛의 직접 데미지 경로 고정 — 사거리 3 에서 1초 시뮬 시
   적 누적 피해 ≈ 5틱 × 7 (attackCooldown 소진 규칙에 따른 허용 오차 명시). 기존
   AttackSystem 통합 테스트 하네스가 있으면 그 파일에 케이스 추가.
3. **카탈로그 등록** + `.meta` 짝. portrait/skeleton placeholder 재사용. 시트 행은 커밋 후.

## 완료 기준

- [ ] compile clean + 신규 테스트 green + `DcApplicabilityMatrixTests`/`UnitKitSummaryTests` green
- [ ] 에디터 Play: 빔 없이도 사거리 3 에서 데미지 넘버가 초당 5회 뜨는지 (심 선행 확인)
- [ ] 공격 애님/SFX 가 0.2s 마다 재트리거되는 문제의 실태 관측 → unit 1 코얼레스 규칙의 입력으로 기록

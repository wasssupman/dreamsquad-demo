# 5. AttackState.damage Removal (severable)

## 목적

어떤 시스템도 읽지 않는 ECS `AttackState.damage` 필드를 제거한다. severable — 드랍/실패 시 단독 revert 가능하며 unit 0~4 성립에 영향 없음.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackState.cs:8` — `damage` 필드 삭제
- `Assets/_Project/Scripts/Battle/Combat/TauntAttackGrantSystem.cs:44` — `damage = p.damage` dead write 제거 (aggro 데미지는 outputs 항목으로 별도 전달되므로 동작 무변화 — `:57` `magnitude = p.damage`는 유지)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs:267` — "AttackState.damage remains..." 주석 정리
- `Assets/_Project/Tests/EditMode/` 내 AttackState initializer를 쓰는 테스트 ~10파일 — `damage =` 라인 기계적 제거

## 구현

- Combat 소유 컴포넌트를 Combat 파일 내에서만 수정 (맥락 경계 준수).
- unit 4 완료 후에만 착수 (베이크 라인이 먼저 사라져야 필드 삭제가 compile-safe).

## 완료 기준

- [ ] compile 오류 없음
- [ ] 전체 EditMode 스위트 회귀 없음 (수정된 ~10 테스트 파일 포함)
- [ ] Play smoke: 데미지/aggro(taunt) 공격 동작 전후 동일

# 6 — 시트 Effects 탭 (저작 3단계)

## 목적
시트는 **소유자별 탭**(UnitSkills · Dreamcatcher · Nightmares), 코드는 한 레이어(사용자 의견 · 가이드 12 §2). 효과는 Effects 탭에서 한 번 정의하고 소유자 탭은 id 로 참조한다. 임포터는 탭 → 출처 꼬리표만 붙인다.

## 변경 대상
- `Editor/UnitStatImport/DcSheetExporter.cs` · `DcSheetApplier` — Effects 탭 export/import · 소유자 탭 세 개 **같은 열 스키마**(트리거 · 주체 · 게이트 · 수명 · 효과 id).
- `EffectWitness`(`Scripts/BattleCore/Trigger/EffectWitness.cs`) Assets lane 테스트 — 카드뿐 아니라 유닛 능력·악몽까지 전 출처.
- `docs/spec/README.md` 「시트 ↔ 저작 ↔ 코어 정합 감사」 — 죽은 컬럼 4 제거 · 거짓 문안 2(`bomb_man` · `boomerang`)는 **사용자가 시트에서** 고친다(여기선 목록만).
- unit 5 가 남긴 값 필드 제거.

## 구현
- 시트 쓰기는 사용자 승인 후(시트는 외부 서비스). 설계 확인은 curl 읽기 전용.
- 임포터 dry-run 이 없으므로, 에셋 쓰기 전 diff 표를 로그로 먼저 낸다.

## 완료 기준
- export → import 왕복 후 굽힌 `BindingDef` 줄 필드 동치 · Assets lane(선행 2 외 0) · 위트니스 전 출처 초록.
- 사용자 시트 확인.

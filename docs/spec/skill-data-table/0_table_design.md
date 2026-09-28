# 0 — 새 표 설계 (문서)

## 목적
시트 · 저작 · 코어가 같은 모양을 보게 표 구조를 먼저 정한다. 기존 시트는 폐기 전제라 호환을 고려하지 않는다. 코드 변경 0.

## 변경 대상
- 이 폴더 `tables.md` — 표마다 (한 줄의 뜻 · 열 · 키 · 참조 · 검증 규칙):
  - **Effects**: `effect_id` · 효과 종류(`DcPayloadKind` 이름) · 수치 칸들 · 수치 방식(고정 / 비율 + 기준 스탯, U7) · 탄/패턴/장판 id · 착탄 예고(U1).
  - **Projectiles · Patterns · Hazards**: 기존 SO 필드를 열로. `id` 는 오늘의 `string Id` 그대로.
  - **소유 줄**(Skills): `owner_kind`(card · unit · enemy) · `owner_id` · 트리거(종류 · 주기 · 경계 · N · 주체 · 게이트) · 수명 · `effect_id`. 한 소유자가 여러 줄.
  - **Cards · Units · Enemies**: 소유자 고유 값만(스킬 칸 없음). 카드 `host_kinds`(U5 기본 = 방어유닛).
- id 규칙: 소문자 스네이크 · 전역 유일 · 삭제 대신 폐기 표시(서버 어휘 — 계약 6).
- `docs/reference/battle-core-architecture.md` — README 계약 1~6 을 **예정**으로.

## 완료 기준
- `tables.md` 의 열이 라이브 저작 전량(카드 52 · 유닛 능력 18 · 악몽 6 · 탄 · 패턴 · 장판)을 **손실 없이** 담는지 전수 대조표(필드 → 열) 첨부. 담지 못하는 필드는 사유.
- 사용자 확인(표 모양은 저작자가 쓰는 도구라 승인 대상).

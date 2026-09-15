# 4 · 저작 + 검증

## 목적

검증 질문에 답한다 — **세트 저작 유닛 1기가 Spine 유닛과 같은 판에서 같은 규칙으로 뜨고·싸우고·죽고·카드가 붙는가.**

## 변경 대상

- `Assets/_Project/Sprites/Unit/good_{idle,attack,drag}_alpha.png` 재슬라이스(피벗 BottomCenter)
- `Assets/_Project/Data/Flipbook/Flipbook_good_*.asset` (기존 3개 — 참조 보존)
- 신규 `Assets/_Project/Data/Flipbook/MotionSet_good.asset` (`UnitSpriteMotionSet` · idle/attack/drag · `sheetFacesRight = true`)
- 대상 유닛 SO 1기의 `spriteMotions` 필드 — **어느 유닛인지는 사용자 결정**(good = 파란 정장·대검 캐릭터)
- 신규 `Assets/_Project/Tests/PlayMode/SpriteUnitBackendPlayTest.cs`

## 구현

**시트 규약(확정)**: PPU **128**(2026-09-15 확정 · 상대 튜닝 240 → 160 → 106.67 → 133.33 뒤 2의 거듭제곱으로 반올림) · 셀 640×360 · `{unit}_{motion}.png`(원본) → `{unit}_{motion}_alpha.png`(배경 제거) →
`Flipbook_{unit}_{motion}` · 피벗 BottomCenter · idle/walk/drag 루프 · attack/death/deploy 원샷 · fps 24.
배경 제거는 테두리 flood-fill(내부 흰색 보존) — 스크립트는 `docs/reference/lessons/03-rendering-assets.md` 에 승격.

**크기 맞추기** — Spine 유닛 옆에 세우고 `spineVisualScale` 만 돌린다(PPU·인스턴스 scale 금지 — 노브 하나 계약).
실측: PPU 240 에서 이쑤시개 실높이 1.02(Spine 힐러 1.22) → 사용자 「너무 작다」 → PPU 160 으로 1.48 → 「50% 더」 106.67(2.2) → 「20% 작게」 133.33(약 1.78) → **128 로 확정**(약 1.85 · Spine 힐러 1.26 의 약 1.47×). `spineVisualScale` 1.69 는 그대로.

**PlayMode 테스트** — 라이브 에셋을 건드리지 않는다: `Instantiate(Defender_X)` 로 SO 를 복제해 `spriteMotions` 를
꽂고 스크립트 배틀에 배치 → `TryGetUnitView` 가 `SpriteUnitView` 를 돌려주고 · `CurrentAnimationName == "Flipbook_good_idle"` ·
공격 사건 뒤 `"Flipbook_good_attack"` · `TryGetUnitScreenRect` true · 세트를 뗀 복제본은 `SpineUnitView`. (기존 `PatrolDefenderPlayTest` 관용구)

**Play 확인(사용자)** — 대상 유닛을 실제 판에 배치: 드래그 그림 → 배치 모션 → idle → 적 접근 시 attack 압축 재생 →
드림캐쳐 카드 부착(픽킹) → 펀치/플래시 → 사망/퇴근.

## 완료 기준

- PlayMode 신규 1파일 초록 · 기존 PlayMode 회귀 0.
- 사용자 Play 확인: 위 시퀀스에서 캡슐·흰 박스·중앙 피벗 부양·좌우 반전 오류 0.
- `docs/reference/object-pipeline-map.md` 방어유닛/적 View/Pool 행에 `SpriteUnitView` 한 줄 추가(같은 커밋).

---

2026-09-15 에셋·테스트 커밋 · `bcfd2a60` — 재슬라이스 pivot.y=0 확인 · EditMode 2697 초록 · **PlayMode lane 미실행 · 대상 유닛 미정 · 사용자 Play 확인 대기.**

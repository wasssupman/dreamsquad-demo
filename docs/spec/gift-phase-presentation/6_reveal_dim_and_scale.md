# 6 — 리빌 튜닝: 과시 스케일 축소 + 간격 확대 + 뒷판 딤

## 목적

리빌 근접감(과시 스케일 2.1)이 실기에서 과대하다는 사용자 판정(2026-07-14).
① 과시 스케일을 약 20% 축소, ② 선물 2장 간격을 넓히고, ③ 리빌 동안 뒷판(내 덱
그리드)에 딤 스크림을 깔아 톤을 죽여 선물 카드를 강조한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` — 기본값 2건 + 딤 필드 2건
- `Assets/_Project/Data/Config/GiftConfig_Default.asset` — 명시값 갱신(계약 4)
- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — 딤 레이어 생성/안무

## 구현

- **수치**: `revealScale` 2.1 → **1.7** (−19%), `revealSpreadX` 210 → **240**
  (스케일 축소로 벌어진 시각 간격에 소폭 가산 — 카드폭 180×1.7=306, 중심간 480 → 틈 174px).
- **신규 노브**: `revealDimAlpha`(기본 0.6) / `revealDimFadeSec`(기본 0.25).
- **딤 레이어**: `_cardsRoot` 자식 풀블리드 흑색 Image 1장(코드빌드, `raycastTarget=false`
  — 탭 스킵은 패널 Dim 의 TapCatcher 소관). **sibling index = baseN** 에 삽입해
  그리드 10장 위 / 선물 2장 아래에 놓인다(같은 부모라 정렬 보장).
  - 페이드 인: 리빌 approach 와 동시(`revealDimFadeSec`).
  - 유지: 플립~과시~읽기 홀드 내내.
  - 페이드 아웃: 스택 수렴과 동시 — 기존 수렴 완료 콜백에서 disable(픽셀필 절약).
  - 탭 스킵 착지(`SkipToRevealFocus`)는 즉시 `revealDimAlpha` 로 세팅(전이 없음).
  - 시퀀스 재시작(`PlayGiftSequence` 초기화)에서 alpha 0 + disable 리셋.
- **불변**: 순서 계약·타이밍 노브·스테이지 구조·탭 스킵 2단 동작 무변경.
  딤은 gift 페이즈 코드빌드 UI 로 계약 6(정리)·계약 7(씬 배선 불변)을 따른다.

## 완료 기준

- compile 클린, console 에러 0.
- Play 육안(리빌 포커스): 선물 카드가 기존보다 작고 간격이 넓으며, 뒷판 그리드가
  딤 아래로 톤 다운되어 선물 카드가 명확히 강조된다. 스크린샷 확인.
- 스택 수렴 진입 시 딤이 걷히고 잔류하지 않는다(자연 진행 + 탭 스킵 양 경로).
- 페이즈 이탈/재진입 시 딤 잔류 없음.

- 확인 2026-07-14 (사용자 통과 확인) — 커밋 13988b66 (rebase 전 01fbc8b0)

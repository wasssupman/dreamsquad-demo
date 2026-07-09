# 0 — 절차적 라운드렉트/원 스프라이트 공용 헬퍼

## 목적

라운드렉트/원 배지 스프라이트를 런타임에 절차적으로 굽는 헬퍼를 공용화한다. 현재 `ScoreHudView.MakeRoundedRectSprite` 에만 사설로 존재하고, unit 1~2 의 패널 프레임·행 플레이트·순위 배지가 같은 것을 필요로 한다 — 소비자 2+ 이므로 추출한다("반복이 생기면 추출" 규칙).

## 변경 대상

- `Assets/_Project/Scripts/UI/UiRoundedSprite.cs` (신설, static)
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 사설 `MakeRoundedRectSprite` 를 공용 헬퍼 호출로 교체(동작 동일)

## 구현

- `Wassup.UI.UiRoundedSprite`:
  - `Make(float radius, float border, Color fill, Color borderColor)` → 9-slice 라운드렉트 `Sprite`. `ScoreHudView.MakeRoundedRectSprite` 의 SDF 알고리즘을 그대로 이관(반경 r, border b, `pad = 2b+8`, 안티에일리어스, 9-slice border `bd = r+b+1`). 반환 스프라이트는 `Image.Type.Sliced` 로 쓴다.
  - `MakeCircle(int diameter, Color fill, float border = 0, Color borderColor = default)` → 순위 배지용 꽉 찬 원. `Make` 에 `radius = diameter/2`, `pad` 최소로 사실상 원형. 배지는 sliced 불필요(고정 크기)라 단순 원 텍스처로 충분.
  - 생성 텍스처는 `RGBA32`, `Clamp`, `Bilinear`. 픽셀 계산은 순수(외부 상태 없음) — Burst 무관(MonoBehaviour UI 계층).
- `ScoreHudView`: 기존 `MakeRoundedRectSprite(...)` 본문을 지우고 `UiRoundedSprite.Make(...)` 로 위임. plate/tab 두 호출부 그대로 동작.

## 완료 기준

- [ ] compile 통과 (`read_console` 에러 0)
- [ ] Play: 인게임 진입 시 점수 HUD 배지(네이비 플레이트+골드 테두리+SCORE 탭)가 이전과 동일하게 렌더(회귀 없음)

확인: 2026-07-08 — `UiRoundedSprite.Make/MakeCircle` 추출, `ScoreHudView.MakeRoundedRectSprite` 위임 이관. compile 0 에러. HUD 회귀는 인게임 플레이에서 배지 정상(사용자 스크린샷).

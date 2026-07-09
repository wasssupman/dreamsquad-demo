# 0 — StartButton 우하단 재배치 + 배경 이미지 슬롯

## 목적

배치 페이즈의 `START BATTLE` 버튼을 화면 중하단 → **우하단**(타이머+NextWave dock 코너)
으로 옮긴다. 동시에 버튼 배경에 **Sprite 슬롯**(`startButtonBackground`)을 두어, 할당되면
그 이미지를, 비면 `UiRoundedSprite` 절차 플레이트를 쓰도록 한다. 실제 캐주얼 그래픽
할당은 unit 1(Codex).

## 변경 대상

- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — StartButton 앵커/피벗/위치/치수
  를 `NextWaveDock` 코너와 정렬로 변경. 배경 Image 에 `startButtonBackground` 우선, 없으면
  `UiRoundedSprite.Make(...)` 폴백. 스타일 값·스프라이트 슬롯은 SerializeField.

## 구현

1. `[SerializeField] private Sprite startButtonBackground;` 추가.
2. BuildCanvas 의 StartButton 배치를 우하단으로:
   - `anchorMin/Max = (1,0)`, `pivot = (1,0)`, `anchoredPosition ≈ (-40, 40)` — dock 앵커와
     동일 코너. 치수는 dock 폭(≈250)에 맞춰 `≈ (250, 92)` 안팎.
3. 배경 Image: `startButtonBackground != null` 이면 그 스프라이트, 아니면
   `UiRoundedSprite.Make(cornerRadius, borderWidth, fill, border)`. 둘 다 `Type.Sliced`.
   색은 스프라이트가 운반(Image=white) → Button ColorTint 호버/프레스 유지.
4. TMP 라벨 `START BATTLE` 오버레이 — **게임용 디스플레이 폰트(Bangers SDF)** +
   크림/화이트 + **다크 외곽선**(instanced fontMaterial, `Keyword_Outline`) + autosize.
   기본 오피스 폰트(LiberationSans)로는 캐주얼 버튼 느낌이 안 나므로 폰트 교체가 핵심.
   폰트는 SerializeField `startLabelFont` 로 씬 배선.
5. **코너 가시성 juice** (사용자 요구 "더 잘보이게"): 코너 래퍼 아래 아우라·버튼을
   중앙정렬 형제로 두고, 버튼은 브리딩 펄스(`startPulseScale`), 뒤 골드 아우라는 2배
   진폭으로 독립 펄스(`PrimeTween`, `useUnscaledTime`). 폴백 플레이트는 밝은 앰버 바디
   + 밝은 골드 림(진한 라벨) — 다크 배경에서 확 튀게. 표시 시 start / 숨김·OnDisable 시 stop.

## 완료 기준

- 컴파일/콘솔 클린.
- (육안) 배치 페이즈에서 START 버튼이 **우하단 코너**에 뜬다. 전투 진입 시 같은 코너를
  타이머+NextWave dock 이 이어받고 시각 충돌 없음.
- (육안) 배경 이미지 미할당 상태에선 절차 플레이트(다크+골드 테두리)로 보인다.
- 클릭 시 전투 정상 시작, 호버/프레스 피드백 유지. 코스트/페이즈 로직 무변경.

---
완료 확인: 2026-07-09 · 커밋 e25fb553

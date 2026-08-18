# 5 — 맵 설명 (B1)

## 목적

카운트다운 앞에서 **"어디에 놓을 수 있고, 목표가 무엇인지"** 를 말한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` (스텝 추가)

## 구현

unit 4 의 홀드로 카운트다운을 붙잡아 둔 상태에서 돈다. 전투는 아직 시작하지 않았고
적도 없으므로 이 구간에는 정지가 필요 없다.

**세 스텝.**

1. `bridge.ShowPlacementHighlight(cannon)` + `"배치 가능 영역"` — `briefingHoldSeconds`
2. `bridge.ShowBlockedHighlight(cannon)` + `"배치 불가 영역"` — `briefingHoldSeconds`
3. 1↔2 를 `briefingCycles` 회 왕복한 뒤 둘 다 끄고
   `"게임 목표: 최대한 많은 악몽 처치"` — `goalMessageSeconds`

그리고 `ReleaseIntroHold()`.

**하이라이트 기준 유닛은 캐논이다.** 배치 가능 칸은 유닛의 통행 층에 따라 달라지므로
(`EffectivePlacementLayers`) 기준 없이는 "배치 가능 영역"이 정의되지 않는다. 바로 다음
스텝에서 실제로 놓을 유닛과 같은 것을 보여주는 편이 거짓말이 아니다.

**문구만 바꾸고 하이라이트는 갈아끼운다.** 매 스텝 `ShowMessage` 를 다시 부르면
말풍선이 매번 새로 뜬다 — 왕복 동안은 문구 텍스트만 교체한다.

**이 구간은 딤을 쓰지 않는다.** 보여줄 것이 보드 전체라 딤이 그 위를 덮으면 무의미하다.
입력은 어차피 카운트다운 홀드가 막고 있다.

## 완료 기준

- compile 통과.
- 튜토리얼 판 진입 → 배치 가능(시안) ↔ 불가(차단색)가 `briefingCycles` 회 번갈아 뜨고
  각 면에서 해당 문구가 보인다.
- 마지막에 두 하이라이트가 모두 꺼지고 목표 문구가 뜬 뒤 3 · 2 · 1 · GO! 가 시작된다.
- 왕복 중 말풍선이 사라졌다 다시 뜨지 않는다(텍스트만 교체).
- `briefingCycles` 를 0 으로 두면 목표 문구만 뜨고 넘어간다(값 검증).

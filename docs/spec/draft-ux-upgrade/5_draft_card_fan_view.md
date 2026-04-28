# 5. Draft Card Fan View

## 목적

화면 하단에 10장의 카드를 fan 형태로 등장시키고, 폐기 시 toss + 남은 카드 재배치. 입력 처리는 task 6, 오케스트레이션은 task 7. 본 task 는 "보이는 fan + 입장 연출 + 정위치 재배치 + 폐기 toss" 만.

> **API (task 0 smoke 확정)**: `Tween.UIAnchoredPosition(rect, end, dur, ease)`, `Tween.LocalRotation(target, end, dur, ease)`, `Tween.Alpha(canvasGroup, alpha, dur, ease)`, `Sequence.Create()` + `.Chain(...)` / `.Group(...)` / `.ChainCallback(...)`. 종료 대기는 `await sequence` 또는 `sequence.ToYieldInstruction()`.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs`
- 신규: `Assets/_Project/Scripts/UI/Draft/DraftCardView.cs` (시각만, 입력 인터페이스는 task 6)
- 삭제 (본 task 끝에): `Assets/_Project/Scripts/UI/DraftCardView.cs` + `.meta` (옛 위치)

## 구현

1. `DraftCardFanView` MonoBehaviour. 자식 RectTransform `FanRoot` 를 만들어 카드 부모로 사용. anchor `(0.5, 0)-(0.5, 0)`, pivot `(0.5, 0)`, anchoredPosition `(0, 80)`.
2. fan 곡선 (Slay-the-Spire 식, 가운데 카드가 가장 높음):
   - 카드 수 N (≤10), 인덱스 i.
   - 각도 θ(i) = lerp(-26°, +26°, i/(N-1))
   - 호 반경 R = 1400px. center 카드(θ=0) 가 fan 의 정점.
   - 정위치 anchoredPosition: `(R*sin(θ), -R*(1 - cos(θ)) + 60)`. 가운데 카드 y = +60, 양 끝 y ≈ +60 - 1400*(1-cos26°) ≈ +60 - 130 = -70. → 가운데 위, 끝이 아래로 휘는 정상 fan.
   - 회전: `localRotation = Quaternion.Euler(0, 0, -θ * Mathf.Rad2Deg)` 가 아니라 θ 가 이미 도(°) 단위면 `Quaternion.Euler(0, 0, -θ)`. (단위 일치 주의 — 코드에서는 도 단위로 통일.)
   - 카드 사이즈 240×340.
   - 형제 인덱스 = i (좌→우 순서).
3. `DraftCardView` 시각:
   - RectTransform + Image 배경 (0.18, 0.18, 0.22) + 상단 swatch (DefenderUnitData 의 visualMaterial color) + 텍스트 라벨 (HP/RNG/DMG/CD).
   - 옛 `Assets/_Project/Scripts/UI/DraftCardView.cs` 의 시각 부분 옮김. 사이즈/스타일은 위 240×340 으로 통일.
   - CanvasGroup 컴포넌트 추가 (fade 용).
4. 공개 API (`DraftCardFanView`):
   - `void Build(IReadOnlyList<DefenderUnitData> pool)` — 기존 카드 destroy 후 신규 생성. 처음에는 화면 아래 `y = -300` 정렬, 회전 0, alpha 1. 각 카드의 `HomePosition` / `HomeRotation` 을 fan 곡선 정위치로 박음 (task 6 의 정위치 복귀가 이 값을 읽음).
   - `Sequence PlayEnterSequence()` — PrimeTween Sequence:
     - i=0..N-1 카드를 0.04s stagger.
     - 각 카드: anchoredPosition (-300 시작점) → fan 정위치 (0.45s OutQuad), localRotation 0 → fan 회전 (0.45s OutQuad). 같은 카드의 두 트윈은 `Sequence.Group`.
     - 전체 길이 ≈ 0.04*(N-1) + 0.45 ≈ 0.81s.
   - `Sequence PlayDiscardCard(DraftCardView card)` — toss + 회전 + fade out (0.45s) → destroy. 시작 시 `card.CanvasGroup.blocksRaycasts = false` 로 추가 입력 차단. 직후 `LayoutRemaining()` 호출은 toss 의 0.05s 정도 시점에 시작 (fan 재배치는 toss 와 거의 병렬). 본 메서드는 toss Sequence 자체를 반환 (오케스트레이터 task 7 가 await 가능).
   - `void LayoutRemaining()` — `FanRoot` 의 활성 카드 수에 맞춰 fan 곡선 재계산 + 각 카드의 `HomePosition`/`HomeRotation` 갱신 + PrimeTween Tween 으로 0.20s OutQuad 이동/회전.
5. PrimeTween 사용 메서드 (확정):
   - 위치: `Tween.UIAnchoredPosition(rect, target, dur, ease)`
   - 회전: `Tween.LocalRotation(rect, Quaternion.Euler(0, 0, -θ), dur, ease)`
   - 페이드: `Tween.Alpha(canvasGroup, 0f, dur, ease)`
   - 시퀀스: `Sequence.Create()` + `.Chain(t)` (직렬) / `.Group(t)` (병렬) / `.ChainCallback(action)` (사이 콜백)
   - 종료 대기: `await sequence` 또는 코루틴 `yield return sequence.ToYieldInstruction()`
6. 카드 prefab 도입은 본 spec 범위 밖. 모든 카드 런타임 빌드.
7. 본 task 끝에서 옛 `Assets/_Project/Scripts/UI/DraftCardView.cs` + `.meta` 삭제. 이름 충돌 방지.

## 완료 기준

- `Build(pool)` + `PlayEnterSequence()` 호출 시 10장이 화면 아래에서 fan 으로 펼쳐짐 (≈0.85s).
- fan 곡선이 가운데 위, 양 끝 아래로 휘는 정상 모양. 가운데 카드 회전 ≈ 0°, 양 끝 ±26°.
- `LayoutRemaining()` 호출 시 남은 카드가 0.20s 안에 새 fan 곡선으로 재배치.
- `PlayDiscardCard(card)` 호출 시 카드 위로 toss + 회전 + fade out (0.45s) 후 destroy. 추가 입력 차단됨.
- 옛 `DraftCardView.cs` (옛 위치) + .meta 삭제 완료.
- 컴파일 에러 0.

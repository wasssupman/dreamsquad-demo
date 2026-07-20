# 0 · FlipbookCharacterView — 상태별 플립북 재생

## 목적

캐릭터 하나의 **상태 5개**를 플립북 5개에 매핑하고, 상태 전이(원샷 → `Idle` 복귀)를 소유한다.
프레임 진행·클럭·스프라이트 쓰기는 기존 `SpriteFlipbookPlayer` 가 이미 하므로 **여기서 다시 하지 않는다.**

## 변경 대상

- `Assets/_Project/Scripts/Presentation/FlipbookCharacterView.cs` (신규)
- `Assets/_Project/Tests/EditMode/FlipbookCharacterViewTests.cs` (신규)

기존 파일은 수정하지 않는다.

## 구현

### 상태와 정책

```csharp
public enum FlipbookCharacterState { Idle, Attack, Death, Deploy, Drag }
```

정책은 상태가 소유한다. 둘 다 `public static` 순수 판정이라 테스트가 직접 닿는다.

| 상태 | 루프 | 완주 후 |
|---|---|---|
| `Idle` | O | — (계속 루프) |
| `Drag` | O | — (계속 루프) |
| `Attack` | X | `Idle` 복귀 |
| `Deploy` | X | `Idle` 복귀 |
| `Death` | X | **마지막 프레임 유지** (복귀도 파괴도 없음) |

- `ShouldLoop(state)` — `Idle`/`Drag` 만 참
- `ReturnsToIdle(state)` — `Attack`/`Deploy` 만 참. `Death` 는 원샷이지만 **거짓**

`Death` 가 원샷이면서 복귀하지 않는다는 게 이 표의 유일한 비자명 지점이다.
`!ShouldLoop` 로 복귀를 판정하면 사망 캐릭터가 살아나므로, 두 술어는 분리돼 있어야 한다.

### 슬롯과 폴백

`Idle`/`Attack`/`Death`/`Deploy`/`Drag` 5개 `SpriteFlipbookData` 필드.
`Resolve(state)` 가 비어 있으면 `Idle` 을 돌려준다 (필수 3 + 선택 2 계약).
`Idle` 자체가 비면 `null` — 재생기가 `FrameCount == 0` 에서 알아서 정지한다.

**폴백은 데이터만이 아니라 상태까지 접는다.** `Play(Deploy)` 가 빈 슬롯을 만나면
`_current` 도 `Idle` 이 된다.

데이터만 접고 상태를 `Deploy` 로 남기면 이렇게 갇힌다:

```
_current = Deploy        → ReturnsToIdle(Deploy) = true   (복귀를 기다린다)
재생 중 = idle 데이터    → loop = true → IsPlaying 영원히 참
                         → 복귀 조건이 영영 성립하지 않는다
```

`Current` 가 영구히 `Deploy` 에 갇히는데 **화면에는 idle 이 정상 재생돼 보여서 증상이 드러나지 않는다.**
구현 중 EditMode 테스트(`ValidLoopPolicy_LogsNothing`)가 오탐 에러 로그로 먼저 잡아낸 버그다.

### 재생

`[RequireComponent(typeof(SpriteFlipbookPlayer))]`. 뷰는 `player.Play(data)` 만 호출한다.

전이 폴링은 `Update` 본문이 아니라 **`public void PollPlayback()`** 에 둔다.
`SpriteFlipbookPlayer.Tick` 이 `Update` 에서 분리된 것과 같은 이유 — 비포커스 에디터나
EditMode 테스트에는 프레임이 없어서, 검증 툴이 전이를 직접 전진시킬 수 있어야 한다.

```csharp
private void Update() => PollPlayback();

public void PollPlayback()
{
    if (!ReturnsToIdle(_current)) return;
    if (_player.IsPlaying) return;
    Play(FlipbookCharacterState.Idle);
}
```

**재생기의 자가 tick 은 그대로 둔다.** 뷰가 클럭을 이중 소유하면 프레임이 두 배로 진행한다.
결과적으로 전이가 최대 1프레임 늦지만 눈에 보이지 않는다 — 확인용 도구에 적절한 트레이드오프다.

### 루프 정책 위반 방어

원샷 상태에 루프 데이터가 들어오면 `IsPlaying` 이 영원히 참이라 **상태가 갇힌다** (README 함정 참조).

- `Play(state)` — 위반 시 `Debug.LogError` 에 **상태 이름 + 에셋 이름**을 함께 찍는다.
  원인이 컴포넌트가 아니라 에셋 체크박스라, 로그가 에셋을 지목하지 않으면 추적이 오래 걸린다.
- `OnValidate` (`UNITY_EDITOR`) — 5슬롯 전체를 정책과 대조해 경고.

**감지만 하고 강제로 고치지 않는다.** 재생기가 `SpriteFlipbookData.Loop` 를 직접 읽으므로
런타임에 뒤집으려면 SO 를 쓰게 되고, 그건 확인용 도구가 사용자의 에셋을 조용히 바꾸는 것이다.

## 완료 기준

- compile clean.
- EditMode 테스트 통과:
  - `ShouldLoop` / `ReturnsToIdle` 가 5개 상태 전부에 대해 위 표와 일치.
  - **`Death` 는 완주해도 `Idle` 로 돌아가지 않는다** — 이 spec 의 핵심 계약.
  - `Deploy`/`Drag` 미할당 시 `Resolve` 가 `Idle` 데이터를 돌려주고, `Play` 는 `Current` 까지 `Idle` 로 접는다.
  - 위 접기가 과교정되지 않는다 — `Deploy` 슬롯이 **할당돼 있으면** `Current` 는 `Deploy` 로 남는다.
  - `Attack` 완주 후 `PollPlayback()` 이 상태를 `Idle` 로 바꾸고, 렌더러 스프라이트가 idle 프레임이 된다.
  - `Idle` 재생 중 `PollPlayback()` 을 여러 번 불러도 재시작하지 않는다(경과 시간 보존).
  - 원샷 상태에 루프 데이터를 넣으면 에러 로그가 나고 상태가 갇힌다(현재 동작을 고정).
- 기존 테스트 무회귀 — 특히 `SpriteFlipbookPlayerTests` · `FlipbookMathTests`.

---

확인 2026-07-20 · 커밋 `6af72181`.
격리 리그 배치 실행에서 EditMode **1078건 중 1076 통과 · 0 실패 · 2 스킵**(기존 스킵 그대로), 신규 14건.
`ValidLoopPolicy_LogsNothing` 이 구현 중 폴백 상태 갇힘 버그를 잡아내 계약을 고쳤다(위 "슬롯과 폴백" 참조).

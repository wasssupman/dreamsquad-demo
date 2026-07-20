# 0 · Flipbook Math (순수 프레임 선택)

## 목적

경과시간 → 프레임 인덱스 변환을 **아키텍처를 모르는 순수 함수**로 확정한다 (제약 10).
`SpriteRenderer`·`TimeManager`·에셋을 전혀 모르고 plain 값만 입출력하므로 EditMode 단위 테스트 대상이다.

경계 조건(루프 되감기 · 원샷 hold · 0 fps · 프레임 없음)이 분기로 갈리는 **비자명 로직**이고,
재생기·미래 소비자가 공유하며, 회귀 시 "애니메이션이 미묘하게 어긋나는" 형태로 조용히 깨지므로 추출 가치가 충분하다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Presentation/FlipbookMath.cs`
- 신규 `Assets/_Project/Tests/EditMode/FlipbookMathTests.cs`

## 구현

`public static class FlipbookMath` (namespace `Wassup.Presentation`), 함수 3개.

| 함수 | 계약 |
|---|---|
| `FrameAt(elapsed, fps, frameCount, loop)` | 프레임 인덱스. 프레임 없음 = `-1` |
| `Duration(fps, frameCount)` | 원샷 1회 길이(초) |
| `IsFinished(elapsed, fps, frameCount, loop)` | 원샷 완주 여부. 루프는 항상 `false` |

경계 규칙:

- `frameCount <= 0` → `FrameAt` 은 `-1` ("그릴 것 없음"). 0 이 아니다 — 0 은 유효 인덱스라 소비자가 빈 배열을 그리려 든다.
- `elapsed <= 0` → 첫 프레임. 재생 시작 프레임에서 음수 dt 가 들어와도 인덱스가 튀지 않는다.
- **원샷은 마지막 프레임에서 정지(hold)**, 루프는 `% frameCount` 로 되감는다.
- `fps <= 0` → `FrameAt` 은 첫 프레임 고정(0 나눗셈·무한 진행 방지). `Duration` 은 `0` 이므로
  **원샷은 즉시 완료**로 판정된다(무한 정지 방지), 루프는 첫 프레임에서 영구 정지한다.
- `NaN`/`Infinity` elapsed → **`float.IsFinite` 로 명시 차단**하고 첫 프레임을 준다. 예외를 던지지 않는다
  (매 프레임 호출되는 함수라 이상치 하나가 로그 폭풍이 된다).
  `FloorToInt` 결과의 부호에 기대면 안 된다 — float→int 캐스트가 x64 는 `int.MinValue`,
  **ARM64 는 saturate** 라 `+Inf` 가 `int.MaxValue` 로 떨어져 음수 가드를 통과한다. 그러면 루프 재생이
  `int.MaxValue % frameCount` 라는 임의 프레임에 영구 고착된다. 타겟이 Apple Silicon 에디터 +
  Android ARM64 라 이 분기는 실기기 쪽에서 터진다. (2026-07-20 리뷰 적발)

`Duration` 은 `IsFinished` 와 재생기 양쪽이 쓰고(2+ 호출처), `IsFinished` 는 "루프는 끝나지 않는다"는
규칙을 단독 소유해 소비자가 재유도하지 않게 한다.

## 완료 기준

- EditMode 테스트 통과. 최소 커버: 루프 되감기 · 원샷 hold · 정확한 프레임 경계(`elapsed == 1/fps` → 인덱스 1) ·
  `fps <= 0` · `frameCount == 0` · `frameCount == 1` · 음수 elapsed · 비유한 elapsed.
- 비유한 elapsed 테스트는 **`Is.EqualTo(0)` 로 못박는다.** `InRange` 로 느슨하게 두면 위 아키텍처 분기를
  통과시켜 결함을 은폐한다(실제로 그랬다 — 2026-07-20 리뷰).
- **`FrameAt` × `IsFinished` 결합 불변식** 을 별도 테스트로 고정한다: dt 를 쪼개 누적했을 때
  "완료로 판정되는 시점의 프레임 == `frameCount-1`". 재생기의 "반영 후 판정" 순서가 의미를 갖는 근거다.
- `FlipbookMath` 가 `UnityEngine.Mathf` 외의 Unity 타입을 참조하지 않는다 (아키텍처 무지 검증).

---

2026-07-20 확인 · `11932992` — EditMode 23건 통과

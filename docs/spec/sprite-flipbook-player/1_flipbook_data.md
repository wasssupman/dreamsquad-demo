# 1 · Sprite Flipbook Data (재생 데이터 SO)

## 목적

플립북 1개의 **고유 속성**(프레임 · fps · 루프)을 SO 로 확정한다 (제약 6 — 하드코딩 금지).
여러 소비자가 같은 애니메이션을 공유할 수 있어야 하므로 컴포넌트 직렬화가 아니라 에셋이다.

`TimeDomain` 은 **여기에 두지 않는다.** 도메인은 "이 애니메이션이 무엇인가"가 아니라 "어디에 쓰이는가"라
같은 에셋이 전투 이펙트와 UI 연출에 동시에 쓰일 수 있다. 도메인은 재생기 인스턴스가 소유한다 (unit 2).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/SpriteFlipbookData.cs`

## 구현

`[CreateAssetMenu(menuName = "Wassup/Sprite Flipbook", fileName = "SpriteFlipbook")]`, namespace `Wassup.Data`.

| 필드 | 의미 |
|---|---|
| `frames` (`Sprite[]`) | 재생 순서대로의 프레임. 컷 모드는 수동 할당, 통 모드는 unit 3 유틸이 채운다 |
| `fps` | 초당 프레임. 기본 24 |
| `loop` | 루프 여부. 기본 false(원샷) |

읽기 표면은 `FrameCount` · `Fps` · `Loop` · `Sprite FrameAt(int index)` 만 노출한다.
배열 자체를 내보내지 않는다 — 소비자가 밖에서 배열을 바꿔 SO 상태를 오염시킬 수 없게.

`FrameAt` 은 범위 밖 인덱스와 `frames == null` 에 `null` 을 돌려준다. `FlipbookMath.FrameAt` 이
"프레임 없음"에 `-1` 을 주므로 그 값이 그대로 흘러들어와도 안전해야 한다.

`OnValidate` 검증(에디터 전용, 에셋 이름과 함께 경고):

- `frames` 에 빈 슬롯이 섞임 → 경고. 재생기는 빈 슬롯에서 **직전 프레임을 유지**하므로(렌더러가
  순간 비는 것보다 낫다) 증상은 "프레임이 사라짐"이 아니라 "앞 프레임이 그만큼 길어짐"이다.
  단 인덱스 0 이 비면 유지할 직전 프레임이 없어 **렌더러에 authored 된 스프라이트가 그대로 남는다** —
  경고문이 두 경우를 구분한다. 조용히 넘기면 타이밍 디버깅에서 헤맨다.
- `fps <= 0` → 경고. 원샷이면 `IsFinished` 가 즉시 참이라 재생이 시작하자마자 끝난다.
  `fps` 의 NaN·`+Inf` 는 이 검사로 못 잡는다(NaN 비교는 전부 false) — 그쪽은 `FlipbookMath` 가
  유한성 검사로 단독 방어한다(unit 0).

## 완료 기준

- compile 통과 + `Assets/Create/Wassup/Sprite Flipbook` 으로 에셋 생성 가능.
- 빈 슬롯이 섞인 배열을 넣으면 인스펙터 조작 시 콘솔 경고가 에셋 이름과 함께 뜬다.
- `FrameAt(-1)` / `FrameAt(999)` 가 예외 없이 `null` 을 반환한다.

# 2 · Sprite Flipbook Player (재생 컴포넌트)

## 목적

`SpriteFlipbookData` 를 `SpriteRenderer` 위에서 재생하는 얇은 MonoBehaviour.
프레임 판정은 unit 0 순수 함수에, 재생 속성은 unit 1 SO 에 위임하고, 이 컴포넌트는
**시간 누적 + 스프라이트 반영 + 수명**만 소유한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Presentation/SpriteFlipbookPlayer.cs`

## 구현

`[RequireComponent(typeof(SpriteRenderer))]`, namespace `Wassup.Presentation`.

| 필드 | 의미 |
|---|---|
| `flipbook` | 재생할 SO. 비어 있으면 재생하지 않는다 |
| `timeDomain` | 클럭 도메인. 기본 `Battle` |
| `playOnEnable` | 활성화 시 자동 재생. 기본 true |
| `disableRendererWhenFinished` | 원샷 완주 후 `SpriteRenderer.enabled = false` |

공개 표면: `IsPlaying` · `IsLooping` · `Play()` · `Play(SpriteFlipbookData)` · `Stop()` · `Tick(float dt)`.

**재생 완료 이벤트는 만들지 않는다** (README 후속 후보). 소비자는 `IsPlaying` 을 폴링한다 —
첫 실사용처가 실제로 콜백을 요구할 때 추가한다.

`IsLooping` 은 그 폴링 계약을 성립시키기 위한 필수 짝이다. 루프 플립북은 `IsPlaying` 이 영원히 참이라,
이게 없으면 풀 회수 코루틴(`WaitWhile(() => player.IsPlaying)`)이 영구 대기하며 인스턴스를 샌다.
SO 에 `loop` 를 켜는 것만으로 컴파일도 경고도 없이 누수가 생긴다. (2026-07-20 리뷰 적발)

핵심 계약:

- **클럭은 `TimeManager.Instance.DeltaTime(timeDomain)`.** `Time.deltaTime` 금지 —
  전투 슬로우모 중 이펙트만 정속으로 돌아 어긋난다. `Update` 는 `Tick` 에 위임만 한다.
- **`Tick(float dt)` 를 `Update` 에서 분리**한다. 비포커스 에디터에선 프레임이 안 흘러
  검증 툴이 dt 를 직접 주입해야 한다(로비 캐릭터 선례).
- **프레임 반영을 완료 판정보다 먼저** 한다. 순서가 뒤집히면 마지막 프레임이 한 번도 안 그려진다.
- **빈 슬롯은 직전 프레임을 유지**한다. 렌더러가 순간 비는 것보다 낫고, 원인은 SO `OnValidate` 경고가 잡는다.
- **`SpriteRenderer.enabled` 는 `disableRendererWhenFinished` 가 켜졌을 때만 건드린다.**
  끄는 기능과 켜는 기능이 같은 플래그를 소유해야 이 플래그를 안 쓰는 소비자가 렌더러 상태를 온전히 소유한다.
- **그 플래그가 켜져 있으면 `Stop()` 도 렌더러를 끈다.** 소유권을 재생기에 넘긴 소비자는 중도 취소에서도
  정리를 기대한다 — 안 그러면 취소된 원샷의 중간 프레임이 화면에 멈춰 남는다(소비자는 소유권을 넘겼다고
  믿어 직접 끄지 않는다). 완주 경로만 끄고 취소 경로를 빼면 그 공백이 그대로 버그가 된다. (2026-07-20 리뷰 적발)
- **정렬(sortingLayer/order)·GameObject 수명은 건드리지 않는다.** 소비자 소유 (README 계약).
- `_renderer` 는 lazy 조회한다. 비활성 GameObject 에 `AddComponent` 직후 `Play()` 하면 `Awake` 전이라 NRE 가 난다.

## 완료 기준

- **`SpriteFlipbookPlayerTests` (EditMode) 통과.** 순수 함수 테스트로는 "반영 → 판정" 순서 계약을
  검증할 수 없다(판정을 앞으로 옮겨도 `FlipbookMath` 테스트는 전부 통과한다). `Tick(dt)` 를 직접 밀어
  완주 시점의 `SpriteRenderer.sprite` 가 마지막 프레임인지 확인하는 테스트가 그 회귀를 잡는 유일한 지점이다.
  렌더러 가시성 소유권(플래그 on/off × Play/Stop/완주)도 여기서 고정한다. (2026-07-20 리뷰 적발)
- compile 통과.
- 씬에서 `SpriteRenderer` + 이 컴포넌트 + 프레임이 든 SO 로 원샷/루프가 육안 확인된다.
- 원샷 완주 후 `IsPlaying == false` 이고 마지막 프레임이 화면에 남는다(`disableRendererWhenFinished` 해제 시).
- 전투 슬로우모 중 재생 속도가 같이 느려진다(`timeDomain = Battle`).

---

2026-07-20 확인 · `ff9ef18e`

- EditMode 13건 통과. mutation("반영 → 판정" 순서 뒤집기)으로 순서 계약이 실제로 지켜짐을 확인.
- 오프스크린 렌더 육안 확인 — Archer 컷씬 24프레임 원샷이 `001→005→010→015→020→024` 로
  전진하고, 완주 시 `IsPlaying=false` + 마지막 프레임 유지.
- 슬로우모 연동은 **부분 검증**: 클럭 소스(`TimeManager`)의 스케일링·도메인 격리·`Time.timeScale=1`
  고정은 실측했으나(lease 0.25 → `ScaleOf(Battle)=0.25`, `Interaction` 은 1 유지),
  재생기의 `Update` 가 그 dt 를 소비하는 경로는 코드 검토만 했다(Play 모드 실측 아님).
  첫 실사용 소비자가 붙을 때 판 위에서 확인할 것.

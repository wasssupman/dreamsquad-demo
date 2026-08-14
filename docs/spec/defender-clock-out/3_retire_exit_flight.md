# 3 — 퇴근 연출 (사망 애니 배제 · 위로 뽑히는 스냅)

> ## ✗ rev 3 시도 → **기각·롤백** (2026-08-14) — 키링 회수
>
> **가설**: rev 2 의 스냅은 *뽑는 주체가 화면에 없어* 유닛이 스스로 튀어 오르는 것처럼 보인다.
> 배치가 "키링에 매달려 판에 던져진다" 이므로 퇴근은 **그 줄이 다시 와서 채간다** 로 하면
> 인과가 붙고 픽션·메커닉도 맞는다(그 유닛은 실제로 트레이로 돌아가 쿨타임을 돈다).
>
> **구현**: `DragController.CreateKeyringHardware`(배치·재배치와 같은 소스) 로 고리+줄을 만들고,
> ① 구간에 줄이 위에서 내려오는 것과 유닛의 웅크림을 **겹쳐서** 배치, ② 구간엔 고리가 앞장서고
> 유닛이 딸려 오게 했다. 재배치의 스무스 추종(sway)은 버리고 즉시 추종(0.28초에 관성을 넣으면
> 줄이 뒤처져 "채간다"가 "따라간다"로 읽힌다).
>
> **결과 — 사용자 평가: "키링 붙인 느낌이 안 난다. 이 연출 구조에서 어쩔 수 없다."**
>
> **왜 안 되나 (다시 시도하지 말 것)**: 부착이 읽히려면 «줄이 닿는다 → 걸린다 → 팽팽해진다 →
> 끌려간다» 를 눈이 따라갈 시간이 필요한데, 이 연출의 정체는 **0.28초 스냅**이다. 줄이 내려올
> 시간이 0.10초뿐이고 채가는 건 OutExpo 라 첫 프레임에 이미 끝난다 — 즉 **"짧고 즉발이라 좋다"는
> rev 2 의 강점이 그대로 부착 연출의 불가능 조건**이다. 둘은 같은 축의 양 끝이라 한 연출에
> 공존하지 않는다. 키링을 살리려면 스냅을 포기하고 rev 1 계열의 긴 비행으로 돌아가야 하는데,
> 그건 이미 "밋밋하다"로 기각된 방향이다.
>
> **롤백 범위**: `DefenderRetireFlight.cs` 를 rev 2 커밋본으로 복원 · `BattleBridge` 의
> `Fly(..., binding.data)` 인자 되돌림 · 씬의 `defenderSelector`/`hookDropDistance` 제거(재저장).
> `BattleBridge.cs`·`BattleScene.unity` 는 다른 세션 작업이 섞여 있어 **통째 checkout 하지 않고**
> 내 변경만 되돌렸다.
>
> **남은 개선 방향**(사용자: "나중에 개선한다"): 뽑는 주체를 세우려면 **줄이 아닌 것**으로 —
> 예컨대 스냅과 동시에 터지는 위쪽 방향 임팩트(속도선·상승 퍼프)처럼 **시간을 요구하지 않는**
> 어휘여야 한다. 부착·연결처럼 «관계를 보여줘야 하는» 어휘는 이 길이에 안 들어간다.

---

> ## ⚠ rev 2 — 아치 이탈 폐기 (2026-08-14, 사용자 평가 "연출이 구리다")
>
> rev 1 은 배치 아치를 거꾸로 재생했다(0.55초 곡선 상승 + 축소). **진단은 방향이 아니라 구조였다** —
> 게임 필은 «예비 → 스냅 → 여운» 인데 rev 1 엔 **가운데만** 있었다:
> 예고가 없어 무슨 일이 일어나는지 못 읽고, 등속에 가까워 힘이 안 실리고, 0.55초는 전투에 비해 길다.
>
> **rev 2 = 3막 ~0.28초:**
>
> | 막 | 시간 | 무엇 |
> |---|---|---|
> | ① 웅크림 | 0.10s | 눌리며(세로 76%) 살짝 내려앉는다 — "당겨지기 직전" 텐션 |
> | ② 스냅 | 0.18s | 위로 **뽑혀 나간다.** OutExpo 즉발 가속 + 세로 2.3배·가로 0.2배 stretch |
> | ③ 여운 | 스냅과 동시 | 떠난 칸에 **배치 링**(`SpawnPlacementRing`) — 올 때 나던 그 링 |
>
> **죽음과의 구분이 더 선명해졌다**: 죽음은 아래로 무너지고, 퇴근은 **위로 뽑힌다.**
> squash&stretch 는 캐주얼 게임의 기본 어휘라 작은 화면에서도 읽힌다.
>
> **버린 것**: 아치·베지어·`KeyringSim`·`DragController` lazy 해석. 곡선은 "날아간다"를,
> 직선 스냅은 "뽑힌다"를 말한다 — 이 연출이 원하는 건 후자다. 컴포넌트가 오히려 짧아졌다.
> **얻은 것**: `VfxSpawner` 배선 1개(링). `Fly(view, simWorld)` 의 좌표 인자가 **이번엔 실제
> 소비처**를 갖는다 — `VfxSpawner` 가 진입부에서 `ToView` 하므로 **sim 을 넘겨야** 한다(이중 변환 금지).
>
> 아래 rev 1 본문은 "왜 뷰를 떼어내 따로 모는가"(Detach 계약·고아 방지·동시 퇴근)가 여전히
> 유효하므로 남긴다. 곡선·목적지 관련 서술만 위 표로 대체됐다.

---

# (rev 1 본문 — 구조 근거는 유효, 곡선 서술은 폐기)

## 목적

퇴근한 유닛이 **죽은 것처럼 보이지 않게** 한다. 사용자 결정 2026-08-13: "퇴근 유닛은 특별한
연출을 통해 사라지도록".

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitPool.cs` — `Detach(entity, out view)` 1개
- `Assets/_Project/Scripts/UI/DefenderRetireFlight.cs` — **신규**(작은 컨트롤러)
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 퇴근한 유닛 슬롯 1회 펄스
- `Assets/_Project/Scenes/BattleScene.unity` — 컴포넌트 1개 배선 (UnityMCP 자동화)

## 구현

**① 왜 기존 비행을 그대로 못 쓰나.** 재배치 비행(`DefenderRelocationController.RunRelocationFlight`)은
`bridge.SetDefenderViewOverride(entity, p, lift, baseline)` 로 **살아 있는 엔티티의 뷰**를 곡선 위로
밀어 넣는다. 퇴근은 엔티티를 즉시 파괴하므로 그 경로가 없다.

그렇다고 **파괴를 비행 끝까지 미루면 안 된다** — 그 동안 유닛이 판에 살아서 때리고 맞는다.
`RetireDefender` 의 원자성(판정 → 정리 → 파괴)도 깨진다. 선례가 답을 준다: 보스 도약은
*"sim 은 즉시 텔레포트하고 **뷰만** 아치로 날린다"*. **뷰를 떼어내 따로 날린다.**

```csharp
// SpineUnitPool — 뷰를 풀 관리에서 떼되 파괴하지 않는다. 호출자가 수명을 갖는다.
// Despawn(즉시 Dispose)·NotifyDeath(사망 애니 후 자멸) 와 나란한 세 번째 출구.
public bool Detach(Entity entity, out SpineUnitView view);
```

**② 곡선은 기존 것을 그대로 쓴다.** `DragController.ComputeThrowArc(start, end, camUp, boardRight,
gen, out c1, out c2)` → `KeyringSim.CubicBezier(...)`. 배치 비행·재배치 비행이 이미 공유하는 순수
헬퍼라 **신규 곡선 코드 0**, 퇴근은 세 번째 소비자다. 시계는 `TimeDomain.Battle`(둘 다 그렇다).

**③ 방향 = 위로 이탈.** `end` 는 `start` 에서 `camUp` 으로 밀어 올린 지점 + `boardRight` 약간.
꼬리에서 스케일 축소 + 페이드. **배치가 내려온 길을 거슬러 올라간다.**

⚠ **트레이 슬롯을 목적지로 삼지 않는다.** 그러면 "어디로 갔는지"가 더 선명하겠지만, 트레이는
UGUI 스크린 공간이고 비행 수학은 전부 뷰/월드 공간(`camUp`·`boardRight`·`BoardSpace`)이다.
공간을 건너는 변환은 캔버스 모드·카메라 설정에 의존해 이 unit 전체를 그 문제에 건다.
**같은 인과를 훨씬 싸게 얻는 길이 있다** — ④.

**④ 도착 신호는 트레이가 자기 공간에서 낸다.** 비행이 끝나는 순간(또는 퇴근 즉시)
`DefenderSelector` 가 그 유닛 슬롯을 **1회 펄스**한다. 쿨타임 오버레이는 어차피 그 칸에서 차오르기
시작하므로, 펄스 하나로 "저기로 갔고 저기서 돌아온다"가 읽힌다. 좌표 변환 0.

**⑤ 죽음과 움직임 문법으로 갈린다.**

| | 죽음 | 퇴근 |
|---|---|---|
| 애니 | `deathAnimation`(기본 `"die"`) | 없음 — 서 있는 채로 |
| 움직임 | 그 자리에 쓰러진다 | **온 길로 되돌아 올라간다** |
| 뷰 수명 | `Kill()` → 애니 완료 시 자멸 | `Detach` → 비행 끝에 파괴 |

**⑥ 고아 방지.** 떼어낸 뷰는 풀이 더 이상 모른다. 컨트롤러가 유일한 소유자이므로
**매치 teardown / 페이즈 이탈 / 씬 언로드에서 즉시 파괴**해야 한다. 재배치 비행의 `_flightGen`
세대 가드와 같은 형태로 진행 중 비행을 무효화한다. 동시 다발 퇴근이 가능하므로 **단일 슬롯이
아니라 리스트**로 들고 있는다(재배치는 단일 슬롯이었지만 그건 이동모드가 하나뿐이라서였다).

**⑦ 배선.** `DefenderRetireFlight` 는 `bridge.DefenderRetired` 를 구독한다. unit 1 이 이벤트에
실어 보낸 `Vector3`(셀 월드좌표)가 **아치의 출발점**이다 — 그 파라미터의 소비처가 여기서 생긴다.
씬 배선 1개는 **UnityMCP 로 자동화**하고 Play 검증까지가 완료다(사용자 수작업으로 미루지 않는다).

## 완료 기준

- 컴파일 통과.
- **PlayMode**: 퇴근 시 `spineUnitPool.TryGet(entity, …)` 가 false(뷰가 풀에서 빠졌다)이고,
  비행 종료 후 떼어낸 GameObject 가 파괴돼 씬에 남지 않는다.
- **PlayMode**: 비행 중 매치를 종료(teardown)해도 **고아 GameObject 가 0**이다.
- **PlayMode**: 두 유닛을 연속 퇴근시키면 두 비행이 **각각** 끝난다(단일 슬롯 덮어쓰기 없음).
- 육안: 퇴근한 유닛이 **쓰러지지 않는다.** 선 채로 위로 아치를 그리며 작아지고 사라진다.
- 육안: 그 순간 트레이의 그 슬롯이 한 번 반짝이고 쿨타임이 차오르기 시작한다.
- 육안: 배치 비행과 **같은 곡선 문법**으로 보인다(방향만 반대).
- **회귀**: 사망은 종전대로 `deathAnimation` 을 재생하고 쓰러진다.

> **자동 검증 2026-08-14** — 컴파일 통과(CS 에러 0).
> `DefenderRetireTest` **5/5**(신규 `Retire_DetachesView_AndFlightDisposesIt` 포함) ·
> 회귀 9개 클래스 **23/23**(재배치×3 · BoardLimit×2 · 순찰병 · PlacementAura · BountyMark · SlimeSplit).
>
> **구현하며 계획이 두 군데 틀렸다:**
> ⑴ `dragController` 를 `[SerializeField]` 로 잡으려다 실패 — `DefenderDragPlacementController` 는
>    씬 직렬화 대상이 아니라 `DefenderSelector` 가 **런타임 `AddComponent`** 로 만든다.
>    직렬화 필드로 뒀다면 인스펙터에서 영영 비어 곡선이 직선 폴백으로 **조용히 죽었을** 것이다.
>    `DefenderRelocationController` 가 쓰는 `defenderSelector.DragController` lazy 해석으로 맞췄다.
> ⑵ `Fly(view, startView)` 의 출발점 인자가 실제로 쓰이지 않았다 — 뷰의 자기 transform 이 더
>    정확하다(`SpineVisualOffset`·넉업 hop 이 얹혀 있고 그 차이가 곧 "있던 자리에서 뜬다").
>    인자를 지웠고, unit 1 에 적어둔 "`DefenderRetired` 의 `Vector3` 를 unit 3 이 소비한다" 는
>    주석도 **사실대로 고쳤다**(소비처 0, 형제 대칭으로 알고 남긴 인자).
>
> 씬 배선: `DefenderRetireFlight` 를 `DefenderRelocationController` GO 에 얹고
> (`defenderSelector`·`mainCamera`), `BattleBridge.retireFlight` 연결 후 BattleScene 저장.
> 저장 전 `dirty=False` 를 확인했다 — 남의 in-memory WIP 를 베이크할 위험이 없는 상태였다.

> **rev 2 자동 검증 2026-08-14** — 컴파일 통과(CS 에러 0).
> `DefenderRetireTest` **5/5** · 회귀 6개 클래스 **17/17**.
> 배선 갱신: `defenderSelector` 제거 → **`vfxSpawner` 추가**(링). BattleScene 재저장.
> 테스트 러너가 한 번 "failed to initialize" 로 죽어 `manage_editor stop` 후 재시도해 통과했다
> (이 프로젝트의 알려진 불안정 — 결과가 아니라 러너 초기화 실패다).
>
> **튜닝은 전부 인스펙터**(`DefenderRelocationController` GO → `DefenderRetireFlight`):
> `anticipationSeconds` 0.10 · `crouchAmount` 0.24 · `crouchDip` 0.14 ·
> `snapSeconds` 0.18 · `riseDistance` 4.6 · `stretchY` 2.3 · `stretchX` 0.2.
> 더 과격하게 하려면 `stretchY`↑ `stretchX`↓ `snapSeconds`↓, 더 묵직하게 하려면 `crouchAmount`↑
> `anticipationSeconds`↑.

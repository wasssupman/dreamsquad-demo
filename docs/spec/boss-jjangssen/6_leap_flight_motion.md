# 6 — 도약 비행 모션 (텔레포트 → 아치 도약) + 착지 슬램

## 목적

집단 도약이 **순간이동으로 보인다**는 사용자 피드백(2026-07-29). sim 은 그대로 텔레포트로 두고,
**뷰만** 출발지 → 착지점을 아치로 날려 "웅크렸다 뛰어 내리찍는" 모션으로 만든다.

`defender-drop-dismount` 의 하마 비행과 같은 문제·같은 해법이다 — 그 spec 의 궤적 수학
(`KeyringSim.DismountPoint`: 반동 Hermite → 비행 베지어 → **수직 끝접선 착지**)을 그대로 재사용한다.
신규 수학 0.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/BossLeapVisualEvents.cs` (신규) — Combat→Bridge 채널
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — 퍼프 2발 제거 + 비행 이벤트 emit
- `Assets/_Project/Scripts/Bridge/BattleBridge.BossLeap.cs` (신규 partial) — 오버라이드 + 드레인 + 코루틴
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 적 뷰 피드에 오버라이드 소비 2줄 + 큐 lifecycle
- `Assets/_Project/Data/` — 비행 튜닝 SO 값 (하드코딩 금지)

## 구현

### 왜 새 채널인가

기존 `BlinkRequestEventsSingleton`(Combat→Movement)은 **sim 텔레포트용**이고 `dataIndex`·출발 좌표를
싣지 않는다. 반면 `HealthThresholdSystem` 은 `transform.Position`(출발) · `destWorld`(착지) ·
`slot.projectileDataIndex`(연출) **셋을 모두 이미 갖고 있다** — 그러니 `BlinkApplySystem` 을 건드리지 않고
그 지점에서 프레젠테이션 신호를 따로 낸다. `KnockupVisualEvents`(Combat→Presentation 원샷) 선례와 동형.

```csharp
public struct BossLeapVisualEvent
{
    public Entity entity;
    public float3 fromWorld;   // 도약 직전 위치 (sim 은 이 프레임에 이미 toWorld 로 간다)
    public float3 toWorld;     // 착지 셀 중심
    public int dataIndex;      // 출발/착지 퍼프 ProjectileData index (<0 = 무연출)
}
```

> **README 계약 8 정정**: "신규 ECS 채널 0" 은 units 0~4 의 계약이었다. 이 unit 은 채널 1개를
> 추가한다(Combat→Bridge, 프레젠테이션 전용). sim 로직·맥락 경계는 불변이다.

### 퍼프 타이밍 — 착지 퍼프를 비행 끝으로 옮긴다

현재 `HealthThresholdSystem` 이 출발·착지 퍼프를 **둘 다 즉시** 쏜다. 비행이 생기면 착지 퍼프가
뷰가 도착하기 전에 터져 desync 다. 두 enqueue 를 **모두 제거**하고 브리지가

- 비행 시작 프레임 → 출발 퍼프(발 뜨는 자리)
- 비행 종료 프레임 → **착지 퍼프**(`EarthSlamSpikesAoeVFX` — 원래 착지에 어울리는 연출)

로 재생한다. 도약 연출이 한 곳에 모인다.

### 비행 구동

`KeyringSim.DismountPoint(start, startVel, end, camUp, recoilFrac, dipDistance, arcHeightFactor,
minArcHeight, launch, landingHeight, t01)` 를 그대로 호출한다.

- `startVel = 0` — 보스는 스윙 잔여 속도가 없다(하마는 드래그 플릭 속도를 흡수).
- `camUp` = 카메라 up. **아치 높이는 view 공간에서만 계산한다** — `BoardSpace.ToView` 가 sim-Y 를
  버리므로 sim 에 높이를 넣으면 평면화된다(기존 교훈).
- **시간 이징 없음(선형)** — drop-dismount 가 구현 중 정정한 계약. Out* 이징은 끝속도를 0으로 죽여
  내리찍는 임팩트가 물러진다. 착지 속도는 기하(끝접선)가 만든다.
- 진행 시계는 `Time.unscaledDeltaTime` 이 아니라 **배틀 도메인 델타**를 쓴다 — 손패 슬로모(0.3x) 중에는
  도약도 같이 느려져야 시뮬과 어긋나지 않는다(하마는 UI 조작이라 unscaled 였다. 여기가 다른 점).
- 매 프레임 `SetEnemyViewOverride(entity, p)`, 종료 시 `ClearEnemyViewOverride`.
- **abandon 조건**: 엔티티 소멸/사망 → 즉시 override clear 후 종료(공중에 시체 정지 방지).
- `OnDisable`/`OnDestroy`/매치 teardown → 진행 중 비행 전부 즉시 완결(override clear).

### 적 뷰 오버라이드

defender 쪽(`_defenderViewOverride`, `SyncMonoUnitViews` 최우선 분기)의 미러를 적 피드에 만든다.
훅 지점은 적 루프의 `world` 계산 직후 — Spine/Quad 두 분기 **앞**이라 한 곳만 고치면 둘 다 적용된다.

`BattleBridge` 는 여러 세션이 동시 편집하므로 상태·API·코루틴은 **신규 partial**
(`BattleBridge.BossLeap.cs`)에 두고, 공유 파일에는 소비 2줄과 큐 lifecycle 만 넣는다.

## rev 1 — 착지 슬램 (2026-07-29 사용자 지시)

비행만 붙이면 **내리찍는 연출인데 피해가 0** 이라 연출이 거짓말을 한다. 착지 시 자기중심
`slamTileRange` 타일에 `slamDamage` 를 준다(현 값 = 반경 1 / 50).

- `DcPayloadSpec` · `DcTriggerSlot` 에 `slamDamage`/`slamTileRange` **명시 필드 append**.
  SelfBlink 는 이미 `magnitude`(밀집 반경)·`tileRange`(링 상한)를 쓰고 있어 자유 스칼라가 없고,
  무엇보다 **데미지 경로는 이름으로 grep 돼야** 한다. append-only → 기존 카드는 0 = 슬램 없음.
- **피해 시점을 브리지가 소유한다.** 슬램의 "언제" 는 비행이 끝나는 프레임이고 그것을 아는 것은
  비행 구동자뿐이다. sim 은 이미 텔레포트를 끝냈다. 브리지는 ECS 창구이고 `DrainMeteorBarrageRequests`
  가 같은 방식으로 `ProjectileSpawnRequest` 를 내는 선례가 있다.
- `shooter = Entity.Null` (보스 AttackOutput 스냅샷 방지 — 슬램은 고정 피해), `owner = 보스`(킬 귀속),
  `targetFaction = Defender`, `flightTime = 0`.
- 슬램이 있으면 그 요청의 히트 이벤트가 VFX 도 그린다 → **퍼프를 따로 재생하지 않는다**(이중 재생 방지).
- 비행 시간 0.55 → **0.83초**(+50%, 사용자 지시). 씬에 직렬화되지 않은 SerializeField 라 코드 기본값이 실효값.

## 완료 기준

- compile 클린 · 기존 EditMode 전량 통과
- **Play 육안**: HP 50% 에서 보스가 **웅크렸다가 아치로 날아** 밀집 지점에 내리찍는다.
  순간이동 프레임 없음. 착지 스파이크 VFX 가 **뷰가 도착한 순간** 터진다(먼저 터지지 않는다)
- 비행 중 보스가 죽으면 공중에 멈추지 않고 즉시 정상 처리
- 손패를 열어 슬로모(0.3x) 중 도약이 시뮬과 같은 배율로 느려진다
- 나이트메어(도약 슬롯 없음)·방어유닛 재배치 비행 무회귀
- **착지 슬램**: 착지 순간 반경 1 안의 방어유닛이 50 피해를 입는다. 킬은 보스에 귀속된다.
  슬램 VFX 가 **한 번만** 재생된다(퍼프와 이중으로 겹치지 않는다)

- 확인: 2026-07-29 · EditMode 1575 중 1573 통과·실패 0·스킵 2(기존 `[Ignore]`) · 컴파일 에러 0.
  **Play 육안 검증은 미완** — 위 항목 참조.

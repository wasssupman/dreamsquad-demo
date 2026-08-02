# 3 — 이탈/강습 연출 채널

## 목적

sim 페이즈 전이를 뷰로 나른다: 발동 프레임 **상승 이탈**(화면 밖으로) → 이탈 동안 **뷰 숨김** →
착지 프레임 **수직 강하 + 착지**. 기존 `BossLeapVisualEvents` 를 재사용하지 않는 이유: 그 채널은
"출발→도착 아치" 를 나르는데 이 연출은 아치가 아니라 **이탈과 강하가 2초 떨어진 별개 사건**이다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/UltimateLeapVisualEvents.cs` — **신규** 채널
- `Assets/_Project/Scripts/Bridge/BattleBridge.UltimateLeap.cs` — **신규 partial** (BossLeap partial 선례
  — 공유 파일에는 lifecycle 호출만)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 채널 lifecycle 3점 세트 + 드레인 호출

## 구현

```csharp
// Combat→Bridge. kind 로 사건 구분 — 상승(Ascend, 발동 프레임)과 강하(Descend, 착지 프레임)는
// 2초 떨어진 별개 사건이라 한 이벤트에 못 싣는다.
public struct UltimateLeapVisualEvent
{
    public Entity entity;
    public byte kind;            // 0=Ascend, 1=Descend
    public float3 world;         // Ascend=이탈 위치 / Descend=착지 셀 중심
    public int dataIndex;        // 착지 VFX (Descend 만)
}
```

- **상승**: 이탈 위치에서 view 공간 위로 가속 상승(ease-in) 후 화면 밖에서 뷰 비활성.
  `_enemyViewOverride` 의 (simPos, viewHeight) 2축 분리 재사용 — 높이는 view 공간(flight-lift-feel
  의 lift 확대·그림자 반응이 공짜로 따라온다: 뜰수록 커지다 사라진다).
- **숨김 동안**: 오버라이드 키 유지 + 뷰 비활성. 오버헤드 HP 바는 뷰 앵커 기반이라 함께 사라진다
  (확인 항목). 착지 슬램의 VFX 는 기존 TileAoe 히트 경로가 그린다.
- **강하**: 착지 셀 직상방(화면 밖 높이)에서 수직 낙하(ease-in, 끝속도 최대) → 착지 프레임에
  오버라이드 해제 + `PlayLandingSquash`(flight-lift-feel 재사용).
- **teardown**: `_enemyViewOverride.Clear()` 가 비행을 끊는 기존 계약 그대로 — 강하 대기 중이던
  코루틴은 키 부재로 자진 종료. 채널 Dispose 는 3점 세트.
- 상승·강하 시간은 브리지 SerializeField(연출별 취향 — flight-lift-feel 노브 소유 구분 준수).
  단 **합이 예고 2초를 넘으면 안 된다** — 강하 시작 = 착지 프레임이므로 강하 시간만큼 뷰가 sim 보다
  늦게 도착한다. 슬램 VFX 는 뷰 도착에 맞춰야 하므로 강하 완료 시점에 발화(BossLeap 의 "착지 퍼프가
  뷰 도착보다 먼저 터지지 않는다" 계약 미러).

## 완료 기준

- compile 클린 · EditMode 무회귀 · 채널 lifecycle 3점 세트(생성/파괴/Dispose) 확인
- `unity-feature-wiring` 체크: 씬 저장 없이 재생 가능한지(신규 SerializeField 는 코드 기본값)
- (연출 Play 확인은 unit 5 통합 검증에서)

## 구현 중 확정

- 상승·강하를 **한 코루틴으로 잇지 않았다.** 상승이 끝나면 화면 밖 높이로 유지하며 sim 의 Descend
  신호를 기다린다 — 브리지가 2초를 복제하면 두 시계가 갈리기 때문이다(브리지는 예고 시간을 모른다).
  `_ultimateLeapAirborne` 집합이 "대기 중" 을 표현하고, 비우면 코루틴이 자진 종료한다.
- Descend 는 상승 기록이 없으면 무시한다 — 취소·teardown 뒤에 허공에서 유닛이 떨어지지 않게.
- 드레인은 `SyncMonoUnitViews` **앞**(BossLeap 드레인 옆). 뒤로 가면 이탈 프레임에 sim 좌표를,
  착지 프레임에 이미 텔레포트한 좌표를 각각 한 프레임씩 노출한다.
- 착지 스쿼시는 `flight-lift-feel` 의 `PlayLandingSquash` 재사용(0.14/0.06 — 일반 착지보다 세게).

## 검증 기록

- 2026-08-02 · EditMode 1809 중 1807 통과·실패 0 · compile 클린. 채널 lifecycle 3점 세트 배선 완료.

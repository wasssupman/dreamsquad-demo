# 1 — 발동 arm + 시퀀스 시스템

## 목적

체력 경계 발동 → 착지 셀 고정 → 2초 카운트다운 → 착지(텔레포트 + 슬램). sim 이 시퀀스를 소유한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — `UltimateLeap` payload 분기
- `Assets/_Project/Scripts/Battle/Combat/UltimateLeapSystem.cs` — **신규** ISystem (Combat)

## 구현

### 발동 (HealthThresholdSystem — 기존 SelfBlink 분기 옆)

- 착지 셀 해석은 **기존 체인 재사용**: `TryResolveBlinkDest`(밀집도 최대 셀 → 링 스냅). 실패
  (방어유닛 전멸 등) 시 skip — k 는 이미 전진(기존 규약).
- 성공 시 ECB 로 `UltimateLeapState`(remaining=slot.duration, landingCell, 슬램 값들) +
  `LeapFlight` 를 부착. 둘 다 Combat 소유라 소유 맥락 내 쓰기다.
- 이탈 신호를 `UltimateLeapVisualEvents` 에 enqueue (Ascend — unit 3).

### 시퀀스 (UltimateLeapSystem, `[UpdateAfter(HealthThresholdSystem)]`)

```
foreach (UltimateLeapState, WithNone<DeadTag>):
    state.remaining -= dt          // SystemAPI.Time.DeltaTime = Battle 도메인 (기존 sim 규약)
    if (state.remaining > 0) continue
    // 착지 프레임:
    //  1. BlinkRequestEventsSingleton enqueue — 기존 Combat→Movement seam 재사용 (신규 채널 0)
    //  2. 슬램: ProjectileSpawnRequest 캐리어 (SkyFall×TileAoe, flightTime 0,
    //     targetFaction Defender, owner=보스 — ResolveLanding 슬램과 동일 규약)
    //  3. UltimateLeapVisualEvents enqueue (Descend — unit 3)
    //  4. UltimateLeapState·LeapFlight 제거 (ECB)
```

- **abandon = teardown 뿐이다.** 계약 3(피해 완전 차단)으로 공중 사망이 없으므로 `DeadTag` 분기는
  방어적 가드다(오버킬 프레임 경합 대비 — 있으면 착지 없이 상태만 제거).
- 시스템 셸에 게임 의미를 넣지 않는다(계약 7c) — 타이머 감산과 만료 판정뿐이라 **추출할 순수 함수가
  없다**. `HealthThresholdEval` 처럼 분기 있는 계산이 생기는 순간(예: 다단 페이즈) 그때 추출한다.

## 완료 기준

- compile 클린 · EditMode 무회귀
- (에셋 배선 전이라 발동 불가 — unit 5 에서 Play 검증. 이 유닛은 시스템 등록·순서까지)
- `UltimateLeapSystem` 이 `BattleSimGroup` 에서 `HealthThresholdSystem` 뒤에 도는 것 확인

## 구현 중 확정

- `TryResolveBlinkDest` 에 `out int2 landingCell` 추가. 궁극기는 예고 타일을 그려야 해서 셀이
  필요한데, 월드→셀 역변환은 반올림 경계에서 원본과 갈릴 수 있다 — **예고와 실제 착지가 어긋나면
  이 스킬의 존재 이유가 무너지므로** 원본 셀을 그대로 나른다. SelfBlink 호출부는 `_` 로 버린다.
- 이탈 신호(Ascend) enqueue 는 **unit 3 으로 미뤘다** — 채널이 거기서 생긴다. 이 유닛까지는
  sim 만 돌아서, 배선 시 보스는 제자리에 보인 채 2초 뒤 착지 위치로 순간이동한다.
- 착지 실패(방어유닛 전멸)는 skip 이고 k 는 이미 전진이라 **재시도가 없다**(생존당 1회).
  응징할 밀집이 없는 상황이므로 수용.

## 검증 기록

- 2026-08-02 · EditMode 1809 중 1807 통과·실패 0 · compile 클린. 에셋 배선 전이라 발동 없음.

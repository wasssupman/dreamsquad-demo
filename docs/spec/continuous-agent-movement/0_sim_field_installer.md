# unit 0 — 판 단위 sim 필드 설치 코드 추출

## 목적

`BattleBridge` 에서 **판 단위 sim 필드 3종의 할당·해제**를 떼어내 `SimFieldInstaller` 로 옮긴다. 기능 변경이 아니라 **이 spec 이 이어서 편집할 코드의 사전 정리**다 — unit 1 이 `BuildFlowField` 안의 walkMask 를 손대므로, 먼저 옮겨두면 unit 1 의 diff 가 읽힌다.

**동작 불변이 이 unit 의 전부다.** 로직 개선·정리는 하지 않는다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Bridge/SimFieldInstaller.cs`
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

옮기는 것 (약 180줄):

| 현재 위치 | 내용 |
|---|---|
| `BuildFlowField()` `:792-889` | `FlowFieldSingleton` + `DefenderFieldSingleton` 생성 |
| `BuildPickupSpawnState()` `:936-969` | `PickupSpawnState` 생성 (Walk∪Place 후보 셀) |
| `TeardownPickupSpawnState()` `:971-983` | 위의 해제 |
| `TeardownFlowField()` `:1287-1321` | 3종 해제 (멱등) |
| 필드 `:384·387·390` | `_flowFieldSingleton` · `_defenderFieldSingleton` · `_pickupSpawnStateSingleton` → `SimFieldHandles` 하나 |

세 싱글턴을 함께 옮기는 이유: **라이프사이클 공유가 이미 명시적 계약**이다(`:389` "goal/defender field 와 동일 lifecycle", `:1319` "픽업 스폰 상태도 맵 field 와 동일 lifecycle"). 나눠 옮기면 그 계약이 두 파일에 걸친다.

## 구현

**`SimFieldHandles`** — 엔티티 핸들 3개를 담는 struct. `Reset()` 하나.

**`SimFieldInstaller`** — plain static class. 메서드 4개:

- `InstallNavFields(EntityManager, in GeneratedMap, float tileSize, float3 origin, ref SimFieldHandles)`
- `InstallPickupSpawnState(EntityManager, in GeneratedMap, uint pickupSeed, ref SimFieldHandles)`
- `Teardown(World, EntityManager, ref SimFieldHandles)`
- `TeardownPickupSpawnState(EntityManager, ref SimFieldHandles)`

`BattleBridge` 쪽은 껍데기만 남긴다 — 멱등성 계약(설치 전 teardown 선행)은 **호출 순서로 계속 보이게** 유지한다:

```
private void BuildFlowField()
{
    if (!_generatedMap.IsCreated || _em == null) return;
    TeardownFlowField();                      // CRITICAL #1 계약 유지
    SimFieldInstaller.InstallNavFields(_em, in _generatedMap, tileSize, _boardOrigin, ref _simFields);
}
```

읽기 지점은 기계적 치환뿐이다 — `_flowFieldSingleton` 외부 소비처는 2곳(`ComputeSpawnLateralOffset` `:991`, 경로 추적 `:1946`)이고 나머지 두 핸들은 외부 소비처가 없다.

**ECS 경계 (제약 1) 판정**: `SimFieldInstaller` 는 **MonoBehaviour 가 아니다.** 제약 1 은 "그 외 *MonoBehaviour* 에서 `EntityManager` 직접 호출 금지"이며, 이 클래스는 `BattleBridge` 만이 호출하는 plain static helper다. 창구는 여전히 브리지 하나다.

### 의도된 동작 변경 1건

`:587-591` 의 else 분기(월드 사망 시)가 현재 **3개 중 2개만** null 로 되돌린다 — `_pickupSpawnStateSingleton` 이 stale 하게 남는다. `SimFieldHandles.Reset()` 은 3개를 모두 되돌린다.

무해한 비대칭이지만(`TeardownPickupSpawnState` 가 `_em` 가드로 막고 있다) struct 로 접으면서 비대칭을 그대로 표현할 방법이 없고, stale 핸들을 남기는 쪽이 엄격히 더 나쁘다. **이 커밋의 유일한 의도적 델타이며 커밋 메시지에 명시한다.**

## 완료 기준

- [x] compile 통과 — Unity 콘솔 에러 0
- [x] `BattleBridge.cs` 감소 — 7377 → 7222 (**−155줄**), `SimFieldInstaller.cs` 220줄 신설
- [x] `TeardownFlowField` 호출처 4곳이 모두 새 경로 경유 (메서드 본문만 위임으로 교체, 호출부 무변경)
- [x] EditMode **1919 중 1917 통과 · 실패 0** (skip 2 = 기존 `[Ignore]`)
- [x] PlayMode **회귀 0 — 기준선 대조로 확인**: HEAD 기준선 15 실패 / 본 변경 13 실패. 본 변경의 실패 집합은 기준선의 **진부분집합**이며 신규 실패 없음. (차이 2건은 버프 배율 누적 계열 flaky — `DreamcatcherEffectTest.CardBuffs`, `DreamcatcherAttachRequirementE2ETest`)
- [x] **Play 육안 확인 통과** (2026-08-07 사용자) — 전투 진입 → 적 이동 정상 → 로비 복귀 → 재진입, 누수·멱등성 이상 없음

### 검증 기록

기준선 대조 절차: `git show HEAD:BattleBridge.cs` 로 원본 복원 + `SimFieldInstaller.cs` 임시 격리 → PlayMode 전량 → 복원 후 파일 해시 일치 확인. 워크트리는 원상 복구됨.

## 주의

`TeardownFlowField` 의 멱등성·누수 방지 계약은 주석에 **"CRITICAL #1 (Codex 2차 리뷰)"** 로 박혀 있다(`:790`). `AddComponentData` 는 컴포넌트가 이미 있으면 throw 하고, 기존 배열이 dispose 없이 덮이면 누수다. **추출 시 조건문·순서·예외 처리(`catch` 안의 부분 dispose)를 한 글자도 바꾸지 않는다.** 누수는 조용히 발생해 나중에 원인 추적이 오래 걸린다.

---

**완료 기준 확인**: 2026-08-07 · `942ca7f5`

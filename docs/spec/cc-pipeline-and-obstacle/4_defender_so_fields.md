# Defender SO Knockback / On-Place Push Fields

**작업 구분**: 4

## 목적

DefenderSO 에 넉백 / 배치 push 파라미터 5개 필드를 추가한다. 모두 default 0 → 기존 SO asset 영향 0. 실제 enqueue 로직은 다음 단위들 (5, 6) 에서.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/DefenderUnitData.cs` (실제 DefenderSO 파일명. 다른 이름이면 그 파일 수정)
- (필요 시) Modify: 해당 SO 데이터를 ECS 컴포넌트로 미러링하는 코드 — `DefenderRuntimeData` 류. 5개 필드를 미러 컴포넌트에도 추가하여 Burst 시스템에서 읽을 수 있게 한다.

## 추가 필드 (DefenderSO)

```csharp
[Header("Knockback (per attack)")]
public float knockbackDistance;   // world units. 0 = 비활성
public float knockbackDuration;   // 초. 짧을수록 강한 충격감

[Header("On-place Push")]
public float onPlacePushDistance; // world units. 0 = 비활성
public float onPlacePushDuration; // 초
public float onPlacePushRadius;   // world units. 디펜더 중심 반경 안 적이 대상
```

## 의미 + 단위

- `knockbackDistance`: 넉백 1회의 총 변위. velocity = `(direction × distance) / duration`.
- `direction` (knockback): defender → enemy 정규화 벡터 (적은 디펜더 *반대* 방향으로 밀림).
- `direction` (on-place push): defender → enemy 정규화 벡터 (방사형 밀어내기).

## ECS 미러 (필요 시)

- 디펜더 entity 의 `DefenderRuntimeData` (또는 동등 컴포넌트) 에 5개 필드 미러링.
- Combat 시스템은 ECS 컴포넌트만 읽음 (Burst 호환). ScriptableObject 직접 read 금지.
- SO → ECS 미러링 코드는 디펜더 spawn 시점 (`BattleBridge.SpawnDefender` 등) 에 1회 복사.

## Producer 와의 관계

- 본 단위에서는 *필드 추가만*.
- 기존 모든 SO asset 의 5개 필드는 0 으로 직렬화 → enqueue 분기 (`> 0`) 가 안 잡혀 동작 변화 0.

## 완료 기준

- 컴파일 통과.
- DefenderSO Inspector 에 두 헤더 + 5필드 노출.
- 기존 모든 defender asset 5필드 default 0.
- ECS 미러 (사용 시) 의 5필드도 0 으로 초기화됨.
- 런타임 동작 변화 0.
- 콘솔 에러/경고 0.

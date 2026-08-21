# unit 1 — 포신이 겨눈 쪽으로 돈다

## 목적

본능이 쏠 때 프랍이 미동도 없다 — 탄만 어디선가 튀어나온다. **포신만**(계약 5) 대상 쪽으로
돌려 「저게 나를 보고 있다」를 만든다.

신호는 **이미 도착해 있다.** `AttackSystem` 은 본능에도 `UnitAttackVisualEvent{attacker,
targetWorld}` 를 넣는데(공격자 전원 공통 경로), `BattleBridge.DrainUnitAttackVisualEvents` 안의
소비자 셋이 전부 본능을 모른다 — `spineUnitPool.NotifyAttack`(스파인 풀에 없음) ·
`_enemyTypeByEntity`(적 유닛 전용) · `FindDefenderData`(null → `continue`). **받는 사람이 없어서
조용히 흘러나간다.** 이 unit 은 그 자리에 수신자를 놓는다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/StructureTurretView.cs` (신규)
- `Assets/_Project/Prefabs/Structures/Instinct_{Ally,Enemy}.prefab` — 컴포넌트 부착 + 포신 참조
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `SpawnStructureViews` — 셀 → 프리젠터 사전 등록
  - `DrainUnitAttackVisualEvents` — 본능 수신 분기

## 구현

### 프리젠터 (뷰 소유)

```
StructureTurretView : MonoBehaviour
    [SerializeField] Transform barrel;      // cannon_barrel_* (프리팹에서 지정 — 이름 문자열 금지)
    [SerializeField] float turnSeconds;     // 기본 0.2 (제약 6 — 수치는 저작에서)
    public void AimAt(Vector3 viewDir)      // 목표 yaw 갱신
    Update()                                // 배틀 도메인 delta 로 현재 yaw → 목표 yaw 보간
```

- 목표 각 = **월드 Y 축 yaw**. 보드는 grid 가 월드 90°X 로 누운 XZ 평면이고 프랍은 월드 업라이트로
  서 있으므로, 판 위 방향은 곧 월드 XZ 벡터다.
- 최단호 보간(`Mathf.MoveTowardsAngle`) — 360° 를 가로지를 때 반대로 돌지 않게.
- 시간은 `TimeManager.Instance.DeltaTime(TimeDomain.Battle)`(계약 8).
- **포신 전방축 확인 필요**: `cannon_barrel_*` 메쉬의 전방이 로컬 +Z 가 아니면 프리팹 변형 안에
  중간 피벗 오브젝트를 한 겹 넣어 축을 맞춘다(코드에 오프셋 상수를 박지 않는다).

### 브리지 배선

- `SpawnStructureViews` 가 뷰를 세울 때 `cell → StructureTurretView` 사전에 담는다. 뷰는 **맵
  수명**, 엔티티는 **판 수명**이라 엔티티로 직접 잇지 않고 **셀**로 잇는다(`_structureRegistry`
  가 이미 (entity, cell, faction)을 들고 있다).
- `DrainUnitAttackVisualEvents` 에서 `attacker` 가 등록부의 본능이면 셀로 프리젠터를 찾아
  `AimAt(BoardSpace.ToView(targetWorld) - BoardSpace.ToView(attackerWorld))` 를 호출한다.
  - 이 분기는 반드시 `defData == null → continue` **앞**에 둔다 — 그 아래는 전부 방어유닛 전용이라
    적 본능이 거기까지 못 간다(회오리 VFX 분기가 같은 이유로 위에 있다).
  - 방향은 뷰 공간에서 구한다(계약 6). sim 벡터를 그대로 쓰면 엉뚱한 축으로 돈다.

### 순수 함수로 빼지 **않는** 이유 (제약 10 판정)

「방향 → yaw」는 퇴화 가드 한 줄 + `Mathf.Atan2(x, z)` 한 줄이고, 「한 스텝 회전」은
`Mathf.MoveTowardsAngle` 이 이미 최단호를 보장한다. 제약 10 의 추출 기준 셋 —
(a) 비자명 · (b) 호출처 2+ · (c) sim-critical 회귀 가치 — 에 **하나도 걸리지 않는다.**
호출처 하나뿐인 두 줄을 static 타입으로 빼면 제약 8(나중을 위한 추상 레이어)과 충돌한다.
따라서 프리젠터 안에 인라인하고, 검증은 Play 육안으로 한다(뷰 전용 변경).

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 2 lane 그린 (신규 테스트 없음 — 위 §판정 참조. 사전 실패 1건은 unit 0 문서 참조)
- [x] Duel 라이브 Play — `_structureTurretsByCell` 에 본능 4기가 등록되고(적 마음은 미등록),
      전투 중 **아군 본능만** 포신이 돈다(yaw 0 → 90.8°/80.5° → 52.3°/43.3°, 적 무리를 추적).
      적 본능은 yaw 0 유지 — 방어유닛을 하나도 안 놨으니 겨눌 대상이 없다(저작 마스크대로).
- [x] 받침·터렛 고정 — 회전은 `barrel` 트랜스폼 한 곳에만 쓴다
- [ ] 사용자 Play 체감 — 회전 속도(540°/s)가 「돌고 나서 쏜다」로 읽히는가

---

**확인 2026-08-20** — 구현 커밋 `92333fbd`. 라이브 Duel Play 로 검증(투트랙 리뷰 반영 후 재확인).

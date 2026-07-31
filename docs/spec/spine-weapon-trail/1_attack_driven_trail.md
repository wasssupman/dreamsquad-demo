# 1 — 공격 구동 배선 + 가시성 판정

## 목적

궤적을 실제 전투 경로에 물린다. **그리고 unit 0 이 답하지 못한 "화면에서 읽히는가"를 실전 시야에서
판정한다.** 이 unit 은 배선이자 **판정 게이트**다 — 여기서 안 읽히면 unit 2 는 튜닝이 아니라
설계 재선택(README 후속 후보의 절차 스윙 / 애니 교체)으로 간다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — SO 필드 2개
- `Assets/_Project/Scripts/Data/IDefenderSpineExtras.cs` — 접근자 2개
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 부착 + Start/Stop

씬 배선 없음(프리팹 참조를 SO 가 들고 있다) → `unity-feature-wiring` 대상 아님.

## 구현

### 1. opt-in 은 `IDefenderSpineExtras` 에 둔다

궤적은 방어 유닛 전용(README 계약 9)이고, `SpineUnitView` 는 적 스폰 시 `_defenderExtras` 가
null 이다. 이 인터페이스에 얹으면 **적 제외가 코드 분기 없이 자동으로 성립**한다
(`SpineCastAnchorBone` 이 이미 같은 자리에 산다).

| 필드 | 의미 |
|---|---|
| `weaponTrailPrefab` (GameObject) | null = 무궤적. 유무가 유일한 게이트 — id/kind 분기 금지 |
| `weaponTrailEndNormalized` (float 0~1) | 공격 애니 길이 대비 방출 종료 지점 |

두 번째 필드가 필요한 이유: 방출은 **스윙 구간에만** 걸어야 한다. `Attack3` 는 전체 0.867초 중
0~0.267초만 스윙이고 나머지는 복귀다. 끝까지 방출하면 복귀 동작까지 궤적이 따라붙어 "칼을 되돌리는
자국"이 남는다. 0.267/0.867 ≈ **0.31 이 초기값**. 애니마다 다르므로 상수로 박지 않는다(제약 6).

### 2. 스폰 시 부착

`SpineUnitView.Spawn` 에서 스켈레톤 `Initialize` **이후에** 붙인다(본 조회가 skeleton 을 탄다):

1. 프리팹 null 이면 skip
2. `Instantiate(prefab, transform)` — 유닛의 자식이어야 틸트 빌보드 평면을 상속한다(README 계약 5)
3. `BoneFollower.skeletonRenderer = _skeleton` → `Initialize()` 직접 호출
   (프리팹의 `initializeOnAwake` 가 false라 우리가 부르기 전엔 안 붙는다 — unit 0 계약)
4. 컴포넌트 참조를 필드에 보관

### 3. 공격에 물리기

`PlayAttack` 안에서 `StartTrail()`. 종료는 `entry` 로 계산한 실시간 길이 뒤:

```
stopAfter = Animation.Duration * weaponTrailEndNormalized / entry.TimeScale
```

`entry.TimeScale` 을 나누는 이유는 `PlayAttack` 이 공격 주기에 맞춰 애니를 압축 재생하기
때문이다(≥1). 이걸 빼면 공속이 빠른 유닛에서 방출이 스윙보다 오래 남는다.

시간 기준은 `Time.time` — 궤적 자체가 그 위에서 도는 것과 맞춘다(README 계약 7). 별도 시간
배선을 만들지 않는다.

### 4. 정리

리그가 유닛의 자식이라 유닛 파괴 시 같이 죽고 `OnDestroy → DestroyRuntimeLayers` 가 씬 루트
레이어를 회수한다. `Kill()` 경로에서 잔존이 없는지 확인만 한다(아래 완료 기준).

## 완료 기준

- compile, 콘솔 에러 0
- **기능**: 근접 디펜더 1기 배치 → 공격 시 궤적 · 프리팹 미할당 유닛(원거리)은 무궤적 · 적도 무궤적
- **방출 창**: 궤적이 스윙에서 끝나고 복귀 동작을 따라가지 않는다

### 가시성 판정 (이 unit 의 핵심)

실제 배틀 카메라로 판정한다. unit 0 의 정면 직교 뷰는 이 질문에 답하지 못한다.

- 조건: `Billboard(Tilted, 45°)` 고정 평면 + 배틀 카메라 pitch + `tilemapCharacterScale 0.504`
- 판정: 보드 위 다른 유닛·타일 사이에서 궤적이 **공격 동작으로 읽히는가**.
  `Attack3` 는 세로 내려찍기라 호가 단축되는 축에 눕는다 — 이게 얼마나 먹는지가 관건
- 결과를 README "unit 1 로 이월된 검증" 항목에 반영하고, 통과/불통과에 따라 unit 2 성격을 확정

### 이월 검증 3건 (README 참조)

- **카메라 이동 중 박제**: 궤적 수명(0.2초) 안에 `CameraDirector` 가 카메라를 움직일 때
  스프라이트와 어긋나는지
- **실행 순서**: `BoneFollower.LateUpdate` ↔ `HS_SwordMeshTrail.LateUpdate` 1프레임 지연이
  눈에 띄는지. 띄면 Script Execution Order 고정
- **레이어 회수**: 유닛 사망 · 매치 종료 후 **프레임을 넘겨** `Generated Mesh Trail` 잔존 0 확인
  (unit 0 정리 때 같은 프레임엔 1개가 남아 보였다 — 지연 파괴로 추정되나 확증 안 됨)

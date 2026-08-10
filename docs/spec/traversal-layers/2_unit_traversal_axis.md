# unit 2 — 유닛 통행 층 축 (행동 변화 0)

## 목적

**유닛이 «나는 어느 층을 지날 수 있나»를 갖는다.** 값이 SO → 엔티티로 흐르는 것까지가 이 unit 이고, 그걸 **읽는 건 unit 3** 이다.

## 착수 전 조사 (계약을 쓰기 전에 확인한 것)

rev 3 §0 의 규칙 — «데이터를 쓰기 전에 출처와 소비처를 전수한다». 결과:

| | 사실 | 계획과 달랐던 점 |
|---|---|---|
| **출처** | 순찰 방어유닛 = `DefenderUnitData`, 적 = **`AttackUnitData`** — **SO 타입이 둘이다** | 계획은 SO 하나를 가정했다. `DefenderUnitData` 엔 `placementLayers` 가 있고 `AttackUnitData` 엔 층 개념이 아예 없다 |
| **주입 지점** | `BattleBridge` 2곳 — `CreatePatrolEntity:6173`(순찰 방어유닛) · `SpawnUnit:7546`(적) | 앞선 rev 들이 이 둘의 줄번호를 서로 바꿔 적었었다 |
| **소비처** | `PatrolFieldSystem` 의 `fullMask` **하나**. `:49-50` 에서 **엔티티 루프 밖·프레임당 1회** 만들고 루프 안 2곳(`FillAreaMask:66` · `StepDir:73`)에서 쓴다 | 유닛별로 달라지면 **그 hoist 가 깨진다** — unit 3 이 S 가 아니라 S~M 이다 |

## 설계 판정 (렌즈 4개)

**① ECS 가 필요한가 — 필요하다, 단 새 컴포넌트는 아니다.**
값은 SO 에서 오고 sim 은 SO 를 못 읽는다(경계) → 스폰 시 주입이 유일한 경로다. 그런데 `PathFollowState` 가 **이미 움직이는 모든 주체에** 붙어 있고 `speed`/`radius`/`holdingGround` 를 담고 있다 → **byte 하나 얹는다.**

**② 로직-아키텍처 분리 — 추출할 로직이 없다.**
판정식 `(셀 & 유닛) != 0` 은 이미 순수 함수다(`TraversalSlots.FillWalkMask`). `EffectiveTraversalLayers` 폴백은 한 줄 프로퍼티로 `EffectivePlacementLayers` 와 동형 — 별도 함수로 빼면 제약 10 후단의 과잉 추출이다.

**③ 불필요한 설계 — 신규는 SO 필드 2개 + 컴포넌트 필드 1개. 그 외 0.**
새 enum ✗ · 새 컴포넌트 ✗ · 새 순수 함수 ✗ · 새 헬퍼 ✗ · 로스터 수집 ✗.

**④ 출처·소비처 — 위 표.**

## 변경 대상

- `Data/AttackUnitData.cs` · `Data/DefenderUnitData.cs` — `traversalLayers` + `EffectiveTraversalLayers`
- `Battle/Movement/PathFollowState.cs` — `traversalLayers` (byte)
- `Bridge/BattleBridge.cs` — 스폰 2곳에서 주입
- `Tests/EditMode/TraversalLayerAxisTests.cs` (신규)

## 이름이 거짓말하는 것에 대하여

통행 층에 **`PlacementLayer` 타입을 쓴다.** 판정이 «(셀 층 & 유닛 층) != 0» 이라 **셀과 같은 비트 공간**이어야 하고, 셀 층은 `PlacementLayers.Derive(tiles)` 가 만들기 때문이다. 같은 비트를 가진 병렬 enum 은 순수 중복이다(제약 8).

리네임(`PlacementLayer` → `CellLayer`)은 참조가 40곳을 넘어 **후속 후보**로 둔다. 대신 두 SO 와 컴포넌트 주석에 «배치와 통행은 다른 축»을 명시했다 — README §0 의 회귀가 정확히 이 혼동이었다.

## ⚠ 폴백을 «전원 `Path`» 로 못박는 이유

rev 2 는 «방어유닛 = 자기 `placementLayers`» 였다. **그대로 두면 unit 3 이 판을 바꾼다.**

순찰 소환물은 `placementLayers` 가 **이니셜라이저 잔여값** `Ground` 인데 실제로는 **Walk 층에서 돈다** — 앵커가 `TryGetNearestWalkCell` 로 Walk 셀에 스냅되기 때문이다. `Ground` 로 폴백하면 앵커가 자기 마스크 밖 → 영역 마스크 전부 0 → **순찰병이 굳는다.**

«방어유닛 = `placementLayers`» 는 방어유닛을 실제로 움직이는 별도 spec 의 몫이다(README §5).

## 완료 기준

- [x] compile 에러 0 · EditMode **2022 중 2019 통과 · 실패 0**
- [x] **기존 테스트 기대값 갱신 0건** — 늘어난 4건은 이 unit 이 추가한 것
- [x] 신규 4건: 적 폴백 = `Path` / **방어유닛 폴백 = `Path`(자기 `placementLayers` 아님)** / 저작이 폴백을 이긴다 / **두 축이 독립**
- [x] 소비자 0 — `traversalLayers` 를 읽는 시스템이 아직 없다(값이 흐르기만 한다)

---

**완료 기준 확인**: 2026-08-09 · EditMode 2022 중 2019 통과 · 실패 0 · 행동 변화 0

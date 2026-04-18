# Phase 8 — 방어 유닛 Spine 적용 (ECS + MonoBehaviour 하이브리드)

> Phase 5에서 들어온 Billboard 렌더는 "위치와 종류만 식별되는" 최소 비주얼이다. Phase 8은 방어 유닛을 **Spine 2D 스켈레톤**으로 교체하여 유닛별 외형 차별화 + 공격/사망 애니메이션 추가 + 타겟 방향 플립을 구현한다. ECS 시뮬레이션은 그대로, 렌더만 하이브리드로 전환한다.

---

## 1. 목표

- 방어 유닛 10종이 `player-main.skel`의 **스킨(skin)** 으로 시각적으로 구분된다.
- ECS 엔티티(위치/전투 로직)와 Spine `SkeletonAnimation` GameObject(렌더)를 **1:1 하이브리드**로 묶는다.
- 방어 유닛의 **idle / attack / die** 상태 전환이 ECS 이벤트에 반응해 자동 재생된다.
- 방어 유닛의 **facing(flipX)** 이 가장 가까운 타겟 방향으로 자동 전환된다.

### 비목표

- 공격 유닛(적) Spine 전환 — 현행 Billboard 유지. 이후 Phase로 미룬다.
- 커스텀 스킨 합성(슬롯별 파트 스왑) — player-main의 **기존 skin만** 사용.
- 스킬/VFX 애니메이션 — 기존 ECS 경고 링/프로시저럴 유지.
- 사운드 — 미포함.
- Spine Timeline 연동, IK 조작, 새 애니메이션 제작 — 전부 제외.

---

## 2. 확정된 결정 (자율 판단)

| # | 항목 | 결정 | 근거 |
|---|---|---|---|
| D1 | 스킨 매핑 | `DefenderUnitData.spineSkinName` 필드 추가, Inspector에서 사용자 지정 | 실제 skin 이름은 Unity 에디터로만 확인 가능 |
| D2 | 애니메이션 상태 | idle / attack / die 3-state | 정적 방어 유닛이므로 walk/aim 불필요 |
| D3 | 하이브리드 구조 | Spine GameObject 풀 + LateUpdate 동기화 | Hybrid Renderer 경로보다 단순, 프로토타입 스코프 |
| D4 | 공격 유닛 | Phase 8에서 제외 | 스코프 폭주 방지 |
| D5 | Facing | flipX로 타겟 방향 자동 전환 | 시각적으로 즉시 읽힘 |
| D6 | 사망 처리 | die 애니메이션 종료 후 destroy | 몰입도 ↑ |
| D7 | 체력바 / 기존 VFX | 그대로 유지 | 스코프 외 |
| D8 | 렌더 위치 | 기존 defender spawn 위치 그대로 (타일 중앙) | 호환성 |
| D9 | Skel 파싱 | spine-unity SkeletonDataAsset 사용 | 표준 파이프라인 |
| D10 | 애니메이션 이벤트 | Spine의 `AnimationState.Complete` 리스너로 die 종료 감지 | API 일관성 |

---

## 3. 아키텍처 — 하이브리드 브리지

### 3.1 ECS → GameObject 1:1 링크

- **소유 위치**: MonoBehaviour 레이어. `BattleBridge`가 GameObject 인스턴스를 추적한다.
- **매핑 테이블**: 기존 `Dictionary<Vector2Int, DefenderBinding>` 옆에 `Dictionary<Entity, SpineDefenderView>` 추가.
- **스폰**: `PlaceDefenderAs` 성공 경로에서 ECS 엔티티 생성 + `SpineDefenderView.Spawn(unitData, entity, worldPos)` 호출.
- **제거**: `DefenderDeathEventsSingleton`의 NativeQueue 드레인 시 `SpineDefenderView.Kill()` 호출 → die 애니 재생 → 콜백으로 `Destroy(go)`.

### 3.2 Transform 동기화

- **위치**: 정적 유닛이라 LateUpdate 없이 Spawn 시 1회 Position copy면 충분. 단 Knockback/이동 스킬이 추후 추가되면 LateUpdate가 필요. Phase 8은 **Spawn 시 1회** + **사망 시 즉시 제거** 로 단순화.
- **회전**: Spine 스켈레톤은 flipX만 전환. Transform rotation 건드리지 않음.

### 3.3 ECS 이벤트 → Spine 애니메이션 트리거

- **idle**: Spawn 후 기본 재생.
- **attack**: `ProjectileSpawnRequest` 또는 기존 `IncomingDamage append` 직전에 `SpineDefenderView.PlayAttackOnce()` 호출. BattleBridge의 `DrainProjectileSpawnRequests()` 드레인 루프에서 defender entity ID로 lookup → view에 트리거.
- **die**: `DefenderDeathEventsSingleton` 드레인 시 `view.Kill()`.
- **facing**: AttackSystem이 가장 가까운 attacker를 찾을 때 그 방향을 `TargetFacing` 컴포넌트에 기록하거나, 매 프레임 `SpineDefenderView.Update()` 가 자기 전방에 있는 가장 가까운 적을 읽어 flipX 결정. 간단한 쪽은 **view가 매 프레임 가까운 AttackUnit의 월드 X 좌표를 읽어 flipX 판정**.

---

## 4. 파일 구조

```
Assets/_Project/Scripts/Presentation/
  SpineDefenderView.cs        (신규) — MonoBehaviour, 1 per defender
  SpineDefenderPool.cs        (신규) — 생성/반환 관리, GameManager가 hold
```

- 새 폴더 `Presentation/` 은 비-ECS 뷰 계층을 모은다. 기존 MonoBehaviour들이 여러 곳에 흩어진 상태라 Phase 8 시점에 새 맥락으로 **시각 렌더 전용**을 분리한다.
- ECS Component는 **추가하지 않는다** — 모든 상태는 기존 `DefenderUnitTag`, `LocalTransform`, `Health`, `AttackState` 에서 읽는다.

---

## 5. `DefenderUnitData` 확장

```csharp
// 추가되는 필드
[Header("Phase 8 — Spine")]
public string spineSkinName;              // 예: "Skin/Scout" — player-main.skel에 존재하는 스킨 이름
public SkeletonDataAsset skeletonDataAsset; // 선택. 비우면 GameManager.defaultSkeletonData fallback
public string idleAnimation = "idle";
public string attackAnimation = "attack";
public string deathAnimation = "die";
```

- SO 마이그레이션은 **필드 추가만** — 기본값이 비어 있어도 `spineSkinName == null` 조건으로 Spine 렌더를 skip하고 기존 billboard fallback.

---

## 6. 기존 Billboard와 공존

- `PlaceDefenderAs` 는 여전히 billboard 렌더 설정을 수행한다. `SpineDefenderView`가 뜨면 billboard MeshRenderer를 `enabled = false` 로 끄거나, 아예 Spine 생성 시 billboard 생성 분기를 건너뛴다.
- fallback: `spineSkinName` 또는 `skeletonDataAsset` 이 없는 유닛은 **billboard 그대로**. 점진 전환 가능.

---

## 7. Spine 설정 (사용자 에디터 작업)

Phase 8 구현 후 사용자가 Unity에서 해야 하는 설정:

1. **SkeletonDataAsset 생성**: `Assets/_Project/Characters/player-main.skel` 선택 → Right-click → Create → Spine → SkeletonDataAsset (또는 spine-unity 자동 생성).
2. **Atlas Asset 생성**: `player-main.atlas` → Create → Spine → SpineAtlasAsset.
3. **GameManager에 defaultSkeletonData 할당** (모든 defender가 같은 skel을 공유).
4. **각 DefenderUnitData SO에 `spineSkinName` 입력** — 실제 skin 이름 10개 (사용자가 player-main.skel에서 확인).

---

## 8. 작업 분해 — P8-NN

### 8.1 데이터

- [ ] P8-01 — `DefenderUnitData` 에 Spine 필드 추가 (skinName + skeletonDataAsset + anim 이름 3개)

### 8.2 하이브리드 런타임

- [ ] P8-02 — `Scripts/Presentation/SpineDefenderView.cs` — SkeletonAnimation wrapping. API: `Spawn(unitData, entity, worldPos)` / `PlayAttack()` / `Kill()` / `Dispose()`.
- [ ] P8-03 — `Scripts/Presentation/SpineDefenderPool.cs` — View 생성/추적. 씬에 하나만 존재.
- [ ] P8-04 — BattleBridge: `PlaceDefenderAs` 성공 시 SpineDefenderPool 통해 View 생성, `spineSkinName` 없으면 기존 billboard 유지.
- [ ] P8-05 — BattleBridge: `DrainProjectileSpawnRequests` / `DrainDefenderDeathEvents` 에서 해당 entity의 View 찾아 공격/사망 트리거.

### 8.3 Facing

- [ ] P8-06 — `SpineDefenderView.Update` 에서 가장 가까운 AttackUnit 월드 X 좌표 기반 flipX 전환. EntityManager 직접 접근 금지 — BattleBridge에 `TryGetNearestAttackerX(worldPos, out float x)` 퍼블릭 헬퍼 추가.

### 8.4 사망 후처리

- [ ] P8-07 — Die 애니메이션 `Complete` 리스너에서 `Destroy(gameObject)`. 체력바도 해당 시점 제거.

### 8.5 Fallback & 공존

- [ ] P8-08 — `spineSkinName == null || empty` 또는 `skeletonDataAsset == null` 일 때 billboard 경로. 사용자가 점진 전환 가능.

### 8.6 로그

- [ ] P8-09 — `BattleLogEntry.phase = "phase8"`. 로그 스키마 변경 없음 (비주얼 전용).

### 8.7 검증

- [ ] P8-10 — PlayMode: Spine 설치된 상태에서 1종 skin 적용한 defender 1기 배치 → idle 재생, 공격 시 attack 한 번, 죽으면 die 재생 후 제거. 사용자가 직접 확인.
- [ ] P8-11 — 10종 defender SO에 실제 skin 이름 할당 (사용자 Unity 에디터 작업).

---

## 9. 종료 조건

- defender 10종이 Spine 스킨으로 시각 구분된다.
- idle 재생, 공격 시 attack, 사망 시 die → destroy.
- 가장 가까운 적 방향으로 flipX 자동.
- 기존 billboard 경로가 fallback으로 살아 있어 skin 없는 defender도 배치 가능.
- 컴파일 에러 0, EditMode 테스트 기존 전부 pass.
- JSON 로그 phase="phase8".

---

## 10. TRD 금지 패턴 재적용

- **싱글톤 금지** — `SpineDefenderPool`은 비싱글톤, GameManager가 ref 보유.
- **수치 하드코딩 금지** — skin/anim 이름 전부 SO.
- **새 맥락 폴더 제한** — `Presentation/` 은 기존 `Scripts/` 최상위 MonoBehaviour 분산을 정리하는 용도지, ECS 맥락 아님. **ECS 맥락은 여전히 Units/Movement/Combat/Effects 4개**.
- **맥락 경계** — SpineDefenderView는 ECS 상태를 **읽기만** 한다. 쓰기 X.
- **ECS 창구 유지** — SpineDefenderView가 EntityManager 직접 호출 금지. BattleBridge 퍼블릭 헬퍼 경유 (Facing용 `TryGetNearestAttackerX` 등).
- **Manager 싱글톤은 GameManager만** — SpineDefenderPool은 MonoBehaviour, Instance 없음.

---

## 11. 리스크 & 미해결

- **skin 이름 불확실성**: 사용자가 `player-main.skel` 의 실제 스킨 이름을 알려주기 전까지는 D1 결정에 따라 빈 필드로 둔다. Phase 8 구현은 필드/플로우 완성, 실제 스킨 할당은 사용자 작업.
- **Editor-only 의존**: SkeletonDataAsset은 빌드 가능하지만 초기 파싱은 에디터에서 발생. 빌드 테스트는 Phase 8 범위 밖.
- **Facing vs 체력바 겹침**: 체력바는 현재 defender 위로 렌더. flipX 영향 없음 확인.
- **다중 스킨 합성(예: 옷 + 얼굴)**: Phase 8 범위 밖. skin 1종만 설정.

---

**문서 버전**: v0.1 (스펙 확정, 구현 미시작)
**결정 출처**: 사용자 "ㄱㄱ" 위임 → 에이전트 자율 결정 D1~D10

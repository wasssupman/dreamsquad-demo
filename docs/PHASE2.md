# Phase 2 — 스킬 (H2 스킬 축 조기 검증)

> 본 문서는 `PRD.md`, `TRD.md`, `PHASE0.md`, `PHASE1.md`, `phase0-decisions.md`, `phase1-decisions.md`를 전제로 작성되었다. Phase 0·1에서 확립된 아키텍처 경계, 맥락 분리, 추상화 규칙, 금지 패턴은 Phase 2에서도 그대로 유지된다. 본 문서는 **Phase 2의 What과 Done만 정의**한다. How(구체 스킬 수치, UI 레이아웃, Effects 맥락 구체화 방식 등)는 에이전트 구현 재량이다.

---

## 0. Phase 2의 존재 이유

### 검증 목표 (H2 부분 검증)

> PRD §0의 **H2 (3분 루프의 긴장감)** 는 "코스트 제약 + 3분 타이머 + 배치 + 스킬" 결정 전체의 긴장감을 묻는 복합 가설이다. Phase 2는 이 중 **스킬 축**만 선제적으로 검증한다. 코스트·3분 타이머는 Phase 4에서 합류한다. **H2의 전면 검증은 Phase 4 완료 후에만 가능**하다.

Phase 2의 구체 질문:

> **"같은 유닛 드래프트 + 같은 공격 패턴 조건에서, 플레이어는 반복 플레이를 통해 스킬 사용 '타이밍'과 '대상 선택'을 수렴적으로 개선하는가?"**

스킬이 의미 있는 결정 축이 되지 못하면, 게임은 배치 최적화 퍼즐로 수렴하고 Phase 4에서 코스트·타이머를 더해도 긴장감 밀도가 부족해질 위험이 있다. Phase 2는 이를 조기에 걸러낸다.

### Phase 2가 하는 것 / 안 하는 것

**Phase 2가 하는 것:**
- **플레이어 능동 스킬 3종** 도입 — 판마다 **동일한 고정 3종**을 사용한다 (§3.3 측정 전제).
- 스킬은 **쿨다운 기반** 단일 메커니즘.
- **ECS Effects 맥락 활성화** — 지금까지 자리만 있던 `Battle/Effects/` 폴더에 Component/System 처음 진입.
- **스킬 UI** — 화면 하단 SkillBar 3슬롯 + 쿨다운 시각화 + 조준 모드.
- 로깅 확장 — `BattleLogEntry`에 `SkillRecord { loadout, usages }` 추가.

**Phase 2가 하지 않는 것:**
- **스킬 드래프트 / 스킬 선택 UI** — 스킬을 학습 축에 추가하면 H1(픽 수렴)·Phase 2 측정이 교란됨. Phase 3 이후 재검토.
- **유닛별 Passive 고유 스킬 / 배치 시 효과 / 인접 시너지** — 전부 Phase 3 영역.
- **코스트 시스템 / 판당 사용 횟수 제한** — Phase 4. Phase 2 쿨다운만 허용.
- **3분 타이머 / 봇 / 스코어 비교** — Phase 4.
- **스킬 레벨 / 업그레이드 / 재장전 시너지** — 단일 레벨 고정 효과.
- **Heal, AoE 즉발 대미지** — Units 맥락 경계 문제 회피를 위해 Phase 2에서 제외. Slow/DamageBoost 두 축만(§2.1 결정).

---

## 1. Phase 2의 게임 흐름

```
[빌드 실행]
  ↓
[맵 + 경로 + 배치 타일 표시 (Phase 0 동일)]
  ↓
[드래프트 UI: 10종 → 7종 픽 (Phase 1 동일)]
  ↓
[픽 확정 → BattleBridge에 defenderPool + skillLoadout(고정 3종) 동시 주입]
  ↓
[실시간 디펜스: 배치 = picked 7종 중 랜덤 / 스킬 = 하단 SkillBar 탭]
  ↓
[스킬 효과 적용 ↔ 적에게 영향 ↔ 쿨다운 회전]
  ↓
[결과 화면 (VICTORY / DEFEAT)]
  ↓
[다시 시작 | 다른 픽으로 재도전]  ← Phase 1 분기 그대로
```

---

## 2. Phase 2 콘텐츠 스펙

### 2.1 스킬 풀 (고정 3종)

Phase 2는 **고정 3종**을 전 판 동일하게 사용한다 (§3.3 통계 안정성 요구).

3종은 **Effects 맥락에서 깔끔하게 표현 가능한 효과**만 선정한다. 즉각 데미지(Meteor), 체력 회복(Heal)은 Units 맥락(Health, IncomingDamage) 쓰기 권한 경계 문제를 만들므로 **Phase 2에서 제외**한다. Phase 3 이후 Units 맥락이 `HealEvent` 같은 공식 진입점을 갖춘 뒤 재도입.

**선정 결과 (확정)**:

| id | 이름 | 타깃팅 | 효과 | 지속 | 쿨다운 |
|---|---|---|---|---|---|
| `slow_field` | Slow Field | 타일 좌표(반경 2.0) | 범위 내 적 이동속도 ×0.6 | 5s | 20s |
| `power_surge` | Power Surge | 방어 유닛 1개 | 대상 방어 유닛 발사 데미지 ×2.0 | 8s | 30s |
| `rapid_fire` | Rapid Fire | 방어 유닛 1개 | 대상 방어 유닛 쿨다운 감소 ×0.5 | 6s | 25s |

- 세 스킬 모두 **기존 Component(PathFollowState, AttackState)를 직접 쓰지 않는다**. 대신 신규 Effects Component를 부여하고, Movement/Combat 시스템이 **읽기 전용**으로 참조한다(§2.2).
- 수치 구체값은 확정이나, 에이전트가 튜닝 필요 시 `SkillData` SO에서 조정하고 `phase2-decisions.md`에 이유 기록.
- 신규 스킬 추가는 Phase 2 범위 외 — "3종 고정" 원칙 유지.

**SkillData SO 필드 (확정)**:

```csharp
public enum SkillEffectType { SlowField, PowerSurge, RapidFire }
public enum SkillTargetType { TilePoint, DefenderUnit }

[CreateAssetMenu(fileName = "Skill", menuName = "Wassup/Skill", order = 12)]
public class SkillData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public SkillEffectType effect;
    public SkillTargetType target;
    public float range;           // tile radius for TilePoint, 0 for DefenderUnit
    public float magnitude;       // multiplier value (e.g., 0.6 for SlowField)
    public float durationSec;
    public float cooldownSec;
    public Color uiTint;          // 슬롯 식별용 색
}
```

### 2.2 ECS Effects 맥락 활성화

`Assets/_Project/Scripts/Battle/Effects/` 아래에 본 Phase에서 처음 도입하는 타입:

- `SlowEffect` (IComponentData) — `float remaining; float multiplier;` (적 엔티티에 부여)
- `DamageBoost` (IComponentData) — `float remaining; float multiplier;` (방어 엔티티에 부여)
- `CooldownReduction` (IComponentData) — `float remaining; float multiplier;` (방어 엔티티에 부여)
- `EffectTickSystem` (ISystem, BurstCompile) — 매 프레임 `remaining -= DeltaTime`, 0 이하가 되면 ECB로 해당 Component 제거

**맥락 경계 규칙 (TRD 2.5.2 구체 해석, Phase 2 확정)**:

- Effects Component 3종의 **쓰기(Add/Update/Remove)는 오직 Effects 맥락**이 수행 — 즉 `EffectTickSystem`(만료 시 Remove) + `BattleBridge.CastSkill`이 호출하는 전용 Effects 헬퍼 경로(부여 시 Add/Update). BattleBridge에서 직접 AddComponent 하더라도 **그 코드는 `Battle/Effects/EffectSpawner` static 유틸 메서드를 경유**하는 것으로 경계를 명시한다.
- `MovementSystem`은 `SlowEffect`를 **읽기만** 한다. 실제 이동 속도 계산:  
  `effectiveSpeed = pathFollow.speed * (HasComponent<SlowEffect>(e) ? slow.multiplier : 1f)`.  
  `PathFollowState.speed` 필드 자체는 수정하지 않는다(Movement가 계속 소유).
- `AttackSystem`은 `DamageBoost`와 `CooldownReduction`을 **읽기만** 한다. 데미지 방출 시:  
  `emittedDamage = attackState.damage * (HasComponent<DamageBoost>(e) ? boost.multiplier : 1f)`.  
  `attackState.damage` 필드는 **수정하지 않는다**(Combat이 계속 소유, 원본 불변).  
  쿨다운 적용:  
  `attackState.cooldownRemaining += cooldownDuration * (HasComponent<CooldownReduction>(e) ? cr.multiplier : 1f)`.  
  쿨다운은 원래 AttackSystem이 소유하므로 쓰기 계속 가능, Effects의 multiplier만 읽어 반영.
- Effects 쓰기가 Movement/Combat를 필요로 하는 경우(희귀)에는 본 Phase에서는 설계하지 않는다. 발생 시 stop + 질문(CLAUDE.md 자가 점검).

### 2.3 스킬 발동 진입점 (BattleBridge 확장)

- **새 메서드**: `BattleBridge.CastSkill(SkillData skill, object payload)` — `payload`는 `SkillTargetType`에 따라 `Vector2Int`(TilePoint) 또는 `Entity` 또는 방어 유닛 식별용 `Vector2Int cell`.
- 타입 안전을 위해 2개 오버로드로 분리 권장:
  - `CastSkillAtTile(SkillData skill, Vector2Int tile)`  
  - `CastSkillOnDefender(SkillData skill, Vector2Int defenderTile)`
- 쿨다운 체크는 BattleBridge 내부 `CastSkill*` 메서드 진입부에서 `_skillRuntime.IsReady(skill)` 호출. 미준비 시 `false` 반환 + UI가 시각 피드백.
- 영향받은 엔티티 수는 반환값 또는 out 파라미터로 제공(로그 `affected_count`에 쓰임).
- 내부 구현은 `SkillEffectType` switch — 인터페이스/상속 계층 금지(CLAUDE.md §3).

### 2.4 쿨다운 런타임 상태 (MonoBehaviour, 비-싱글톤)

- 신규 MonoBehaviour `SkillRuntime` — GameManager의 자식 GameObject로 배치. `public static Instance` 금지. GameManager에 `[SerializeField] SkillRuntime skillRuntime` 참조 보관.
- 내부 상태: `Dictionary<SkillData, float> _cooldownRemaining`.
- 공개 API: `IsReady(SkillData)`, `Consume(SkillData)` (쿨다운 초기화), `GetRemainingNormalized(SkillData)` (UI용 0~1).
- Update에서 모든 엔트리 `-= Time.deltaTime`, 0 하한. `_running=false`이면 tick 정지(Restart/Redraft 시 기존 잔여 쿨다운 전부 0으로 초기화).
- BattleBridge는 `CastSkill` 진입부에서 `skillRuntime.IsReady(...)` 호출 후 Consume. SkillRuntime이 BattleBridge를 역참조하지 않는다(단방향).

### 2.5 스킬 UI (SkillBar)

- 신규 MonoBehaviour `UI/SkillBar` — `DraftView`와 같은 **런타임 빌드 패턴**: 자체 Canvas를 AddComponent로 부착하여 프리팹 자산 0건 원칙 유지.
- **전용 Screen Space Overlay Canvas 1개 신규 생성**(기존 ResultCanvas와 별도, sortingOrder 낮춤).
- 하단 중앙 가로 슬롯 3개 — `SkillData.uiTint` 배경 + displayName + 쿨다운 숫자(남은 초).
- 조준 모드 UX:
  - 타깃팅 스킬 슬롯 클릭 시 `SkillBar.EnterAimMode(SkillData)` → 그 다음 유효 클릭(지면 or 방어 유닛 셀)을 수신해 `BattleBridge.CastSkill*` 호출 → 조준 모드 종료.
  - 조준 가이드는 **월드스페이스 LineRenderer**로 원 표시(MapView의 공유 Material 재사용, 새 머티리얼 자산 금지). 재료 수치 초기 Phase 2 결정 영역.
  - 조준 모드 진입 동안 `PlacementInput.enabled = false` — 직접 참조 금지, 대신 `GameManager`가 상태 기계 역할을 하여 `GameManager.IsAiming` 플래그를 양쪽이 읽는 구조.
  - 취소: 같은 슬롯 재클릭 또는 조준 영역 밖 클릭 → 복귀.
- EventSystem은 P0-09에서 추가된 기존 싱글 EventSystem 재사용.

### 2.6 로깅 확장 (BattleLogSchema v3)

**유지**: 실제 클래스명은 `BattleLogEntry` (phase0-decisions #P0-03의 `BattleSessionLog` 표기는 초기 문서 착오, 코드 기준 정정).

**필드 변경**:
- `phase` 기본값 `"phase1"` → `"phase2"` (Phase 1 decision #26 후속).
- 새 필드 `SkillRecord skill = new();`

```csharp
[Serializable]
public class SkillRecord
{
    public List<string> loadout = new();      // 이번 판의 고정 3종 SkillData.id
    public List<SkillUsageLog> usages = new();
}

[Serializable]
public class SkillUsageLog
{
    public string skill_id;
    public float time;                 // StartBattle 시작 후 경과(초)
    public Vector2Int target_tile;     // DefenderUnit 타깃도 타일 좌표로 통일. no-target은 (-1,-1)
    public int affected_count;         // SlowField 범위 내 적 수, 1 for 단일 타깃
}
```

`BattleLogger` 신규 메서드:
- `SetSkillLoadout(IEnumerable<string> ids)` — 현 세션 loadout 기록.
- `RecordSkillUsage(SkillUsageLog usage)` — usages에 push (List 내부 복사 불필요).

**호출 시점**:
- `DraftController.TryConfirm` → `logger.SetSkillLoadout(loadout ids)` (SetDraft 직후).
- `BattleBridge.CastSkill*` → `logger.RecordSkillUsage(...)` (영향 수집 후).

### 2.7 드래프트 → 스킬 로드아웃 결정 흐름 (고정)

- Phase 2는 **판마다 동일한 3종(Slow Field, Power Surge, Rapid Fire)** 을 사용한다.
- `DraftController.TryConfirm` 내부에서 `battleBridge.SetDefenderPool(picked)` 직후 `battleBridge.SetSkillLoadout(defaultSkillLoadout)` 호출.
- `DraftController`가 `[SerializeField] SkillData[] defaultSkillLoadout`(크기 3)을 인스펙터에서 참조. SkillData 3개는 `Assets/_Project/Data/Skills/` 아래의 고정 에셋 3개.
- 로드아웃 랜덤 셔플은 Phase 2 범위 외. Phase 3 이후 검토.

---

## 3. 종료 조건 (Done Criteria)

### 3.1 기능 이진 체크 (작업 순서)

Phase 0·1과 동일 원칙: 한 번에 한 작업만, 사용자 통과 확인, `phase2-decisions.md` 기록.

**[P2-01] SkillData SO + 고정 3종 콘텐츠**
- [x] `Data/SkillData.cs` + enums (`SkillEffectType`, `SkillTargetType`)
- [x] `Assets/_Project/Data/Skills/` 아래 `Skill_SlowField.asset`, `Skill_PowerSurge.asset`, `Skill_RapidFire.asset` 3개 생성 (수치는 §2.1 표 그대로)
- 선행: Phase 1 완료
- 완료 확인: Inspector로 SO 3개 열람, 모든 필드 읽힘, `Wassup/Skill` 메뉴로 생성 가능

**[P2-02] Effects 맥락 Component + EffectTickSystem**
- [x] `Battle/Effects/SlowEffect.cs`, `DamageBoost.cs`, `CooldownReduction.cs`
- [x] `Battle/Effects/EffectTickSystem.cs` (ISystem, BurstCompile) — 세 Component 각각에 대해 remaining 감쇄 + 만료 시 ECB로 Remove
- [x] `Battle/Effects/EffectSpawner.cs` static 유틸 — 외부에서 부여 시 이 함수만 경유
- 선행: P2-01
- 완료 확인: EditMode 테스트에서 EffectTickSystem을 수동 tick했을 때 remaining 감쇄·만료 시 Component 제거 확인

**[P2-03] Movement × SlowEffect, Combat × DamageBoost/CooldownReduction 읽기 연동**
- [x] `MovementSystem`이 SlowEffect를 읽어 이동 속도에 반영 (PathFollowState.speed 비수정)
- [x] `AttackSystem`이 DamageBoost를 읽어 방출 데미지에 반영 (AttackState.damage 비수정)
- [x] `AttackSystem`이 CooldownReduction을 읽어 cooldownRemaining 리셋 시 반영
- 선행: P2-02
- 완료 확인: EditMode 테스트 2건 — (a) SlowEffect 있는 적의 이동량이 multiplier 배수로 감소, (b) DamageBoost 있는 방어 유닛이 방출하는 IncomingDamage가 multiplier 배수

**[P2-04] BattleBridge.CastSkill + SkillRuntime 쿨다운**
- [x] `Core/SkillRuntime.cs` MonoBehaviour (Dictionary 쿨다운, Update tick, 싱글톤 금지)
- [x] `BattleBridge.CastSkillAtTile(SkillData, Vector2Int)` + `CastSkillOnDefender(SkillData, Vector2Int)` (affected_count out 포함)
- [x] SkillEffectType switch로 EffectSpawner 경유해 Component 부여
- [x] SkillRuntime.IsReady/Consume/GetRemainingNormalized
- [x] BattleBridge가 StartBattle/Restart/Redraft/Teardown 시 SkillRuntime 초기화
- 선행: P2-02, P2-03
- 완료 확인: `execute_code`로 `CastSkillAtTile(slowField, tile)` 호출 → Player가 배치한 경로 근방 적 엔티티에 SlowEffect 부여 확인, 이후 `IsReady` false, duration 경과 후 Component 제거·IsReady true

**[P2-05] SkillLoadout 주입**
- [x] `BattleBridge.SetSkillLoadout(SkillData[])` 메서드
- [x] `DraftController.TryConfirm`이 `SetDefenderPool` 직후 호출
- [x] `DraftController.defaultSkillLoadout` 인스펙터 필드(크기 3, Phase 2 고정)
- 선행: P2-04
- 완료 확인: Confirm 이후 `bb.SkillLoadout.Length == 3` 확인

**[P2-06] SkillBar UI (런타임 빌드)**
- [x] `UI/SkillBar.cs` — 자체 Canvas, 3슬롯 런타임 생성
- [x] 슬롯에 displayName, uiTint 배경, 쿨다운 숫자
- [x] 타일 타깃 스킬은 조준 모드 진입 → 다음 유효 클릭으로 `CastSkillAtTile` 실행
- [x] 방어 유닛 타깃 스킬은 조준 모드 진입 → 방어 유닛 셀 클릭으로 `CastSkillOnDefender` 실행
- [x] `GameManager.IsAiming` 상태 플래그로 `PlacementInput`과 협조(직접 참조 금지)
- 선행: P2-05
- 완료 확인: Editor Play에서 SkillBar 표시, 슬롯 클릭→조준→맵 클릭→효과 발동→쿨다운 숫자 감소 → 0초 도달 시 재사용 가능

**[P2-07] 로깅 확장 (v3)**
- [x] `BattleLogSchema`에 `SkillRecord`, `SkillUsageLog` 추가 + `BattleLogEntry.skill` 필드
- [x] `BattleLogEntry.phase` 기본값 `"phase2"`로 변경
- [x] `BattleLogger.SetSkillLoadout`, `RecordSkillUsage`
- [x] `DraftController.TryConfirm` → SetSkillLoadout 호출
- [x] `BattleBridge.CastSkill*` → RecordSkillUsage 호출
- 선행: P2-04, P2-05
- 완료 확인: `GameLogs/` 최신 세션 JSON에 `phase="phase2"`, `skill.loadout=[3개 id]`, `skill.usages=[적어도 1건]`

**[P2-08] EditMode 테스트 확장 (목표 16/16)**
- [x] `EffectTickSystemTests` (최소 2건): remaining 감쇄 tick, 만료 시 Component 제거
- [x] `MovementSystem × SlowEffect` 조합 테스트 1건 — 기존 MovementSystemTests에 추가 가능
- [x] 기존 13개(Movement 3 + UnitLifecycle 3 + DraftSession 7) 회귀 없음
- 선행: P2-02, P2-03
- 완료 확인: `run_tests` EditMode → **16/16 pass** (기존 13 + 신규 3 이상)

**[P2-09] Phase 0·1 회귀 체크**
- [x] 드래프트 UI → Confirm → 배치 → VICTORY/DEFEAT → Restart/Redraft 모두 정상
- [x] 로그 파일에 draft · skill.loadout · skill.usages · placements · result 모두 적재
- 선행: P2-07, P2-08
- 완료 확인: 한 판 수동 플레이 완주 + 로그 검증

**[P2-10] Android 실기기 검증 (P1-10 흡수)**
- [ ] 실기기에서 드래프트 → 전투 → 스킬 사용(타깃팅 포함) → 결과 → Restart/Redraft 정상
- 내용: P1-10이 하드웨어 부재로 유보 상태. Phase 2 실기기 검증 시 함께 처리(P0-13→P1-10 패턴 반복).
- 선행: P2-09
- 완료 확인: 실기기 스킬 터치/조준 동작 확인, 로그 파일이 `Application.persistentDataPath/GameLogs/`에 기록됨

---

### 3.2 아키텍처 이진 체크

**Phase 0·1 재확인 (회귀 방지)**
- [ ] ECS 시스템이 전투 로직을 계속 소유
- [ ] `BattleBridge`가 유일한 MonoBehaviour ↔ ECS 창구 (`CastSkill*`도 여기만)
- [ ] `EntityManager` / `World.DefaultGameObjectInjectionWorld` 사용은 `BattleBridge` 한 파일에만 존재 (Effects 맥락 내부 ISystem은 SystemAPI 사용 — 규칙 외)
- [ ] SubScene 미사용 / 네트워크 코드 전무
- [ ] 맥락 분리 유지 (Units / Movement / Combat / Effects)
- [ ] 드래프트 로직 MonoBehaviour 유지

**Phase 2 전용**
- [ ] Effects Component 쓰기(Add/Update/Remove)는 Effects 시스템 + EffectSpawner 유틸만 수행
- [ ] Movement/Combat은 Effects Component를 **읽기만** (PathFollowState.speed / AttackState.damage 원본 불변)
- [ ] SkillData 전부 SO — 효과 수치 하드코딩 없음
- [ ] SkillBar UI는 프리팹 무도입, 런타임 빌드 (DraftView 패턴 재사용)
- [ ] 새 싱글톤 도입 없음 — GameManager 여전히 유일 싱글톤. SkillRuntime은 MonoBehaviour 자식, 정적 Instance 없음
- [ ] `GameManager.IsAiming` 상태 플래그로 SkillBar ↔ PlacementInput 간접 조정 (직접 참조 없음)
- [ ] Assembly Definition 2개 체제 유지 (`Wassup.Runtime` + `Wassup.Tests.EditMode`)

---

### 3.3 주관 평가 게이트

Phase 2의 핵심 질문: **반복 플레이에서 스킬 사용 타이밍/대상 선택이 수렴하는가.**

- 3~5명 플레이어가 **동일 AttackDeck + 동일 유닛 풀 시드 + 동일 스킬 로드아웃 3종**에서 **최소 10판 반복 플레이**.
- 수집 지표:
  - 스킬별 **첫 사용 타이밍(초)** 분포 — 표준편차 감소 경향
  - 스킬별 **대상 좌표 밀집도** (TilePoint 스킬은 좌표 분산, DefenderUnit 스킬은 어떤 방어 유닛에 집중되는지)
  - 스킬 사용 직후 단기 KPI (10초 내 적 사망 수 / 생존 시간 증가)
  - 자가 보고: "이 스킬을 이 타이밍에 쓴 이유?" (첫 3판 vs 마지막 3판 언어화 비교)
- 통과 기준: 스킬 타이밍 분산 감소 + 사용 후 단기 KPI 개선 + 자가 보고의 언어화 구체성 상승. 상세 분석 방법은 PRD 4.2 참조(단, H2 전면 검증은 Phase 4에서).

**실패 경로** (PRD 부록 A.H2):
- 헤드리스 시뮬레이션으로 스킬 사용 시점별 결과 편차 재확인
- 스킬 효과 수치·쿨다운 재조정(SkillData SO만 수정, 코드 변경 없음)
- 3종 중 명백히 저효율 스킬이 있으면 교체 (단 Phase 2 내 3종 유지 원칙)
- 위 모두 OK인데도 실패라면 **스킬 축 자체 재설계** 또는 Phase 4 타이머/코스트 결합 후 재측정

---

## 4. 에이전트 자율 결정 영역

TRD §3·§5 준수 하에 다음은 구현 에이전트 재량:

- SlowEffect/DamageBoost/CooldownReduction 구체 필드 추가(예: `byte stackCount`). 다만 Phase 2는 non-stackable 가정 — 같은 효과 재부여는 duration/multiplier 덮어쓰기.
- EffectTickSystem의 tick 주기 (매 프레임 ISystem OnUpdate vs FixedSimulation). 단순한 쪽 우선.
- 조준 가이드 원의 두께·색상
- 쿨다운 시각화 (숫자 / 바 / 파이 — 숫자 기본, 단순 우선)
- `SkillRuntime` 위치 (GameManager 자식 고정. 별도 GameObject 여부만 재량)
- SkillBar 슬롯 크기·여백·폰트
- `affected_count` 측정 시 타깃팅 스킬은 1로 고정하는지 실제 영향 엔티티 수로 계산하는지 (실제 수 권장)
- `SkillData.id` 문자열 표기 규칙 (`skill_slowfield` vs `SlowField` 등)

**결정 원칙**: 애매하면 단순한 쪽. Phase 2는 H2의 스킬 축 검증용이지 스킬 장르 개척용이 아니다.

---

## 5. Phase 2 종료 시 에이전트 산출물

- 동작하는 Unity 6 프로젝트 (에디터 + Android 실기기 빌드 가능 상태)
- EditMode 테스트 16건 이상 pass (기존 13 + 신규 3+)
- `phase2-decisions.md` — 자율 결정 항목 누적 기록
- 스킬 사용 기록이 포함된 JSON 로그 샘플 3개 이상 (`GameLogs/`) — `phase="phase2"`, `skill.loadout`, `skill.usages` 모두 채워짐
- Phase 3에서 재활용될 핵심 타입 정리 (Effects Component 3종, EffectSpawner 유틸, SkillData, SkillBar 런타임 패턴, GameManager 상태 플래그)

---

## 6. Phase 2 이후 Phase 순서 (참고용)

`TRD.md` §6.3~6.5 및 이전 Phase 문서 §6과 동일 — Phase 3(배치 시 효과 / 인접 시너지) → Phase 4(마무리: 3분 타이머, 코스트, 봇 스코어, H2·H4 전면 검증).

- Phase 3에서 Effects 맥락이 Passive(유닛 고유 효과)와 인접 시너지로 확장된다. Phase 2에서 도입된 Component 3종은 그대로 활용되며, `SlowEffect` 같은 Component가 유닛 자체 Passive에서도 발원될 것.
- Phase 4에서 코스트·타이머 합류 후에야 H2 전면 검증이 가능하다. Phase 2 주관 평가 통과 ≠ H2 통과 — 조기 신호일 뿐.

Phase 2 종료 후 `PHASE3.md`를 작성한다. 직전 Phase 완료 전에 미리 작성하지 않는다.

---

## 7. TRD 금지 패턴의 Phase 2 재적용

TRD 섹션 5의 금지 패턴은 전부 Phase 2에도 유효하다. 특히 주의:

- **Effects 시스템은 Effects 맥락 안에서만 Component를 쓴다** — MovementSystem이 SlowEffect를 수정하거나 EffectTickSystem이 PathFollowState.speed를 직접 쓰면 안 됨. Add/Update는 `EffectSpawner` 유틸 경유.
- **Combat의 AttackState/Damage 원본 불변** — Effects의 multiplier는 읽어서 방출 값에만 곱한다. 원본 필드 쓰기 금지.
- **Units의 Health 직접 수정 금지** — Phase 2 스킬은 Heal을 포함하지 않는다(Health 쓰기 우회 경로 없음). Phase 3에서 Units 공식 HealEvent를 도입한 뒤 Heal 스킬 재도입 검토.
- **새 싱글톤 금지** — `SkillRuntime`은 MonoBehaviour. `public static Instance` 금지. 외부는 `GameManager.skillRuntime` SerializeField로만 접근.
- **"나중을 위한" 인터페이스 금지** — 3종 스킬 모두 `SkillEffectType` enum + `CastSkill*` switch 분기로 구현. `ISkillEffect` 추상화 Phase 2 금지.
- **수치 하드코딩 금지** — 모든 스킬 수치(range/magnitude/duration/cooldown)는 SkillData SO 필드. 리터럴 작성 금지.
- **Assembly Definition 2개 체제 유지** — `Wassup.Runtime` + `Wassup.Tests.EditMode`. Effects 맥락 추가되더라도 asmdef 분리 금지.
- **드래프트/배치 로직과 스킬 UI 직접 참조 금지** — `GameManager.IsAiming` 상태 플래그 경유.
- **판당 사용 횟수 제한 도입 금지** — 쿨다운 단일 메커니즘. 코스트·횟수 제한은 Phase 4 영역.

---

**문서 버전**: v1.0
**상태**: 확정, 에이전트 전달 준비됨
**Phase 0·1 완료 기반으로 작성**: 코드 실상(`AttackSystem`, `BattleLogEntry`, `DraftController.TryConfirm`, `DraftView` 런타임 빌드 패턴)과 정합.
**다음 업데이트**: Phase 2 구현 완료 후 `PHASE3.md` 작성 시 본 문서의 결정이 Phase 3 전제로 승계됨. Phase 2 진행 중 발견된 조정은 `phase2-decisions.md`에 누적.

# Phase 2 — 스킬

> 본 문서는 `PRD.md`, `TRD.md`, `PHASE0.md`, `PHASE1.md`, `phase0-decisions.md`, `phase1-decisions.md`를 전제로 작성되었다. Phase 0·1에서 확립된 아키텍처 경계, 맥락 분리, 추상화 규칙, 금지 패턴은 Phase 2에서도 그대로 유지된다. 본 문서는 **Phase 2의 What과 Done만 정의**한다. How(구체 스킬 목록, UI 레이아웃, 시스템 구성, Effects 맥락 구체화 방식 등)는 에이전트 구현 재량이다.

---

## 0. Phase 2의 존재 이유

### 검증 목표

> **"스킬 선택·사용이 반복 플레이에서 학습 신호를 만드는가"** — PRD 섹션 0의 **H2 (스킬 판단 학습 신호)** 를 1차 검증한다.

Phase 1까지는 "어떤 유닛을 뽑아서 어디에 놓느냐"의 결정만 학습 표면이었다. Phase 2는 여기에 **"어떤 스킬을 언제 쓸 것인가"** 라는 두 번째 학습축을 더한다. 이 축이 유의미한 신호를 생성하지 못하면 게임은 결국 배치 최적화 퍼즐로 수렴하고, 디펜스 장르 고유의 결정밀도가 약화된다 (PRD 부록 A, H2 실패 시 스킬 체계 전면 재설계).

Phase 2는 스킬을 **플레이어 능동 사용(Active)** 과 **유닛 개별 고유 효과(Passive)** 두 종류로 도입하되, **Phase 2에서는 Active 스킬만 구현**한다. Passive는 Phase 3(배치 시 효과) 이후 인접 시너지와 함께 재검토.

### Phase 2가 하는 것 / 안 하는 것

**Phase 2가 하는 것:**
- **Active 스킬 3~5종** 도입 — 각 유닛에 종속되지 않는 판 전역 스킬(예: 전체 느리게, 지정 유닛 공격력 증가, 지정 구간 광역 대미지, 단일 적 대상 즉시 데미지 등).
- 스킬은 **쿨다운 기반**. 한 판당 N회 제한(자율 결정)로도 가능하지만 쿨다운이 기본선. H2 측정 비교 가능성을 위해.
- **ECS Effects 맥락 활성화** — Phase 0·1에서 자리만 있던 `Battle/Effects/` 폴더에 실제 Component/System 진입. 스킬 발동 시 대상에 상태이상 Component 부여 → EffectSystem이 duration/감쇄 처리.
- **스킬 UI** — 화면 하단 스킬 슬롯 + 쿨다운 표시 + 탭-드래그로 조준 또는 즉발. 배치 UI와 공존 가능한 레이아웃.
- **드래프트 흐름 확장**: 스킬 드래프트는 Phase 2에 **도입하지 않음**. Phase 1의 유닛 드래프트 확정 후 고정 스킬 세트(또는 판마다 동일 풀에서 랜덤 3종 자동 할당 — 자율 결정)를 사용. 스킬 드래프트는 Phase 3 이후 재검토.
- 로깅 확장 — `BattleLogSchema`에 `skill_usages`(스킬 id, 타깃, 시간, 효과량) 기록.

**Phase 2가 하지 않는 것:**
- **스킬 드래프트** — Phase 1 드래프트는 유닛에만 적용. 스킬은 고정 또는 랜덤 배분 (자율 결정).
- **배치 시 효과(onPlace) / 인접 시너지** — Phase 3.
- **코스트 시스템** — Phase 4.
- **3분 타이머 / 봇 비교** — Phase 4.
- **유닛별 Passive 고유 스킬** — 공통 Active 스킬만 먼저 도입. Passive는 인접 효과 구조가 준비된 뒤에 (Phase 3).
- **스킬 레벨/업그레이드** — 단일 레벨. 동일 효과 세기.

---

## 1. Phase 2의 게임 흐름

```
[빌드 실행]
  ↓
[맵 + 경로 + 배치 타일 표시 (Phase 0 동일)]
  ↓
[드래프트 UI: 10종 → 7종 픽 (Phase 1 동일)]
  ↓
[픽 확정 + 스킬 세트 결정 — 고정 또는 랜덤 3종]
  ↓
[실시간 디펜스: 배치 = picked 7종 중 랜덤 / 스킬 = 하단 슬롯 탭]
  ↓
[스킬 효과 적용 ↔ 적에게 영향 ↔ 쿨다운 회전]
  ↓
[결과 화면 (VICTORY / DEFEAT)]
  ↓
[다시 시작 | 다른 픽으로 재도전]
```

Phase 1의 분기(`RestartRequested` vs `RedraftRequested`)는 그대로 유지. 스킬 세트는 드래프트 확정 시 함께 확정되어 `SkillLoadout` 형태로 `BattleBridge`에 주입.

---

## 2. Phase 2 콘텐츠 스펙

### 2.1 스킬 풀 (3~5종)

에이전트 재량으로 3~5종을 선정. 단순 업그레이드 관계 금지 — 각 스킬은 명확한 트레이드오프(범위/효과/지속/쿨다운)를 가진다. 제안 축(자율 결정, `phase2-decisions.md` 기록):

| 축 | 변주 |
|---|---|
| **효과 유형** | 즉시 대미지 · 둔화 · 공격력 증폭 · 체력 회복(방어 유닛) · 재장전 단축 |
| **타깃팅** | 자가 선택 유닛 · 자가 선택 지점(반경 R) · 전역(적 전체) |
| **지속시간** | 즉발 · 짧은 디버프 (3s) · 긴 버프 (8~12s) |
| **쿨다운** | 짧음(15s) · 중간(30s) · 긴 대기(60s+) |

**구체 예시**(확정 아님, 에이전트 조율):
- **Slow Field**: 지정 지점 반경 2 범위 내 적 이동 30% 감소, 5s 지속, CD 20s.
- **Power Surge**: 지정 방어 유닛 공격력 2배, 8s 지속, CD 30s.
- **Meteor**: 지정 지점 반경 1.5 범위 적에 80 즉시 대미지, CD 45s.
- **Heal Pulse**: 지정 방어 유닛 체력 40 회복, 즉발, CD 25s.

각 스킬은 `SkillData` ScriptableObject로 정의, 하드코딩 0건(PHASE1 §7 재적용).

### 2.2 ECS Effects 맥락 활성화

`Assets/_Project/Scripts/Battle/Effects/` 아래에 다음 요소를 이번 Phase에서 처음 도입:

- `SlowEffect` (IComponentData) — `float remaining, float multiplier`
- `DamageBoost` (IComponentData) — `float remaining, float multiplier`
- `EffectTickSystem` (ISystem) — 매 틱 remaining 감쇄, 0 도달 시 Component 제거
- 추후 적으로도 대미지 효과 (Burn 등) 가능 — 필요 시 같은 맥락에서 확장

**맥락 경계 재확인** (TRD 2.5.2):
- `SlowEffect`의 `multiplier` 값은 **Effects 소유** — 다른 맥락 쓰기 금지
- `MovementSystem`이 `SlowEffect`를 **읽고** `PathFollowState.speed` 계산에 반영 — 읽기는 허용. 쓰기는 여전히 Movement(PathFollowState만 쓰기).
- `CombatSystem`이 `DamageBoost`를 **읽고** `AttackState.damage` 곱셈에 반영 — 동일 패턴.

### 2.3 스킬 발동 진입점

- **BattleBridge 확장**: `CastSkill(SkillData skill, Vector2Int? target)` 메서드. UI 레이어의 유일한 창구(TRD 2.4 재확인, ECS 접근은 여기서만).
- 내부 구현: SkillData.effectType에 따라 `EntityManager`로 타깃 쿼리 → `SlowEffect` 등 Component를 추가/업데이트 → 이벤트 로그 남김.
- **쿨다운 관리**: MonoBehaviour 레이어에서 `SkillRuntimeState { SkillData data, float cooldownRemaining }` per-skill 보관. Update에서 `cooldownRemaining -= Time.deltaTime`. UI가 이 상태를 읽어 슬롯 표시.

### 2.4 스킬 UI

- 화면 하단 가로 배치 스킬 슬롯 3~5개
- 슬롯은 SkillData.icon(미정의 시 색상) + 쿨다운 파이/숫자 표시
- 클릭: 즉발 스킬은 바로 실행, 타깃팅 스킬은 "조준 모드" 진입 → 다음 클릭의 타일 좌표/유닛을 타깃으로 사용
- 조준 모드 표시: 맵 위 반투명 원(예상 범위). 취소: ESC 또는 다시 슬롯 클릭.
- 카드 형태(프리팹 무도입 방침은 Phase 1과 동일하게 유지, `DraftView` 패턴 재사용 권장)

### 2.5 로깅 확장 (BattleLogSchema v3)

`BattleLogEntry`에 추가:

```csharp
[Serializable]
public class SkillUsageLog
{
    public string skill_id;
    public float time;
    public Vector2Int target_tile; // no-target 스킬은 -1,-1
    public int affected_count;
}

// BattleLogEntry 추가:
public List<SkillUsageLog> skill_usages = new();
public List<string> skill_loadout = new(); // 이번 판에 사용 가능했던 스킬 ids
```

`BattleLogger`에 `RecordSkillUsage`, `SetSkillLoadout` 메서드. `BattleBridge.CastSkill`이 호출.

### 2.6 드래프트 / 스킬 확정 흐름

**Phase 2 기본(자율 결정)**: 유닛 드래프트 확정 시 스킬 세트도 확정된다. 두 접근 중 하나 선택(에이전트 결정, `phase2-decisions.md`):
1. **고정 스킬 3종** — 모든 판 동일.
2. **풀에서 랜덤 3종** — 유닛 풀 시드와 분리된 스킬 시드로 재현 가능.

스킬 드래프트 UI는 **도입하지 않음**. 스킬 선택을 학습 축으로 포함시키면 H2·H1 구분이 흐려지므로 Phase 2에서는 "사용 타이밍"에만 집중.

---

## 3. 종료 조건 (Done Criteria)

### 3.1 기능 이진 체크 (작업 순서)

**[P2-01] SkillData SO + 3~5종 콘텐츠**
- [ ] `Data/SkillData.cs` SO (id, displayName, description, effectType, range, duration, magnitude, cooldown, icon)
- [ ] `Assets/_Project/Data/Skills/` 아래 3~5개 SO 생성
- 선행: Phase 1 완료
- 완료 확인: Inspector로 SO 전부 열람, 필드 읽힘

**[P2-02] Effects 맥락 Component/System**
- [ ] `Battle/Effects/SlowEffect.cs`, `DamageBoost.cs` (IComponentData)
- [ ] `Battle/Effects/EffectTickSystem.cs` (ISystem, BurstCompile)
- [ ] `MovementSystem`이 SlowEffect 읽기 반영
- [ ] `AttackSystem`이 DamageBoost 읽기 반영
- 선행: P2-01
- 완료 확인: EditMode 테스트로 SlowEffect 주어진 상태에서 이동속도 감소 확인

**[P2-03] BattleBridge.CastSkill**
- [ ] `BattleBridge.CastSkill(SkillData, Vector2Int?)` 메서드
- [ ] SkillData.effectType별 분기 (Slow/DamageBoost/Meteor/Heal 등)
- [ ] 쿨다운 전역 체크(쿨다운 중이면 fail)
- 선행: P2-02
- 완료 확인: `execute_code`로 CastSkill 호출 → 영향받은 엔티티 Component 확인

**[P2-04] SkillLoadout 선정 흐름**
- [ ] DraftController 확정 시 SkillLoadout 결정 로직 추가 (고정 or 랜덤)
- [ ] BattleBridge에 loadout 주입 (`SetSkillLoadout(SkillData[])`)
- 선행: P2-03
- 완료 확인: 로그에 `skill_loadout`이 3~5개 id로 기록

**[P2-05] 스킬 UI 슬롯**
- [ ] `UI/SkillBar` MonoBehaviour (DraftView와 동일한 런타임 빌드 패턴)
- [ ] 3~5개 슬롯 표시 + 쿨다운 시각화
- [ ] 타깃팅 스킬은 조준 모드 진입 후 타일 클릭으로 실행
- 선행: P2-03
- 완료 확인: Editor Play에서 스킬 슬롯 클릭 → 효과 발동 → 쿨다운 회전 확인

**[P2-06] 로깅 확장**
- [ ] `BattleLogSchema` v3 필드 추가 (`skill_loadout`, `skill_usages`)
- [ ] `BattleLogger.RecordSkillUsage` / `SetSkillLoadout`
- [ ] CastSkill이 RecordSkillUsage 호출
- 선행: P2-03, P2-04
- 완료 확인: `GameLogs/` 최신 세션 JSON에 스킬 사용 기록 존재

**[P2-07] EditMode 테스트 확장**
- [ ] EffectTickSystem 테스트 (duration 감쇄, expire 시 component 제거)
- [ ] MovementSystem × SlowEffect 조합 테스트 (속도 감소 확인)
- [ ] 기존 13개 테스트 회귀 없음
- 선행: P2-02
- 완료 확인: run_tests 전부 pass

**[P2-08] Phase 0·1 회귀 체크**
- [ ] 드래프트 흐름 정상, 배치 정상, VICTORY/DEFEAT 정상, Restart/Redraft 정상
- [ ] 로그 파일에 draft·skill_loadout·skill_usages 모두 적재
- 선행: P2-06, P2-07
- 완료 확인: 한 판 수동 플레이 완주 + 로그 검증

**[P2-09] Android 실기기 검증**
- [ ] 실기기에서 드래프트 → 전투 → 스킬 사용 → 결과 전 과정 정상
- 선행: P2-08
- 완료 확인: 실기기 스킬 터치/조준 동작 확인

---

### 3.2 아키텍처 이진 체크

**Phase 0·1 재확인 (회귀 방지)**
- [ ] ECS 시스템이 전투 로직을 계속 소유
- [ ] `BattleBridge`가 유일한 MonoBehaviour ↔ ECS 창구 (이제 `CastSkill`도 여기 경유)
- [ ] `EntityManager` / `World.DefaultGameObjectInjectionWorld` 사용은 `BattleBridge` 한 파일에만 존재
- [ ] SubScene 미사용 / 네트워크 코드 전무
- [ ] 맥락 분리 유지 (Units / Movement / Combat / Effects)
- [ ] 드래프트 로직 MonoBehaviour 유지

**Phase 2 전용**
- [ ] Effects 맥락이 활성화되었고, Effects Component 쓰기는 Effects 시스템만 수행
- [ ] Movement/Combat은 Effects Component를 **읽기만**
- [ ] SkillData 전부 SO — 효과 수치 하드코딩 없음
- [ ] SkillBar UI는 프리팹 무도입(DraftView 런타임 빌드 패턴 재사용) 또는 단일 프리팹 범위 안
- [ ] 새 싱글톤 도입 없음 — GameManager 여전히 유일 싱글톤

---

### 3.3 주관 평가 게이트

Phase 2의 핵심 질문: **반복 플레이에서 스킬 타이밍/대상 선택이 수렴하는가.**

- 3~5명 플레이어가 동일 AttackDeck + 동일 유닛 풀 시드 + 동일 스킬 세트로 **최소 10판 반복 플레이**
- 수집 지표:
  - 스킬별 평균 사용 타이밍(초) 분포 — 표준편차 감소 경향
  - 스킬 사용 후 KPI(적 처치 효율/생존 시간) 상승 경향
  - 자가 보고: "이 스킬을 이 타이밍에 쓴 이유?"
- 통과 기준: 스킬 타이밍 분포 수렴 + 사용 후 KPI 개선. 상세는 PRD 4.1 재확인.

**실패 경로** (PRD 부록 A.H2):
- 헤드리스 시뮬레이션으로 스킬별 결과 편차 재검증
- 스킬 효과 수치·쿨다운 재조정
- 스킬 수 축소 (3종으로 단순화 후 재검증)
- 위 모두 OK인데도 실패라면 **스킬 축 자체 재설계** 또는 Phase 4의 타이머/봇 기반 경쟁 구조 우선 투입 검토

---

## 4. 에이전트 자율 결정 영역

TRD 섹션 3·5 준수 하에 다음은 구현 에이전트 재량:

- 스킬 개수 (3~5종)
- 스킬 효과 타입 조합 (Slow/Damage/Buff/Heal 등)
- 타깃팅 UX (조준 원 모양, 취소 UX, 즉발 vs 지연)
- 스킬 세트 선정 방식 (고정 vs 랜덤, 어떤 시드)
- SkillData 필드 구조 (enum vs string effectType)
- 쿨다운 시각화 (파이 / 바 / 숫자)
- `skill_loadout` 로그 표기 (id / displayName)
- SkillBar 배치 (하단 중앙 / 좌하단 / 동적)
- EffectTickSystem의 tick 주기 (매 프레임 vs 고정 간격)
- Component 단위 Effect vs 단일 "EffectStack" Component (복수 효과 누적 시 관리 방식)
- 조준 모드 진입 시 배치 UI 입력 비활성 여부

**결정 원칙**: 애매하면 단순한 쪽. Phase 2는 H2 검증용이지 스킬 장르 개척용이 아니다.

---

## 5. Phase 2 종료 시 에이전트 산출물

- 동작하는 Unity 6 프로젝트 (에디터 + Android 실기기 빌드 가능)
- EditMode 테스트에 EffectTickSystem + Movement×Effect 연동 테스트 추가 (최소 3건)
- `phase2-decisions.md` — 자율 결정 항목 누적 기록
- 스킬 사용 기록이 포함된 JSON 로그 샘플 3개 이상 (`GameLogs/`)
- Phase 3(배치 시 효과·인접 시너지)에서 재활용될 핵심 타입 정리 (Effects Component 계열, SkillData, SkillBar 런타임 패턴)

---

## 6. Phase 2 이후 Phase 순서 (참고용)

`TRD.md` §6.3~6.5 및 `PHASE0.md` §6 / `PHASE1.md` §6과 동일 — Phase 3(배치 시 효과 / 인접 시너지) → Phase 4(마무리: 3분 타이머, 봇 스코어, 측정 프로토콜 통합).

Phase 2 종료 후 `PHASE3.md`를 작성한다. 직전 Phase 완료 전에 미리 작성하지 않는다.

---

## 7. TRD 금지 패턴의 Phase 2 재적용

TRD 섹션 5의 금지 패턴은 전부 Phase 2에도 유효하다. 특히 주의:

- **Effects 시스템은 Effects 맥락 안에서만 Component를 쓴다** — MovementSystem이 SlowEffect를 수정하거나 EffectTickSystem이 PathFollowState.speed를 직접 쓰면 안 됨.
- **새 싱글톤 금지** — SkillRuntimeState는 MonoBehaviour 내부 리스트/딕셔너리로 보관, 별도 Instance 정적 필드 도입 금지.
- **"나중을 위한" 인터페이스 금지** — 스킬 효과 타입이 4가지라도 `ISkillEffect` 추상화 도입은 구현체가 2개 이상일 때까지 유예(필요 시에만). SkillData.effectType enum + BattleBridge 분기 switch로 시작.
- **수치 하드코딩 금지** — 모든 스킬 수치는 SkillData SO 필드. 쿨다운·범위·지속 같은 값 리터럴 작성 금지.
- **Assembly Definition 2개 체제 유지** — `Wassup.Runtime` + `Wassup.Tests.EditMode`. Effects 맥락이 추가되더라도 asmdef 분리 금지.
- **드래프트/배치 로직과 스킬 UI의 상호 침투 금지** — 스킬 조준 모드가 배치 입력(PlacementInput)을 비활성화해야 한다면, 서로를 직접 참조하지 말고 GameManager 레이어에서 상태 기계로 조정.

---

**문서 버전**: v0.1
**상태**: 초안, Phase 1 완료 직후 작성
**다음 업데이트**: Phase 2 구현 완료 후 (phase2-decisions.md 참고하여 Phase 3 문서 작성)

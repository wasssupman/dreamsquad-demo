# Phase 7 — 스킬 확장 + 랜덤 로드아웃

> Phase 6 완료 직후. 핵심 게임 루프는 동작하므로 **플레이 감각 보강**을 목적으로 스킬 콘텐츠/변별력을 넓힌다. 기획 레벨에서 "매 판 스킬 조합이 달라지는" 경험을 도입해 드래프트·배치 의사결정의 폭을 확장한다.

---

## 1. 목표

- 스킬 **6종 풀**에서 매 판 **랜덤 2종** 배정 → 세션별 플레이 전략이 바뀐다.
- 새 스킬 3종 (Tornado / Meteor / Portal) 추가 — 기존 3종과 성격이 겹치지 않는 **CC / 딜레이 AoE / 위치 조작**.
- 드래프트 단계에서 **이번 판 스킬 2종을 미리 공개**하여 유닛 픽이 스킬 시너지와 연결되도록 한다.

### 비목표

- 새 SkillEffectType 이외의 전투 밸런스 튜닝(유닛/적 스탯/타이머 등).
- 멀티 로드아웃/덱 프리셋 — "랜덤 배정"만. 저장/불러오기 X.
- 커스텀 스킬 아이콘 — 여전히 컬러 tint 방식 유지. 이름 라벨만.
- 3종 이상 동시 배정 — 본 Phase는 정확히 2종.

---

## 2. 확정된 결정 (Q&A 결과)

1. **Tornado = 중심 인력 CC**: 범위 내 적을 중심점으로 끌어당긴다. 데미지 없음, 이동 벡터만 조작.
2. **Meteor = 경고 후 낙하 AoE**: 캐스트 시 빨간 경고 링 1.5s → 대량 단일 타격 AoE. Danger preview.
3. **Portal = 2탭 텔레포트**: 1탭 입구 타일, 2탭 출구 타일. 적이 입구 타일을 통과하면 출구 타일로 즉시 이동. 일정 시간 지속.
4. **스킬 풀 = 6종 합산**: SlowField / RapidFire / PowerSurge (기존) + Tornado / Meteor / Portal (신규).
5. **공개 시점 = 드래프트 화면 동시 노출**: DraftView에 "이번 판 스킬 2종" 패널 추가. 플레이어가 보고 픽을 결정.
6. **재랜덤 규칙**: Redraft → 스킬도 재랜덤. Restart → 같은 조건 유지 (재도전).
7. **초기 밸런스**:
   - Tornado: cost=3, cooldown=12s, range=1.5, duration=2s, magnitude=pull force
   - Meteor: cost=4, cooldown=18s, range=1.8, duration=0 (즉시), magnitude=damage
   - Portal: cost=3, cooldown=14s, range=0 (두 점), duration=8s
8. **SkillBar 슬롯 3→2 축소**: 랜덤 2종만 쓰므로 빈 슬롯 제거.
9. **로그 시드**: `SkillRecord`에 `seed` + 선발 loadout 원본(6종 풀) 기록. DraftRecord.seed와 별도.

---

## 3. 새 스킬 3종 상세

### 3.1 Tornado (토네이도)
- **target**: `TilePoint` (단일 타일)
- **effect**: 범위 내 적 Position을 매 프레임 중심 타일 방향으로 당긴다. 속도 = `magnitude` units/sec.
- **맥락**: Movement (Position 쓰기 소유). Effects 맥락의 `TornadoPull` Component + Movement 내 시스템이 tick.
- **durationSec** 동안 지속. 중첩 시 마지막 타일 캐스트의 중심만 유효.

### 3.2 Meteor (메테오)
- **target**: `TilePoint`
- **visual**: 1.5s 경고 링 (빨간 투명 원) → 낙하 애니 → 폭발.
- **effect**: 경고 종료 시점에 범위 내 모든 적에게 `magnitude` 데미지 1회.
- **맥락**: Combat (Damage 쓰기 소유). Effects 맥락의 `MeteorPending` Component → Combat 시스템이 타이머 만료 시 DamageQueue 발행.
- **durationSec = 0**, 대신 `warningSec = 1.5` 필드 추가.

### 3.3 Portal (포탈)
- **target**: **2탭** — 새 UI 플로우. 1탭 = 입구, 2탭 = 출구.
- **SO 저장 방식**: `SkillData.target` 은 기존 `TilePoint` 재사용, `SkillTargetType` 추가 없음. PlacementInput/SkillBar가 캐스트 중 두 점 수집 상태 머신.
- **effect**: `durationSec` 동안 입구 타일을 통과하는 적을 출구 타일로 텔레포트. 경로 상의 waypoint 인덱스는 출구 타일 위치 기반으로 재계산.
- **맥락**: Movement. Effects 맥락의 `PortalLink` Component (2 tile 쌍) → Movement 시스템이 trigger 검사.

---

## 4. 랜덤 로드아웃 시스템

### 4.1 데이터

- 새 파일: `Assets/_Project/Scripts/Core/SkillLoadoutController.cs` (MonoBehaviour, 비싱글톤).
- API:
  - `Configure(List<SkillData> pool, int count = 2, int seed = 0)` — 풀/선발수/시드 주입.
  - `Roll()` — Fisher-Yates로 `count` 종 선발 → `Picked` 공개.
  - `Picked` (`IReadOnlyList<SkillData>`).
  - `Seed` (사용된 시드 공개).

### 4.2 진입 순서

- GameManager.OnBriefingConfirmed →
  1. DraftController.BeginDraft() (기존)
  2. SkillLoadoutController.Roll()  ← 신규
  3. DraftView가 `picked` 표시 (신규 패널)
- Redraft 버튼 → Draft + SkillLoadout 재랜덤.
- Restart 버튼 → 기존 picked/draft 유지. SkillLoadoutController.Roll 호출 X.

### 4.3 UI 변경

- **DraftView** (`UI/DraftView.cs`): 화면 상단 또는 우측에 "이번 판 스킬" 패널 추가.
  - 2개 슬롯 = 이번 판 2종 (컬러 tint + 이름)
  - 그 아래 "풀: 6종" 요약(회색 작은 텍스트, 선발된 2종만 불투명).
- **SkillBar** (`UI/SkillBar.cs`): 3슬롯 → 2슬롯. 로드아웃의 2종만 표시. 기존 배치/쿨/코스트 게이트 그대로.

### 4.4 ECS 시스템 변경

- `SkillEffectType` enum에 `Tornado, Meteor, Portal` 추가.
- `Effects` 폴더:
  - `TornadoPullComponent.cs` + Movement 시스템에서 소비
  - `MeteorPendingComponent.cs` + Combat 시스템에서 소비 + Warning Visual
  - `PortalLinkComponent.cs` + Movement 시스템에서 소비
- `BattleBridge`에 3개 CastSkill 경로 추가 (기존 3종과 같은 패턴).

---

## 5. 로그 스키마

- `BattleLogEntry.phase = "phase7"`.
- `SkillRecord`에 추가:
  ```csharp
  public List<string> pool;   // 6종 SO id
  public List<string> picked; // 2종 SO id (순서 없음)
  public int seed;
  ```
- `SkillUsageLog`에 `target_tile_b` 옵셔널 필드 추가 (Portal 전용, `Vector2Int(-1,-1)` = 미사용).

---

## 6. 작업 분해 — P7-NN

### 6.1 데이터 / SO

- [ ] P7-01 — `SkillEffectType` enum에 Tornado/Meteor/Portal 추가 + `SkillData.warningSec` 필드 추가
- [ ] P7-02 — 새 3종 SO 에셋 생성 (Assets/_Project/Data/Skills/): Tornado/Meteor/Portal 초기값

### 6.2 런타임

- [ ] P7-03 — `SkillLoadoutController.cs` 신규 + GameManager에 serialize field 추가
- [ ] P7-04 — 6종 풀 기본 리스트 SO 참조 (GameManager 또는 CostConfig 옆 새 SO)
- [ ] P7-05 — DraftController.BeginDraft 성공 경로에서 SkillLoadoutController.Roll 호출

### 6.3 ECS 효과

- [ ] P7-06 — Tornado: Effects Component + Movement system (인력)
- [ ] P7-07 — Meteor: Effects Component + Combat system (지연 AoE) + 경고 링 비주얼
- [ ] P7-08 — Portal: Effects Component + Movement system (텔레포트) + 2탭 캐스트 UI 플로우

### 6.4 UI

- [ ] P7-09 — DraftView "이번 판 스킬 2종" 패널
- [ ] P7-10 — SkillBar 3→2 슬롯 축소 + 동적 로드아웃 바인딩

### 6.5 로깅 / Restart / Redraft

- [ ] P7-11 — SkillRecord.pool/picked/seed 필드 + BattleBridge에서 기록
- [ ] P7-12 — Redraft 경로에서 SkillLoadoutController.Roll 재호출, Restart는 생략
- [ ] P7-13 — SkillUsageLog.target_tile_b (Portal 전용)

### 6.6 검증

- [ ] P7-14 — EditMode 테스트: SkillLoadoutController Roll 결정성(동일 seed=동일 픽), pool 크기 경계
- [ ] P7-15 — PlayMode 회귀: 드래프트 → 배치 → 전투 → Restart/Redraft 스킬 상태 올바른지 사용자 확인

---

## 7. 종료 조건

- 매 판 진입 시 드래프트 화면에 서로 다른 스킬 2종이 노출된다 (seed 고정 시 동일).
- 3종 신규 스킬이 ECS 전투에서 의도한 효과를 낸다 (Tornado 인력 / Meteor 경고+AoE / Portal 2점 텔레포트).
- SkillBar 2슬롯, 기존 코스트/쿨 게이트 정상.
- Restart → 같은 loadout. Redraft → 새 loadout.
- JSON 로그에 `skill.pool`, `skill.picked`, `skill.seed`, `target_tile_b` 채워짐.
- EditMode 테스트 통과, 컴파일 에러 0.

---

## 8. TRD 금지 패턴 재적용

- **싱글톤 금지** — SkillLoadoutController는 비싱글톤, GameManager가 ref 보유.
- **수치 하드코딩 금지** — 모든 cost/cd/range/magnitude/warningSec SO.
- **새 맥락 폴더 금지** — Effects 맥락 내 새 Component. Movement/Combat system이 소유 Component 쓰기.
- **맥락 경계** — Portal/Tornado는 Position 쓰기 → Movement 시스템. Meteor는 Damage 쓰기 → Combat 시스템.
- **"나중을 위한" 추상 금지** — ISkillEffect 등 인터페이스 신설 금지. 기존 enum switch 유지.

---

**문서 버전**: v0.1 (스펙 확정, 구현 미시작)
**결정 출처**: 사용자 응답 — Q1(b)/Q2(b)/Q3(b)/Q4(a)/Q5(a)/Q6(a)/Q7(유지)/Q8(ok)/Q9(ok)

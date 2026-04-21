# Phase 1 — 드래프트

> 본 문서는 `PRD.md`, `TRD.md`, `PHASE0.md`, `phase0-decisions.md`를 전제로 작성되었다. Phase 0에서 확립된 아키텍처 경계 · 맥락 분리 · 추상화 규칙 · 금지 패턴은 Phase 1에서도 그대로 적용된다. 본 문서는 **Phase 1의 What과 Done만 정의**한다. How(구체 클래스 설계·UI 레이아웃·드래프트 시드 관리 방식 등)는 에이전트 구현 재량이다.

---

## 0. Phase 1의 존재 이유

### 검증 목표

> **"반복 플레이를 통해 드래프트 픽 결정이 학습·수렴하는가"** — PRD 섹션 0의 **H1 (드래프트 판단 학습 신호)** 을 1차 검증한다.

Phase 1은 Phase 0의 "랜덤 배치"를 "플레이어가 드래프트한 7종 중에서 배치" 구조로 교체한다. 이 교체만으로 동일 공격 패턴을 반복 플레이할 때 플레이어가 픽을 의미 있게 조정하기 시작하는지가 드러난다. 픽이 학습 신호를 만들지 못하면 **프로젝트 자체의 근본 가정이 흔들린다** (PRD 부록 A, H1 실패 시 설계 전면 재검토).

본 Phase는 **H1의 선제 검증**이 목적이며, H2/H3는 Phase 0에서 이미 선제 검증된 상태에서 Phase 2(스킬)·Phase 4(타이머/봇 비교)까지 이어 검증된다.

### Phase 1이 하는 것 / 안 하는 것

**Phase 1이 하는 것:**
- 방어 유닛 풀을 10~12종으로 확장 (Phase 1 착수 당시 Archer/Guardian/Cannon 3종)
- 판 시작 전 드래프트 UI 노출: 10종 중 7종 픽
- 드래프트 확정 후 Phase 0 루프 진행: 플레이어가 Buildable 타일 클릭 시 **picked 7종 중 랜덤** 선택되어 배치
- 드래프트 결과를 `BattleLogSchema`의 `draft.pool`(10종) + `draft.picked`(7종) 필드에 기록
- "다시 시작" 플로우: 기본은 같은 pick으로 재도전 (H1 학습 신호 측정 요건 — 동일 패턴 + 동일 픽에서 배치·스킬 숙련도 측정). 별도 "다른 픽으로" 버튼이 드래프트를 재오픈
- P0-13에서 유보된 Android 실기기 검증을 본 Phase 종료 시점(P1-10)에서 함께 처리

**Phase 1이 하지 않는 것:**
- 스킬 (Phase 2)
- 배치 시 효과 / 인접 시너지 (Phase 3)
- 코스트 시스템 (Phase 4 이후)
- 3분 타이머 (Phase 4)
- 봇 상대 + 스코어 비교 (Phase 4)
- 공격 타임라인 수동 조정·시즌 교체 (프로토타입 범위 외)

---

## 1. Phase 1의 게임 흐름

```
[빌드 실행]
  ↓
[맵 + 경로 + 배치 타일 표시 (Phase 0과 동일)]
  ↓
[공격 타임라인 브리핑 (선택 — PRD 2.1, H1 학습 신호 근거)]
  ↓
[드래프트 UI: 10종 카드 노출 → 7종 픽]
  ↓
[픽 확정 버튼]
  ↓
[실시간 디펜스 진행 — 배치 pool = picked 7종]
  ↓
[결과 화면 (VICTORY / DEFEAT)]
  ↓
[다시 시작 (같은 픽 유지) | 다른 픽으로 재도전 (드래프트 재오픈)]
```

**재플레이 분기**: 결과 화면에서 두 버튼을 제공한다. 기본 "다시 시작"은 이전 픽을 유지한 채 같은 AttackDeck를 재실행 — 이 반복에서 **배치 위치 개선**의 학습 신호가 수집된다. "다른 픽으로"는 드래프트를 재오픈해 새 7종을 고를 수 있게 한다 — 이 경로에서 **픽 수렴**의 학습 신호가 수집된다.

---

## 2. Phase 1 콘텐츠 스펙

### 2.1 방어 유닛 풀 확장

Phase 0의 3종(Archer, Guardian, Cannon)에 7종 추가하여 **전체 10종** 구성. 각 유닛은 다른 유닛과 명확한 트레이드오프를 가져야 한다 — 단순 업그레이드(상위 호환) 관계는 금지(픽 결정을 무의미하게 만듦). 풀 설계 지침:

| 축 | 변주 |
|---|---|
| **사거리** | 근접(1~2) · 중거리(3~4) · 장거리(5~7) |
| **공격속도** | 빠름(cd 0.2~0.4s) · 중간(cd 0.8~1.2s) · 느림(cd 2~3s) |
| **데미지** | 낮음(5~15) · 중간(20~35) · 높음(50~80) |
| **체력** | 낮음(30) · 중간(60) · 높음(100+) |
| **역할 시그니처** | 단일 타깃 / 군중 억제 용도 암시 / 장시간 생존 등 |

**자율 결정**: 구체 스탯은 에이전트 재량(`phase1-decisions.md`에 기록). "광역 공격(AoE)" 등 Phase 2 이후의 효과는 Phase 1에서 **도입하지 않는다** — 단일 타깃 스탯 튜닝만으로 10종의 구분을 만들어낸다.

신규 SO 생성 위치: 기존 `Assets/_Project/Data/Defenders/` 일관성 유지 (Phase 0 에셋 배치 관례). 머티리얼은 `Assets/_Project/Data/Materials/`.

### 2.2 드래프트 UI

**요구 사항 (PRD 2.2 재확인)**:
- 10종 카드가 한 화면에 읽을 수 있게 노출
- 각 카드에 `displayName`, `health`, `attackRange`, `attackDamage`, `attackCooldown` 필드가 가독 가능해야 함 (툴팁 수준이어도 충분)
- 픽 후 되돌리기 (카드 재클릭으로 해제) 허용
- 현재 픽 카운트 표시 (예: `5/7`)
- 7종 픽 완료 시 "확정" 버튼 활성화
- 미픽 상태에서 확정 버튼 비활성

**비주얼 레벨**: PHASE0 3.1 "기능이 읽히는 최소한의 비주얼" 준수. UGUI 기본 컴포넌트 + TextMeshProUGUI. 카드 디자인은 정사각형 블록 + 이름 + 스탯 수치만으로 충분.

**레이아웃**: 2행 × 5열 그리드 권장 (자율 결정).

### 2.3 드래프트 로직

- **풀 구성**: 매 판 시작 시 전체 방어 유닛 SO 목록에서 10종을 **랜덤 추출**. 추출된 10종이 드래프트 풀이 된다. (PRD 2.2의 프로토타입 기본덱 정책.)
- **픽 제약**: 10종 중 7종. 중복 픽 금지. 픽 순서는 자유.
- **시드 관리** (자율 결정): H1 측정 비교 가능성을 높이기 위해 동일 AttackDeck + 동일 시드 = 동일 드래프트 풀 재현을 권장. 시드 생성 방식(Epoch 기반 / 결정적 카운터 / AttackDeck 기반)은 에이전트가 결정하고 `phase1-decisions.md`에 기록.
- **확정 이벤트**: 드래프트 UI가 확정되면 `DraftSession.Picked`가 결정됨. 이 결과가 `BattleBridge.defenderPool`에 주입되어 Phase 0의 배치 경로가 재사용된다.

### 2.4 배치 변경 (Phase 0 통합 지점)

Phase 0 `BattleBridge.PlaceDefender`의 로직은 그대로 유지되며, **`defenderPool` 필드에 DraftSession.Picked 7종이 주입**되는 것으로 교체된다:

- Phase 0: `defenderPool = [Archer, Guardian, Cannon]` (인스펙터 고정)
- Phase 1: `defenderPool`이 DraftSession으로부터 런타임 주입. 클릭당 랜덤 선택 로직은 동일.

**주입 경로**: DraftSession → (MonoBehaviour gate, e.g. DraftController) → `BattleBridge.SetDefenderPool(DefenderUnitData[] pool)` 메서드 (신규). 인스펙터 고정 값은 폴백/디버그용으로 유지 가능.

### 2.5 로깅 확장 (BattleLogSchema v2)

`Assets/_Project/Scripts/Logging/BattleLogSchema.cs` 에 `DraftRecord` 섹션 추가:

```csharp
[Serializable]
public class DraftRecord
{
    public List<string> pool = new();    // 10종 SO displayName 또는 id
    public List<string> picked = new();  // 7종 SO displayName 또는 id (픽 순서)
    public int seed;                     // 재현 가능성 키
}

// BattleLogEntry에 필드 추가:
public DraftRecord draft = new();
```

`BattleLogger`에 `SetDraft(DraftRecord draft)` 메서드 추가. `BattleBridge.StartBattle` 또는 DraftController가 호출하여 현 세션의 드래프트 결과를 기록.

### 2.6 재플레이 흐름

Phase 0 `BattleBridge.RestartBattle`을 확장:

- **"다시 시작"** 버튼 → 현 DraftSession.Picked 유지한 채 teardown + 재시작. 로그 `draft` 필드는 동일 내용으로 유지.
- **"다른 픽으로"** 버튼 → DraftSession 리셋 + 드래프트 UI 재오픈. 확정 시 새 pool 주입 + 재시작.

두 버튼 모두 `ResultScreen` 하위에 배치 (UGUI Button). 각 버튼에 별도 이벤트(`RestartRequested`, `RedraftRequested`)를 노출.

---

## 3. 종료 조건 (Done Criteria)

Phase 1은 다음이 **모두** 이진 통과해야 Phase 2로 이동한다.

### 3.1 기능 이진 체크 (작업 순서)

의존 관계 순으로 정렬. Phase 0 원칙 유지:
- 한 번에 한 작업만.
- 각 작업 완료 시 사용자 통과 확인 요청.
- 결정 사항은 `phase1-decisions.md` 에 한 줄씩 누적 기록.

---

**[P1-01] 방어 유닛 풀 10종 확장**
- [x] `Assets/_Project/Data/Defenders/` 에 `DefenderUnitData` SO 10개 존재 (기존 3 + 신규 7)
- 내용: 섹션 2.1 지침에 따라 7종 추가. 각 SO에 고유한 머티리얼(색) + 스탯. `Wassup/DefenderUnit` 메뉴를 통해 생성. 기존 3종 스탯은 유지 권장.
- 선행: Phase 0 완료
- 완료 확인: 에디터에서 10개 SO 전부 Inspector로 열람 시 스탯 읽힘, 머티리얼 색상 10종 구분 가능

**[P1-02] DraftSession + DraftController 골격**
- [x] `Core/DraftSession` 클래스 (MonoBehaviour 또는 POCO)에 `pool`, `picked`, `seed` 상태 보관
- [x] `Core/DraftController` MonoBehaviour가 DraftSession 생성·확정 이벤트 처리
- 내용: 10종 풀 랜덤 추출, 픽/언픽 로직, 확정 이벤트 노출. 이 시점은 UI 없이 로직만.
- 선행: P1-01
- 완료 확인: 테스트 코드 또는 execute_code로 DraftSession이 10 pool + 7 pick 상태 전이 가능 확인

**[P1-03] 드래프트 UI**
- [x] 10개 카드(UGUI)가 한 화면에 표시되고 displayName + 핵심 스탯 읽을 수 있음
- [x] 카드 클릭으로 픽/언픽 토글
- [x] 현재 카운트 "n/7" 표시 + 7 도달 시 "확정" 버튼 활성화
- 내용: 섹션 2.2 요구사항. 카드 프리팹 또는 런타임 생성. EventSystem(P0-09에서 추가됨) 재사용.
- 선행: P1-02
- 완료 확인: 에디터 Play에서 카드 클릭·확정 흐름이 DraftController 이벤트까지 연결

**[P1-04] DraftSession → BattleBridge 주입**
- [x] `BattleBridge.SetDefenderPool(DefenderUnitData[])` 메서드 추가
- [x] DraftController가 확정 시 BattleBridge에 pool 주입 후 StartBattle 호출
- [x] 드래프트 확정 전에는 배치 불가 (BattleBridge._running=false 유지)
- 내용: Phase 0의 defenderPool 인스펙터 고정 값을 런타임 주입으로 교체. 인스펙터 고정 값은 drag-in 폴백으로 남겨 둘 수 있음(SetDefenderPool 미호출 시 인스펙터 값 사용).
- 선행: P1-03
- 완료 확인: 드래프트 확정 후 배치 시 picked 7종 중에서만 랜덤 선택 — 색깔 관찰로 확인

**[P1-05] 재시작 플로우 (같은 픽 유지)**
- [x] 결과 화면 "다시 시작" 버튼으로 같은 pick 유지한 채 재진행
- 내용: Phase 0 `RestartBattle`은 그대로 유지 + DraftController가 pool 캐시를 건드리지 않도록 연결. teardown 후 StartBattle 시 주입된 pool 재사용.
- 선행: P1-04
- 완료 확인: 1판 → 결과 → 다시 시작 → 같은 7종으로 재진행 가능

**[P1-06] 재시작 플로우 (다른 픽으로 재도전)**
- [x] "다른 픽으로" 버튼이 드래프트 UI 재오픈, 확정 후 새 pool로 재진행
- 내용: ResultScreen에 두 번째 버튼 추가 (`RedraftRequested` 이벤트). DraftController가 이벤트 받아 DraftSession 리셋 + UI 재표시.
- 선행: P1-05
- 완료 확인: 2판 이상에서 각 판의 pick이 달라질 수 있고 로그에 반영됨

**[P1-07] 로깅 확장**
- [x] `BattleLogSchema.BattleLogEntry.draft` 필드가 pool·picked·seed 값으로 채워짐
- 내용: 섹션 2.5 `DraftRecord` 추가. `BattleLogger.SetDraft` + `BattleBridge.StartBattle` 또는 DraftController 확정 시점에 호출. 기존 session JSON에 `draft` 블록이 추가된다.
- 선행: P1-04
- 완료 확인: `/Users/sy/dev/wassup/GameLogs/` 세션 JSON에 `draft.pool` 10개, `draft.picked` 7개 들어감

**[P1-08] EditMode 테스트 확장**
- [x] DraftSession 단위 테스트 (pool 10종 추출, 7픽 제약, 언픽 토글, 확정 플래그)
- [x] 기존 6개 테스트 회귀 없음
- 내용: `Assets/_Project/Tests/EditMode/DraftSessionTests.cs` 신규. run_tests MCP로 통합 테스트 승인.
- 선행: P1-02
- 완료 확인: run_tests에서 기존 6 + 신규 n건 전부 pass

**[P1-09] Phase 0 회귀 체크**
- [x] Phase 0 13개 기능 체크(P0-01 ~ P0-13) 중 Editor에서 검증 가능한 것 전부 정상 동작
- 내용: 드래프트 도입이 Phase 0 기능을 깨뜨리지 않았는지 수동 플레이로 확인. 맵 표시 / 배치 / 자동 공격 / VICTORY · DEFEAT / 재시작 / 로그 파일 생성 각각 재현.
- 선행: P1-06, P1-07
- 완료 확인: 수동 플레이 완주 + 로그 파일 적재 확인

**[P1-10] Android 실기기 검증 (P0-13 흡수)**
- [ ] Android 빌드가 실기기에서 드래프트 → 배치 → 전투 → 결과 전 과정 정상 동작
- 내용: Android Build Target 전환, SDK/JDK 모듈 확인, APK 빌드, 실기기 배포, 터치 드래프트 + 터치 배치 + 재시작 동작 확인. Input System 기반이므로 터치는 `Pointer.current`가 자동 대응.
- 선행: P1-09
- 완료 확인: 실기기에서 한 판 완주 + 재시작 1회 이상

---

**작업 완료 시점**: 위 10개 작업의 체크박스가 모두 true가 되면 섹션 3.2(아키텍처 이진 체크) + 섹션 3.3(주관 평가 게이트)로 진행.

### 3.2 아키텍처 이진 체크

Phase 0 섹션 3.2 항목을 전부 재확인 + Phase 1 전용 항목 추가.

**Phase 0 재확인 (회귀 방지)**
- [x] ECS 시스템이 전투 로직을 계속 소유
- [x] UI / 터치 / 결과 화면은 MonoBehaviour
- [x] `BattleBridge`가 유일한 MonoBehaviour ↔ ECS 창구
- [x] `EntityManager` / `World.DefaultGameObjectInjectionWorld` 사용은 `BattleBridge` 한 파일에만 존재
- [x] SubScene 미사용 / 네트워크 코드 전무
- [x] 맥락 분리 유지 (Units / Movement / Combat / Effects)
- [x] Effects 맥락은 여전히 자리만 (Phase 2 이후)

**Phase 1 전용**
- [x] 드래프트 로직이 MonoBehaviour 레이어에 있음 (ECS 오염 금지)
- [x] DraftSession / DraftController가 BattleBridge 경계를 침범하지 않음 (EntityManager 직접 접근 없음)
- [x] 신규 방어 유닛 7종은 전부 SO — 스탯 하드코딩 없음
- [x] 드래프트 UI 카드 수/픽 수 같은 수치가 상수 또는 SO 기반 (매직 넘버 최소화)

### 3.3 주관 평가 게이트

Phase 1의 핵심 질문: **반복 플레이에서 픽이 수렴하는가.**

- 3~5명 플레이어가 동일 AttackDeck(WaveA) + 동일 풀 시드에 대해 **최소 10판 반복 플레이**
- 수집 지표:
  - 첫 3판 vs 마지막 3판의 **픽 Jaccard 유사도**
  - 첫 3판 vs 마지막 3판의 **평균 스코어(생존 시간 또는 enemies_reached_goal)**
  - 플레이어 자가 보고: "픽을 바꾼 이유는?"
- 통과 기준: 픽 분포가 수렴 경향(유사도 상승) + 스코어 유의미한 개선. 상세 분석 방법은 PRD 섹션 4.1 재확인.

**실패 경로** (PRD 부록 A.H1):
- 헤드리스 시뮬레이션으로 픽 간 결과 차이 존재 여부 재확인
- 유닛 간 트레이드오프 재설계
- 공격 패턴이 모든 픽에 동일 반응하는지 검증
- 위 모두 OK인데도 실패라면 **드래프트 기반 비동기 경쟁 디펜스라는 설계 자체 재검토**

---

## 4. 에이전트 자율 결정 영역

다음은 **구현 에이전트의 재량**이다. 단, TRD 섹션 3(추상화 규칙)과 섹션 5(금지 패턴)를 준수한다.

- 드래프트 UI 레이아웃 (그리드 / 리스트 / 슬롯)
- 카드 선택 상태 표시 방식 (체크마크 / 테두리 / 색 변경)
- 픽 취소 UX (카드 재클릭 토글 / 별도 X 버튼)
- 드래프트 시드 관리 방식 (에포크 / 결정적 카운터 / 해시)
- `DraftSession` 형태 (MonoBehaviour / POCO / ScriptableObject 인스턴스)
- `DraftController`와 기존 `GameManager` 관계 (병치 / 자식 / 새 싱글톤 **금지** — GameManager가 유일 싱글톤)
- 신규 7종 방어 유닛 구체 스탯 수치
- "다른 픽으로" 버튼 레이블 및 배치
- `DraftRecord`의 SO id 표기 (displayName / GUID / asset path)
- 드래프트 UI를 BattleScene에 배치할지 전용 DraftScene을 만들지 (Phase 0 단일 씬 결정과의 일관성 — 권장: BattleScene 내 패널 토글)

**결정 원칙**: 애매하면 **단순한 쪽**. Phase 1은 H1 검증용이지 최적 UX 설계용이 아니다.

---

## 5. Phase 1 종료 시 에이전트 산출물

- 동작하는 Unity 6 프로젝트 (에디터 + Android 실기기 빌드 가능 상태)
- Phase 0에서 도입한 EditMode 테스트에 DraftSession 테스트 추가 (최소 3건)
- `phase1-decisions.md` — 섹션 4 자율 결정 항목들을 누적 기록
- 드래프트 결과가 포함된 JSON 로그 샘플 3개 이상 (`GameLogs/` 확인)
- Phase 2에서 재활용될 핵심 타입 정리 (특히 `DraftSession`, `BattleLogSchema.DraftRecord`)

---

## 6. Phase 1 이후 Phase 순서 (참고용)

참고: `TRD.md` 섹션 6.3~6.5 및 `PHASE0.md` 섹션 6과 동일 — Phase 2(스킬) → Phase 3(배치 시 효과 / 인접 시너지) → Phase 4(마무리: 3분 타이머, 봇 스코어, 측정 프로토콜 통합).

Phase 1 종료 후 `PHASE2.md`를 작성한다. 직전 Phase 완료 전에 미리 작성하지 않는다.

---

## 7. TRD 금지 패턴의 Phase 1 재적용

TRD 섹션 5의 금지 패턴은 전부 Phase 1에도 유효하다. 특히 주의:

- **드래프트 로직을 ECS에 침투시키지 않는다** — DraftSession은 전적으로 MonoBehaviour 레이어. BattleBridge 경유 주입만.
- **새 싱글톤을 만들지 않는다** — DraftController는 `GameManager`와 동급 혹은 자식 MonoBehaviour이되 별도 Instance 정적 필드를 갖지 않는다.
- **"나중을 위한" 인터페이스 / 추상화 금지** — 픽 전략이 1개뿐이므로 `IDraftStrategy` 같은 인터페이스는 만들지 않는다.
- **수치 하드코딩 금지** — 픽 개수(7), 풀 크기(10)가 설계 상수라면 `DraftConfig` SO 또는 상수 파일에 명시. 매직 넘버 리터럴을 UI/로직 여기저기 흩뿌리지 않는다.
- **Assembly Definition 추가 분리 금지** — `Wassup.Runtime` + `Wassup.Tests.EditMode` 2개 체제 유지.

---

## 8. 구현 결과 스냅샷 (2026-04-19)

Phase 1은 현재 구현 완료 상태다. 확정/구현된 세부 결정은 과거 `phase1-decisions.md` 에 기록되었고, 본 문서가 Phase 1 스펙의 단일 출처다.

- `DraftSession` 이 10종 pool, 7종 picked, seed 기반 상태 전이를 보관한다.
- `DraftController` / `DraftView` 런타임 UI로 카드 선택, 확정, Restart, Redraft 흐름을 처리한다.
- 전투 진입 후 배치는 picked 7종 pool에서만 선택된다.
- Restart 는 같은 picked pool을 유지하고, Redraft 는 드래프트 UI를 다시 열어 새 pool을 확정한다.
- JSON 로그에는 draft pool/picked 정보가 포함된다.
- Android 실기기 검증은 이후 residual 검증으로 유지한다.

**문서 버전**: v1.0 (구현 스펙 통합)
**상태**: 구현 완료. Android 실기 검증은 후속 residual 검증으로 관리.

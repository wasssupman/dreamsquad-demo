# wassup — Defense Tournament Prototype

## 프로젝트 정체성
**드래프트 기반 비동기 경쟁 타워 디펜스**의 프로토타입. 풀 게임이 아니라 **3개 가설 검증용 실험 장치**. 재미있는 게임을 만드는 것이 목표가 아니라, 검증이 실패하면 폐기·재검토하는 것이 목표다.

### 검증 가설 (모든 구현 판단의 최상위 기준)
- **H1** — 반복 플레이로 드래프트 픽 선택이 개선되는가 (*실패 시 프로젝트 전면 재검토*)
- **H2** — 코스트 제약 하 배치/스킬 결정이 3분 루프의 실시간 긴장감을 주는가
- **H3** — 플레이어가 패배 원인을 구체적으로 언어화할 수 있는가

구현 중 판단이 모호하면 **"H1/H2/H3 중 무엇에 가장 도움 되는가"**로 결정한다 (TRD §9).

### 상위 문서
- `docs/PROTOTYPE_PRD.md` — 검증 가설, 스코프, 실패 대응, 비목표 목록 (**무엇/왜**)
- `docs/TRD.md` — 기술 제약, 아키텍처 경계, 금지 패턴, Phase 순서 (**어떤 제약 위에서**)
- `docs/PHASE0.md` — 현재 Phase 상세 (추후 `PHASE1.md`, `PHASE2.md`, ... 순차 추가)

모든 구현 호출은 **TRD + 현재 Phase 문서**를 컨텍스트로 동반해야 한다.

---

## 현재 상태
- **Phase**: Phase 0 (실시간 디펜스 루프) — 진입 준비 중
- **커스텀 스크립트**: 없음 (새로 작성 시작)
- **Git**: 초기 커밋 완료

---

## 기술 스택 (TRD §1)
| 항목 | 값 |
|---|---|
| 엔진 | Unity `6000.3.5f2` (Unity 6) |
| 언어 | C# |
| 아키텍처 | **하이브리드 ECS** (전투만 ECS, 나머지 MonoBehaviour) |
| 렌더 파이프라인 | URP `17.3.0` (Mobile/PC 프리셋 분리) |
| 입력 | New Input System `1.17.0` — `Assets/InputSystem_Actions.inputactions` |
| 주 타겟 | **Android 단일 플랫폼** (최소 API 26) |
| 보조 타겟 | Unity Editor Play Mode |

### 금지 패키지 (TRD §1.2, §4.4)
NGO, Mirror, Photon, DOTween, Zenject, UniRx, MessagePipe, SubScene. 필요하다고 느껴지면 **설계를 의심할 것**.

---

## 아키텍처 경계 (TRD §2)

### ECS 월드 = 전투 시뮬레이션만
- 방어/공격 유닛 엔티티, 투사체, 이동 시스템, 데미지/사거리/시너지 계산, 스킬 전투 효과
- 작성 우선순위: **ISystem + Burst** → `SystemBase`는 C# 참조가 불가피할 때만

### MonoBehaviour = 그 외 전부
- 게임 상태 기계 (`Draft / Placement / Combat / Result`)
- 모든 UI (UGUI / TextMeshPro), 입력, 씬, 로깅, ScriptableObject 데이터 정의

### `BattleBridge` = 경계의 유일한 창구
ECS ↔ MonoBehaviour 사이 통신은 **반드시 `BattleBridge`를 경유**한다. 외부에서 `EntityManager`, `World.DefaultGameObjectInjectionWorld`, `SystemAPI` 직접 접근은 **금지 (위반 시 리팩토링 대상)**.

`BattleBridge`의 4책임:
1. 전투 시작 (MonoBehaviour 데이터 → ECS 엔티티 생성)
2. 커맨드 전달 (플레이어 입력 → ECS 커맨드 구조체)
3. 결과 수집 (전투 종료 시 결과 반환 + 월드 정리)
4. 이벤트 pull (ECS `NativeQueue` → 매 프레임 poll → 로거 전달)

그 외의 책임을 `BattleBridge`에 넣지 않는다. 비대해지면 경계가 샜다는 신호.

### 데이터 경로
- 유닛/공격 패턴/스킬 = **ScriptableObject**
- 런타임에 **Baker 패턴**으로 ECS Component 변환 (SubScene 금지)
- 렌더링은 Entities Graphics의 `RenderMeshArray` 기본 (최대 50~100 유닛 기준 충분)

---

## 폴더 구조
```
Assets/
  Scripts/
    Core/      # GameManager, GameStateMachine, 공통 타입
    Bridge/    # BattleBridge 및 경계 통신 구조체
    Battle/    # ECS Components / Systems / Jobs (전투 로직)
    UI/        # MonoBehaviour UI 컴포넌트
    Data/      # ScriptableObject 정의 클래스 (유닛/패턴/스킬)
    Logging/   # Logger, JSON 스키마
  Data/        # ScriptableObject 애셋 파일 (.asset)
  Scenes/      # Prototype.unity (주 작업 씬)
  Prefabs/
  Settings/    # URP 에셋 (기존 유지)
  Tests/
    EditMode/
    PlayMode/
  InputSystem_Actions.inputactions
Packages/
ProjectSettings/
docs/          # PRD / TRD / PHASE{N}.md
```

**Assembly Definitions**: Phase 0~1에서는 과도한 분리 금지. Phase 3 이후 필요성이 드러나면 분리.

**씬 전략**: 단일/멀티 씬은 구현 재량. `Assets/Scenes/Prototype.unity`를 주 작업 씬으로 사용.

---

## 코딩 규약
- **네임스페이스**: `Wassup.<Feature>` — 예: `Wassup.Core`, `Wassup.Bridge`, `Wassup.Battle`, `Wassup.UI`, `Wassup.Data`, `Wassup.Logging`
- MonoBehaviour 파일명 = 클래스명 = `.cs` 파일명
- 필드: `private` → `_camelCase` / `[SerializeField]` → `camelCase` / `public` → `PascalCase`
- **ECS Component struct의 public 필드는 정상** (예외)
- 입력: `InputSystem_Actions` 액션 맵 사용 — 레거시 `Input.GetKey` 금지
- 셰이더: URP만 (Built-in 셰이더 사용 시 핑크 깨짐)

---

## 금지 패턴 (TRD §4)

### 경계 위반
- `BattleBridge` 외부에서 `EntityManager` / `World` / `SystemAPI` 직접 호출
- ECS 영역을 UI / 드래프트 / 결과 화면 등 경계 밖으로 확장
- UI 로직이 ECS Component를 직접 읽거나 쓰는 것

### 아키텍처 악취
- `XxxManager` 싱글톤 남발 (`GameManager` 1개만 허용)
- MonoBehaviour에 전투 로직 직접 작성
- `SystemBase` 남발 (ISystem 우선)
- ECS Component 10개 넘기 전에 재검토
- **"나중을 위한" 인터페이스 / 추상화 / 확장 포인트**
- enum + switch 떡칠 (ECS는 Tag Component로 대체)

### 데이터 관리
- 하드코딩된 수치 (매직 넘버) — 모든 수치는 ScriptableObject
- `public` 필드 남발 (ECS struct 외에는 `[SerializeField] private`)

### 스코프 침범
- PRD §0의 **명시적 비목표** (세션/에고/컬렉션덱/가챠/BM/튜토리얼/네트워킹/카메라 회전 등)를 건드리는 것
- 현재 Phase 범위를 넘는 시스템을 "미리 만들어두는" 것
- 다음 Phase를 위한 훅/인터페이스를 **미리** 까는 것

---

## Phase 순서 (TRD §6)

| Phase | 핵심 | 상태 |
|---|---|---|
| **0** | 실시간 디펜스 루프 (타임라인 공격 + 랜덤 D&D 배치 + 자동 전투) | 진입 준비 |
| 1 | 드래프트 (10→7픽) | 미착수 |
| 2 | 스킬 2종 + 코스트 | 미착수 |
| 3 | 배치 시 효과 / 인접 시너지 | 미착수 |
| 4 | 3분 타이머, 봇 스코어, 측정 프로토콜 | 미착수 |

**Phase 이동 규칙**: 각 Phase의 **종료 조건 이진 통과**만으로 이동. 시간 기반 판단 금지 ("몇 주 지났으니 다음"). 주관 평가 게이트가 있으면 실패 시 **현재 Phase 재조정**, 다음으로 가지 않는다.

### Phase별 공통 산출물 (TRD §7)
1. 동작하는 Unity 프로젝트 (Editor + Android 빌드 양쪽)
2. EditMode 테스트 ≥1개 + PlayMode 테스트 ≥1개
3. `docs/phase{N}-decisions.md` — 해당 Phase에서 내린 기술 결정과 근거
4. 로그 스키마가 실제 파일로 출력되는지 확인

---

## 로깅 (TRD §3.4, PRD §2.11)
- **Phase 0부터 로깅 존재**. 구현의 마지막이 아니라 첫 축.
- ECS 시스템 → `NativeQueue` 이벤트 쌓기 → MonoBehaviour Logger 매 프레임 poll → JSON 파일
- 스키마는 PRD §2.11 기준, Phase별로 확장
- 네트워크 전송 없음. 로컬 파일만. **이 로그가 H1 검증의 유일한 정량 소스**

---

## 의사결정 순서 (TRD §9)
구현 중 모호하면 다음 순서로 해결:
1. 현재 **Phase 문서**에 답이 있는가
2. **TRD**에 답이 있는가
3. **PRD**에 답이 있는가
4. 없으면 **H1/H2/H3에 가장 도움 되는 선택**
5. 그래도 모호하면 **단순한 쪽**
6. 여전히 모호하면 `phase{N}-decisions.md`에 "결정 필요 항목"으로 남기고 진행

사용자 질문이 필요하면 **여러 결정을 묶어서 한 번에** 질문. 작업 단위마다 묻지 않는다.

---

## 자주 쓰는 작업
- **씬 실행**: `Assets/Scenes/Prototype.unity` 열고 Play
- **빌드 타깃**: Mobile/PC 각각 별도 URP Renderer 에셋 — Quality Settings와 일치시킬 것
- **패키지 추가**: `Packages/manifest.json` 편집 후 Unity 재열기

---

## 버전 민감 키워드 — 기억 대신 검색
학습 데이터의 버전은 낡았을 확률이 높음. 아래 키워드 등장 시 **context7 MCP → WebSearch → `Packages/manifest.json`** 순으로 확인.

- DOTS: `ECS`/`Entities`, `Burst`, `Jobs`, `Collections`, `Mathematics`
- 렌더: `URP` 17, `Entities Graphics`, `Shader Graph`
- 입력/UI: `Input System` 1.17, `UGUI`, `TextMeshPro`

예외: 사용자가 버전 지정한 경우 또는 이미 설치된 버전 유지 작업.

---

## 주의 사항
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `ProfilerCaptures/`는 생성물 — **편집/커밋 금지**
- `.meta` 파일은 항상 짝 에셋과 함께 다룬다 (삭제/이동 시 둘 다)
- Unity Editor가 실행 중일 때 `Library/` 내부 건드리지 말 것 (도메인 리로드 충돌)
- 검증 가설(H1/H2/H3)에 기여하지 않는 작업은 **기각 대상**

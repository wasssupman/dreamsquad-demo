# TRD — Defense Tournament Prototype

> **Demo 기술 제약 정본.** 이 문서는 Hybrid ECS 경계와 Demo 금지 패턴의 기준이다. `CLAUDE.md` 또는 owner가 승인한 활성 Demo spec이 명시적으로 대체하기 전까지 계속 구속력을 가진다. `docs/production-transition/`은 owner-gated dormant downstream이며 이 제약을 완화하거나 ECS 제거·네트워크 구현의 근거로 사용할 수 없다.
>
> **Technical Requirements Document.** 프로토타입의 기술 스택, 아키텍처 경계, 제약, 금지 패턴, Phase 순서를 단일 참조 문서로 모은다. 이 문서는 에이전트 호출 시 **매번 컨텍스트로 함께 전달되는 문서**이며, 개별 Phase 문서(`PHASE0.md` 등)와 함께 읽힌다.
>
> PRD(`PRD.md`)가 "왜 만드는가 / 무엇을 검증하는가"를 다룬다면, 이 문서는 **"어떤 제약 위에서 만드는가"**를 다룬다. 기술 세부 설계(클래스 다이어그램, 시퀀스 등)는 포함하지 않으며, 그 영역은 구현 에이전트의 재량이다.

---

## 0. 이 문서의 역할

- **누가 읽는가**: 구현 에이전트(Superpowers 등), 팀 개발자
- **언제 읽는가**: 모든 구현 호출 시작 시점. 단일 작업마다 이 문서가 프롬프트 컨텍스트에 포함된다.
- **무엇을 포함하는가**: 기술 스택 / 아키텍처 경계 / 제약 / 금지 패턴 / Phase 순서
- **무엇을 포함하지 않는가**: 검증 가설(PRD), 스코프 이유(PRD), 세부 클래스 설계(에이전트 재량), Phase별 상세 구현 범위(각 Phase 문서)

**관련 문서 구조**:

```
PRD.md   — 검증 가설, 스코프, 실패 대응, 검증 프로토콜
TRD.md (이 문서)    — 기술 제약, 아키텍처 경계, 금지 패턴, Phase 순서
PHASE0.md          — Phase 0 상세 스펙과 종료 조건
PHASE1.md          — (Phase 0 완료 후 작성)
PHASE2.md          — (Phase 1 완료 후 작성)
...
```

---

## 1. 기술 스택

### 1.1 엔진 / 언어 / 플랫폼

| 항목 | 결정 |
|---|---|
| 엔진 | **Unity 6** (최신 정식 버전) |
| 언어 | **C#** |
| 아키텍처 | **하이브리드 ECS** (전투 시뮬레이션만 ECS, 나머지 MonoBehaviour) |
| 주 타겟 | **Android** 실기기 |
| 보조 타겟 | Unity Editor 플레이 모드 (일상 개발), iOS Ad Hoc 내부 QA 빌드 |
| 최소 API 레벨 | Android 8.0 (API 26) 또는 그 이상 |

**Android 우선 원칙.** 게임 기능과 런타임 아키텍처는 Android 안정성을 최우선으로 하며,
iOS 지원은 `com.playlinks.somnia.dev`를 사용하는 수동 Ad Hoc 내부 QA 빌드와 실기기 smoke로
한정한다. 이 예외는 iOS 전용 게임 기능, 범용 멀티플랫폼 추상화, App Store 제출,
Windows/WebGL 지원을 허용하지 않는다.

### 1.2 필수 패키지

다음 패키지만 사용한다. 이 목록에 없는 패키지는 플랜 내 채택 근거가 명시되지 않으면 금지.

| 패키지 | 버전 | 용도 |
|---|---|---|
| Entities | 6.4.0 (Unity 6000.4.x 내장 패키지) | ECS 프레임워크 |
| Entities Graphics | 6.4.0 (Unity 6000.4.x 내장 패키지) | ECS 엔티티 렌더링 |
| Burst | 최신 호환 | ECS 시스템 Burst 컴파일 |
| Collections | 최신 호환 | NativeArray, NativeQueue 등 |
| Mathematics | 최신 호환 | int2, float3 등 수학 타입 |
| Jobs | (Entities/Collections 번들) | IJob, IJobParallelFor — 별도 `com.unity.jobs` 패키지 없음, Unity.Jobs 네임스페이스는 Collections/Entities 스택으로 제공 |
| TextMeshPro | UGUI 2.x 번들 | UI 텍스트 (Unity 6에서 별도 패키지 없이 UGUI에 포함) |
| Test Framework | 최신 호환 | EditMode/PlayMode 테스트 |

**금지 패키지**: NGO (Netcode for GameObjects), Mirror, Photon, DOTween, Zenject, UniRx, MessagePipe. 프로토타입 단계에 이들의 기능이 필요하다고 느껴지면 설계를 의심할 것.

### 1.3 개발 도구 (Unity MCP)

본 프로젝트는 **Unity MCP**를 개발 도구로 사용한다. 에이전트는 Unity Editor와의 상호작용을 Unity MCP 툴을 통해 수행한다.

**주요 용도**:
- 스크립트 생성·수정·삭제 (`manage_script`, `create_script`, `apply_text_edits`, `script_apply_edits`)
- 씬 / GameObject / 컴포넌트 관리 (`manage_scene`, `manage_gameobject`, `manage_components`, `find_gameobjects`)
- 프리팹·에셋·ScriptableObject 관리 (`manage_prefabs`, `manage_asset`, `manage_scriptable_object`)
- 에디터 상태 제어 (`manage_editor`: 플레이 모드, 태그/레이어 관리 등)
- 빌드 (`manage_build`: Android 빌드 및 iOS Xcode 프로젝트 export 포함)
- 콘솔·컴파일 확인 (`read_console`, `editor_state` 리소스의 `isCompiling`)
- 테스트 실행 (`run_tests`, `get_test_job`)
- 패키지 관리 (`manage_packages`)
- Unity 공식 문서 조회 (`unity_docs`)

**작업 원칙**:
- 스크립트 생성/수정 후 `read_console`로 컴파일 오류를 **반드시 확인**한 뒤 다음 단계로 진행한다. 도메인 리로드는 `editor_state.isCompiling`으로 폴링한다.
- 에디터 상태를 **읽을 때는 리소스**(`editor_state`, `project_info` 등), **바꿀 때는 툴**을 사용한다.
- 경로는 `Assets/` 하위 기준 상대 경로, 슬래시는 `/`로 통일한다.
- Unity MCP는 **에이전트의 작업 수행 도구**이며, 런타임 코드(`Assets/_Project/Scripts/`)와는 무관하다. 빌드 산출물에는 MCP 의존성이 들어가지 않는다.

Unity MCP가 사용 불가능한 환경에서도 프로젝트는 수동 조작으로 동일하게 작업 가능해야 한다 — MCP는 **편의 도구이지 의존성이 아니다**.

---

## 2. 아키텍처 경계

### 2.1 원칙 — 하이브리드 ECS 전략

본 프로젝트는 **ECS를 유닛 이동·전투 시뮬레이션에만 사용**한다. 그 외 모든 레이어(UI, 입력, 게임 상태, 로깅, 씬)는 전통적 MonoBehaviour + ScriptableObject 방식이다. 이 경계는 프로젝트 내내 고정되며, 개발 중 "이것도 ECS로 옮기면 좋겠다"는 판단은 기본적으로 기각한다.

**ECS 선택의 이유**: 본 게임은 20×10 그리드에서 수십~수백 유닛이 동시에 움직이는 구조이고, 헤드리스 시뮬레이션으로 밸런스를 튜닝할 계획이 있다. Burst 호환 순수 함수 + Job으로 작성된 전투 코드는 이 두 요구를 자연스럽게 만족시킨다.

**ECS 범위를 제한하는 이유**: ECS는 강력하지만 전 계층에 적용하면 UI·입력·씬 관리에서 불필요한 복잡도가 생긴다. 전투 시뮬레이션만 ECS에 두면 각 계층이 자기에게 맞는 패러다임을 쓸 수 있다.

### 2.2 ECS 월드에 속하는 것

- 방어 유닛 엔티티 (배치 후의 상태, 스탯, 쿨다운, 타겟팅)
- 공격 유닛 엔티티 (경로 이동, 체력, 상태이상)
- 투사체 엔티티 (있는 경우)
- 전투 계산 시스템 (데미지 적용, 사거리 체크) — 인접 시너지는 2026-09-03 기능 은퇴
- 이동 시스템 (공격 유닛의 경로 따라가기)
- 스킬의 전투 효과 적용 시스템 ← Phase 2 이후
- ECS 월드 내부의 시간축 (`SystemAPI.Time`)

### 2.3 MonoBehaviour에 속하는 것

- 게임 상태 기계 (`Draft / Placement / Combat / Result`)
- 드래프트 UI 및 로직 ← Phase 1 이후
- 배치 UI 및 터치/마우스 입력 처리
- 결과 화면, 스코어 표시
- 로깅 시스템 (JSON 출력)
- 씬 구성, 씬 전환
- 공격 패턴 데이터 정의 (ScriptableObject)
- 유닛 정의 데이터 (ScriptableObject, 런타임에 ECS Component로 변환)
- 모든 UGUI / TextMeshPro

### 2.4 경계 통신 규칙

**`BattleBridge`가 유일한 통신 창구다.** 이외의 MonoBehaviour 코드에서 `EntityManager`, `World.DefaultGameObjectInjectionWorld`, `SystemAPI` 직접 접근은 **금지**이며 위반 시 리팩토링 대상이다.

**`BattleBridge`의 4가지 책임**:

1. **전투 시작 / 런타임 변환**: MonoBehaviour 쪽 데이터(배치된 유닛, 공격 패턴, ScriptableObject 정의 등)를 받아 ECS 엔티티와 unmanaged Component/Buffer 로 생성
2. **커맨드 전달**: 전투 중 플레이어 입력(추가 배치, 스킬 사용 등)을 ECS 커맨드 구조체로 전달
3. **결과 수집**: 전투 종료 시 결과를 MonoBehaviour 쪽으로 반환. ECS 월드 정리
4. **이벤트 pull / Presentation 반영**: ECS 시스템이 `NativeQueue` 또는 유사 구조에 쌓은 이벤트를 매 프레임 poll하여 로거, Spine/Quad view, Projectile/VFX pool 에 전달

이 4가지 외의 책임을 `BattleBridge`에 두지 않는다. `BattleBridge`가 비대해지는 것은 경계 설계가 샜다는 신호다.

**데이터 입력 방식**: 현재 런타임 전투 데이터는 `BattleBridge`가 ScriptableObject/프리팹 참조를 읽어 ECS 엔티티와 unmanaged Component/Buffer 로 수동 변환한다. Unity Entities 6.4.0 의 Baker/SubScene 흐름은 패키지 기능으로 존재하지만, 본 프로젝트는 동적 배치 구조와 단일 bridge 경계를 우선하므로 **SubScene을 사용하지 않는다**. Baker 도입은 별도 spec 에서 장단점을 검토하기 전까지 기본 경로가 아니다.

**렌더링 / Presentation**: 시뮬레이션 상태는 ECS에 두되, 유닛/적/스킬/Hazard 표현은 MonoBehaviour presentation 계층(SpineUnitPool, QuadUnitViewPool, ProjectileViewPool, VfxSpawner 등)이 담당한다. ECS는 visual event 를 `NativeQueue` 로 발행하고 `BattleBridge`가 이를 drain 해 view 를 갱신한다. Entities Graphics 6.4.0 은 health bar, 단순 mesh, 대량 인스턴싱 같은 제한된 렌더링 용도로만 사용하며, Spine/ParticleSystem/프리팹 참조를 ECS Component에 넣지 않는다.

**입력**: 터치/마우스 입력은 전적으로 MonoBehaviour 쪽에서 처리. 입력이 전투에 영향을 주는 경우 커맨드 구조체로 ECS 월드에 전달한다.

### 2.5 맥락 분리 (Context Boundaries)

ECS 월드 내부는 **맥락(Context)별로 명확히 분리**된다. 맥락이란 "무엇에 책임지는 코드인가"의 경계이며, 폴더 구조와 Component/System의 소속으로 드러난다. 이 분리는 ECS 초기 학습 곡선을 낮추고, "어느 코드가 어디에 속하는가"의 판단을 기계적으로 만든다.

#### 2.5.1 맥락 목록

| 맥락 | 책임 | Phase 0 포함? |
|---|---|---|
| **Units** | 유닛 정의, 배치 상태, 생성/소멸, Health | ✅ |
| **Movement** | 경로 따라가기, 위치 갱신, waypoint 진행 | ✅ |
| **Combat** | 타겟팅, 공격 쿨다운, 데미지 적용, 사거리 판정 | ✅ |
| **Effects** | 상태이상, 스킬 효과 | ✅ (자리만) — 인접 시너지는 2026-09-03 은퇴 |

**주의**: Effects 맥락은 Phase 0에서는 **폴더와 최소 구조만 준비**하고 실제 효과는 Phase 2(스킬) / Phase 3(인접 시너지)에서 구현된다. Phase 0에 Effects 맥락을 미리 여는 이유는, 나중에 Component 소속을 바꿀 때 ECS Archetype이 깨지는 비용을 줄이기 위해서다.

향후 추가될 가능성이 있는 맥락:
- **Collision** (Unity Physics 도입 시점. 프로토타입 범위 밖일 가능성 높음)
- **AI** (공격 유닛의 지능형 행동이 필요해질 때)

#### 2.5.2 맥락 간 통신 규약

맥락 분리가 실제로 작동하려면 다음 두 규칙이 엄수되어야 한다:

**규칙 1 — Component 소유권**

각 Component는 **소유 맥락이 있다**. 다른 맥락은 해당 Component를 **읽을 수만** 있고, 쓰기는 소유 맥락의 System만 할 수 있다.

예시:
- `Health` Component의 소유 맥락은 **Units**. Combat System은 Health를 **읽어서** "사망 판정"을 하지만, Health 값을 직접 수정하지 않는다. 데미지 적용은 `DamageEvent`를 쏘고, Units 맥락이 이를 받아 Health를 갱신한다.
- `Position`(또는 `LocalTransform`) Component의 소유 맥락은 **Movement**. Combat이 사거리를 판정할 때 위치를 읽지만, Combat System이 위치를 바꾸지 않는다.

**규칙 2 — 맥락 간 이벤트는 buffer 또는 NativeQueue**

맥락 간 통신은 Component를 직접 수정하는 대신 **이벤트 구조**로 한다:

- **Buffer Component** (엔티티에 붙는 `DynamicBuffer<T>`) — 해당 엔티티에 쌓이는 이벤트. 예: 엔티티별 `DynamicBuffer<IncomingDamage>`
- **NativeQueue / NativeList** (월드 단위) — 전역 이벤트 큐. 예: 판 단위 `NativeQueue<GoalReachedEvent>`

이 두 방식 중 어느 것을 쓸지는 상황에 따라 다르지만, **직접 Component 수정은 소유 맥락만**이라는 원칙이 깨지면 안 된다.

#### 2.5.3 폴더 구조

맥락별 폴더 분리는 Phase 0부터 적용한다. **Assembly Definition(`.asmdef`) 분리는 하지 않는다** — 프로토타입 단계에서는 단일 asmdef가 충분하며, 컴파일 시간 문제가 실제로 드러나는 Phase에서 재검토한다.

```
Assets/_Project/Scripts/
  Core/              — GameManager, GameStateMachine, 공통 타입
  Bridge/            — BattleBridge 및 경계 구조체
  Battle/
    Units/           — Units 맥락: Component, System, Job
    Movement/        — Movement 맥락
    Combat/          — Combat 맥락
    Effects/         — Effects 맥락 (Phase 0은 자리만)
  UI/                — MonoBehaviour UI
  Data/              — ScriptableObject 정의
  Logging/           — Logger, JSON 스키마
```

---

## 3. 추상화 규칙

ECS와 MonoBehaviour 양쪽에 공통 적용되는 추상화 상한선이다. 이 규칙은 "가벼운 설계 + 확장 가능 구조"의 구체적 정의다.

### 3.1 상속 제한

- **MonoBehaviour / 일반 C# 클래스**: 상속 최대 **2단계**. 즉 베이스 1개 + 구현 1개까지. `UIPanel → ResultScreen` ✓, `UIPanel → Modal → AlertModal` ✗.
- **ScriptableObject**: 상속 최대 **2단계**. `UnitData → DefenderUnitData` ✓.
- **ECS Component (struct)**: 상속 불가 (struct이므로). 대신 **Tag Component 패턴** 사용.
- **ECS ISystem**: 일반적으로 최상위. SystemBase 상속도 1단계까지만.

### 3.2 인터페이스 생성 규칙

- **구현체가 현재 2개 이상 있을 때만** 인터페이스를 만든다.
- "확장성을 위해" 인터페이스를 미리 만들지 않는다. 필요해지면 그때 추출한다.
- Phase 0 기준 유닛 타입은 Tag Component로 구분한다. `IUnit` 인터페이스 금지.

### 3.3 생성 패턴 규칙

- 팩토리/빌더 패턴은 **객체 생성이 3줄 이상**이 될 때만 도입.
- Phase 0~1에서는 대부분 직접 생성으로 충분하다.
- ECS 엔티티 생성은 현재 `BattleBridge` 내부의 직접 `EntityManager` 호출과 작은 변환 헬퍼로 충분하다. Baker/SubScene 도입은 별도 spec 없이 하지 않으며, 별도 팩토리 레이어도 금지한다.

### 3.4 이벤트 시스템 규칙

- 전역 이벤트 시스템(C# event/Action 체인)은 **Phase 2 이후**에만 도입 검토.
- Phase 0은 직접 호출 또는 ECS Buffer/NativeQueue로 충분.
- UnityEvent 금지 (Inspector 노출의 매력이 있지만 디버깅이 어려워짐).

### 3.5 제네릭 규칙

- 제네릭 타입 파라미터는 **1개까지**.
- 2개 이상 필요한 경우 설계를 의심하고 구체 타입으로 분할.

### 3.6 "확장 가능"의 정의

"확장 가능하게 만든다"는 **다음 Phase에서 즉시 쓸 확장 포인트만** 의미한다. 구체적으로:

- Phase 0에서 "드래프트용 인터페이스" 미리 만들기 — ✗ (Phase 1에서 만듦)
- Phase 0에서 "Effects 맥락 폴더 열어두기" — ✓ (Phase 0 구조에 이미 포함)
- Phase 0에서 "스킬 베이스 클래스" 미리 만들기 — ✗ (Phase 2에서 만듦)
- Phase 0에서 "여러 공격 패턴을 데이터로 정의" — ✓ (현재 Phase에서도 필요)

---

## 4. 아키텍처 원칙

### 4.1 데이터 주도

- 유닛/적/공격 패턴/스킬은 ScriptableObject 또는 프리팹으로 정의
- 런타임에 `BattleBridge` 변환 헬퍼로 ECS Component/Buffer 로 옮긴다. Baker/SubScene 기반 변환은 현재 기본 경로가 아니다.
- **하드코딩 금지**. 수치는 모두 데이터에서 나온다.
- 이유: 튜닝 빈도가 높고, 향후 헤드리스 시뮬레이션으로 자동 튜닝 예정

### 4.2 상태 명확

- 판의 상태는 enum 기반 StateMachine (`Draft / Placement / Combat / Result`)으로 명확히 분리
- StateMachine은 MonoBehaviour 쪽에 위치
- Phase 전환 이벤트는 관찰 가능해야 함 (event 또는 유사 패턴)

### 4.3 순수 로직 분리

- ECS 시스템은 가능한 한 **Burst 호환 가능한 ISystem**으로 작성
- `SystemBase`는 C# 오브젝트 참조가 필요한 경우에만 사용
- 전투 계산의 핵심 함수는 `static` 메서드 또는 `IJob` 구조로 분리 가능한 범위에서 분리
- 이유: 헤드리스 시뮬레이션에서 Unity 의존 없이 실행 가능해야 함

### 4.4 로깅 우선

- **로깅은 구현의 마지막이 아니라 첫 축**이다
- Phase 0부터 로깅 시스템이 존재해야 한다
- ECS 시스템이 `NativeQueue`에 이벤트를 쌓고, MonoBehaviour Logger가 매 프레임 poll하여 JSON 파일에 기록
- 로그 스키마는 `PRD.md` 섹션 2.11을 기준으로 하되, Phase 단계에 맞게 확장

### 4.5 테스트 가능성

- ECS 시스템은 `EntityManager` 없이도 단위 테스트 가능하게 핵심 계산 함수를 분리
- 과도한 추상화는 금지. 커버리지는 목표가 아니며, **핵심 로직의 회귀 방지 수준**이면 충분
- 실행 절차·lane 구분·수치 단언 규율은 [`docs/reference/test-procedure.md`](reference/test-procedure.md) 가 정본
  (~~각 Phase는 최소 1개의 EditMode + 1개의 PlayMode 테스트를 요구~~ 는 프로토타이핑 시절 규칙 —
  현재는 spec 작업 단위의 "완료 기준" 섹션이 그 자리를 대신한다)

---

## 5. 금지 패턴

다음은 프로젝트 전반에 걸쳐 금지된다. 구현 에이전트는 이 패턴 발견 시 스스로 거부해야 한다.

### 5.1 경계 위반

- `BattleBridge` 외부에서 `EntityManager`, `World.DefaultGameObjectInjectionWorld`, `SystemAPI` 직접 호출
- ECS 영역을 UI / 드래프트 / 결과 화면 등 경계 밖으로 확장
- UI 로직이 ECS Component를 직접 읽거나 쓰는 것
- 맥락 간 Component 쓰기 (섹션 2.5.2 규칙 1 위반)
- 맥락 간 직접 메서드 호출 (이벤트/버퍼 거치지 않음)

### 5.2 아키텍처 악취

- `XxxManager` 싱글톤 남발 지양. 명확한 단일 역할 매니저만 허용(2026-07-07 완화, CLAUDE.md §5 참조)
  - **의도된 예외: `Wassup.Core.TimeControl.TimeManager`** (도메인 스코프 시간 제어). 시간 스케일이 전투 시뮬(ECS)·BattleBridge 웨이브/타이머·전투 표현·UI 다수 계층에 걸쳐 널리 소비되어 단일 권한이 필요하다. 사용자 승인하 도입(spec `docs/spec/time-manager/`). 순수 C# 싱글턴(MonoBehaviour 아님).
  - **의도된 예외: `SoundManager`** (전역 SFX/오디오 재생). 사용자 승인하 도입(2026-07-07, spec `docs/spec/score-hud-impact-upgrade/` unit 4). 오디오는 여러 계층에서 발화되므로 단일 재생 권한이 자연스럽다.
  - 이 둘 외 새 `XxxManager` 싱글톤은 기능이 실제로 전역 권한을 요구할 때만, 애매하면 질문 후 신설.
- MonoBehaviour에 전투 로직 직접 작성 (전투는 ECS 시스템에서만)
- SystemBase 남발 (ISystem 우선)
- ECS Component 과다 (10개 이상 만들기 전에 재검토)
- "나중을 위한" 인터페이스, 추상화, 확장 포인트 (섹션 3 추상화 규칙 위반)
- enum + switch 떡칠 (단, ECS Component는 Tag Component 패턴으로 다형성을 대체)

### 5.3 데이터 관리

- 하드코딩된 수치 (매직 넘버)
- public 필드 남발 (`[SerializeField] private` 사용. 단, ECS Component struct는 public 필드가 정상)
- ScriptableObject 대신 코드 상수로 유닛 스탯 정의
- JSON 하드코딩된 경로 (공격 패턴 데이터는 SO 권장)

### 5.4 패키지 / API

- 네트워크 관련 패키지/코드 (NGO, Mirror, Photon 등)
- SubScene 사용
- 에디터 전용 API를 런타임 코드에 사용
- Burst 컴파일이 실패하는 API를 ECS 시스템에 사용
- DOTween, Zenject 등 범용 라이브러리 (근거 없으면 금지)
- `Shader.Find(...) + new Material(shader)` 패턴 금지. 모바일 빌드 shader stripping 으로 null 반환되어 렌더가 깨진다. 런타임 Material 생성은 `Wassup.Rendering.RuntimeMaterialFactory.CreateOpaque / CreateTransparent` 경유 (`Assets/Resources/RuntimeMaterials/*.mat` 로 always-included 보장). 신규 런타임 shader 가 필요하면 `Assets/_Project/Shaders/` 에 명시 shader 추가 + Resources 머티리얼 등록.

### 5.5 스코프 침범

- PRD 섹션 0의 "명시적 비목표" 목록을 건드리는 것
- 현재 Phase의 범위를 넘어서는 시스템을 "미리 만들어두는" 것
- 다음 Phase를 위한 훅/인터페이스를 미리 까는 것 (단, 맥락 폴더 자체는 Phase 0에 미리 열어두는 것 허용 — 섹션 2.5.1 참조)

---

## 6. Phase 순서

프로토타입은 다음 순서로 진행된다. **각 Phase는 직전 Phase의 검증을 전제**로 한다. 앞 Phase가 실패하면 뒷 Phase는 무의미하다.

| Phase | 이름 | 핵심 검증 | 상세 문서 |
|---|---|---|---|
| 0 | 실시간 디펜스 루프 | 실시간 루프가 재미있는가 / 작동하는가 | `PHASE0.md` |
| 1 | 드래프트 | 드래프트 선택이 의미 있는가 (H1 선제) | 미작성 |
| 2 | 스킬 | 스킬 사용 결정이 긴장감을 추가하는가 | 미작성 |
| 3 | 배치 시 효과 / 인접 시너지 | 배치 판단의 깊이가 생기는가 | 미작성 |
| 4 | 마무리 | 3분 타이머, 봇 스코어, 결과 비교, 측정 프로토콜 | 미작성 |

### 6.1 Phase 0 — 실시간 디펜스 루프

- 정해진 타임라인에 따라 공격 유닛이 패턴화되어 경로를 통해 종점까지 이동
- D&D 제스처로 배치 타일에 랜덤 방어 유닛 배치
- 방어 유닛이 공격 유닛을 자동 공격
- 종점 도달 N마리 시 패배, 전멸 시 승리
- 드래프트·스킬·시너지·코스트 없음
- 상세: `PHASE0.md`

### 6.2 Phase 1 — 드래프트

- 10종 풀에서 7종 픽. Phase 0의 랜덤 배치가 "드래프트한 7종 중 사용자가 고른 것"으로 교체된다
- 드래프트 UI, 드래프트 로직
- 같은 공격 패턴을 반복 플레이 가능해야 함 (H1 측정 요건)
- 상세: Phase 0 완료 후 작성

### 6.3 Phase 2 — 스킬

- 스킬 2종 (토네이도, 포탈 또는 등가물)
- 코스트 시스템 도입 여부를 이 시점에 결정. 스킬이 코스트 없이 자유 사용이면 긴장감이 안 생김. 코스트 도입이 필연일 가능성 높음
- **맥락 변화**: Effects 맥락에 실제 스킬 효과 Component/System 추가 (Phase 0에 열어둔 자리에 들어감)
- 상세: Phase 1 완료 후 작성

### 6.4 Phase 3 — 배치 시 효과 / 인접 시너지

- 유닛별 배치 시 효과 1종
- 인접 시너지 효과 (옆 유닛 종류에 따른 스탯 증가)
- 배치 판단이 "어디에 놓을까"의 깊이를 갖게 됨
- **맥락 변화**: Effects 맥락 확장 (배치 시 효과 + 인접 시너지). Units의 Component에 시너지 필드 추가 가능
- 상세: Phase 2 완료 후 작성

### 6.5 Phase 4 — 마무리

- 3분 타이머 (Phase 0~3은 타이머 없이 타임라인 끝까지 진행)
- 더미 봇 스코어 5개와 결과 비교 화면
- 검증 프로토콜 실행 (H1/H2/H3 측정)
- 헤드리스 시뮬레이션 하네스
- 상세: Phase 3 완료 후 작성

### 6.6 Phase별 맥락 도입 로드맵

Phase가 진행되면서 ECS 맥락이 어떻게 채워지는지 요약:

| Phase | Units | Movement | Combat | Effects |
|---|---|---|---|---|
| 0 | 배치/Health/생성/소멸 | 경로 따라가기 | 타겟팅/공격/데미지/사거리 | (자리만, 빈 폴더) |
| 1 | + 드래프트 연동 | (변화 없음) | (변화 없음) | (자리만 유지) |
| 2 | (변화 없음) | + 스킬이 영향 주는 경우 확장 | + 코스트 연동 | **스킬 효과 구현 시작** |
| 3 | + 시너지 필드 | (변화 없음) | + 시너지 적용 | + 배치/시너지 효과 |
| 4 | (변화 없음) | (변화 없음) | (변화 없음) | (변화 없음) |

향후 추가 가능성 (프로토타입 범위 밖):
- **Collision 맥락**: Unity Physics 도입 시
- **AI 맥락**: 공격 유닛의 지능형 행동 필요 시

### 6.7 Phase 이동 규칙

- 각 Phase는 해당 Phase 문서의 **종료 조건 이진 체크를 모두 통과**해야 다음 Phase로 이동
- 시간 기반 판단 금지 ("n주 지났으니 다음으로")
- 종료 조건 외에 **주관 평가 게이트**가 있을 수 있음 (`PHASE0.md` 섹션 3.3 참조)
- 주관 평가에서 "재미없고 개선 욕구도 없음" 신호가 다수면 다음 Phase로 가지 않고 현재 Phase를 재조정
- Phase를 건너뛰지 않는다
- **Phase 경계 리팩토링은 자동화되지 않는다**. 필요 시 사용자가 별도로 요청한다.

---

## 7. Phase별 공통 산출물

각 Phase 완료 시 에이전트는 다음을 제공한다:

1. **동작하는 Unity 프로젝트** — 에디터 플레이 + Android 빌드 동작. 모바일 수동 배포 설정을
   변경한 작업은 추가로 iOS Xcode 프로젝트 export와 Ad Hoc 실기기 smoke 결과를 기록
2. **최소 테스트** — EditMode 1개 이상, PlayMode 1개 이상
3. **결정 기록 문서** — `phase{N}-decisions.md`. 해당 Phase에서 에이전트가 내린 기술적 결정(폴더 구조, 클래스 분할, 타겟팅 규칙 등)과 그 근거를 짧게 기록
4. **로그 출력 확인** — 해당 Phase의 로그 스키마가 실제로 파일에 기록되는지 확인

결정 기록 문서는 다음 Phase 작성 시 참조된다. 이것이 없으면 Phase 간 일관성이 깨진다.

---

## 8. 에이전트 자율 결정 영역

다음은 **구현 에이전트의 재량**이며, 본 문서가 지시하지 않는다. 단, 섹션 3(추상화 규칙)과 섹션 5(금지 패턴)의 경계 내에서.

- 프로젝트 폴더 구조의 세부 네이밍 (단, 섹션 2.5.3의 맥락별 폴더 분리는 준수)
- 씬 구성 (단일 씬 / 멀티 씬)
- ECS Component 세부 설계 (이름, 필드, 분리 단위) — 단 소속 맥락은 명확히
- ECS System 세부 설계 (개수, 의존 순서, SystemGroup)
- `BattleBridge`의 구체 클래스 분할 (단일 / 보조 클래스 동반)
- Baker 방식 vs 수동 변환
- 타겟팅 규칙 (가장 가까운 / 앞선 / 체력 낮은)
- 경로 표현 (waypoint / 스플라인)
- 그리드 좌표 타입 (int2 / Vector2Int)
- 유닛 비주얼 (Primitive / 스프라이트)
- UI 프레임워크 (UGUI / UI Toolkit — 일관성만 유지)
- 테스트 범위 (최소 요구만 만족하면 됨)
- 로그 파일 경로, 이름, 회전 정책

**결정 원칙**: 애매하면 **단순한 쪽**. 에이전트가 결정을 내릴 때마다 `phase{N}-decisions.md`에 한 줄로 기록한다.

**ECS 설계의 불확실성 대응**: 팀이 ECS에 깊이 있는 경험이 없을 수 있다. 다음 두 가지 접근을 섞어 쓴다:

1. **작은 결정은 에이전트가 내리고 짧게 설명한다** — 사용자가 실시간으로 ECS를 학습하는 효과
2. **아키텍처 수준의 결정은 사용자에게 질문한다** — 여러 정답이 있는 경우에만. 작업 단위마다 질문하지 않고 묶어서 한 번에.

질문 가치가 있는 결정의 예:
- Component 소속 맥락이 애매할 때 (예: `Cooldown`이 Units인가 Combat인가)
- 맥락 간 이벤트를 Buffer로 할지 NativeQueue로 할지
- SystemGroup 구성과 업데이트 순서
- Burst 호환 불가한 API가 필요해 보일 때

---

## 9. 모호함 해소 순서

구현 중 판단이 모호한 상황에서는 다음 순서로 해결한다:

1. **현재 Phase 문서**에 답이 있는가
2. **본 TRD**에 답이 있는가
3. **PRD**에 답이 있는가
4. 없으면 **검증 가설(H1/H2/H3)에 가장 도움 되는 선택**
5. 그래도 모호하면 **단순한 쪽**
6. 여전히 모호하면 **결정 기록 문서에 "결정 필요 항목"으로 남기고** 다음으로 진행

에이전트가 사용자에게 질문해야 하는 경우, **여러 결정 항목을 묶어서 한 번에** 질문한다. 작업 단위마다 질문하지 않는다.

---

## 10. 변경 이력

| 버전 | 날짜 | 변경 |
|---|---|---|
| v0.1 | 초안 | TRD 최초 작성. PRD에서 기술 섹션 이관, Phase 순서 통합 |

---

**문서 상태**: Active. 모든 구현 호출 시 컨텍스트로 포함.
**업데이트 주기**: Phase 종료 시마다 확인. 기술 제약이 바뀌면 즉시 반영.

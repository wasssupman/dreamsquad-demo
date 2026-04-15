# TRD — Defense Tournament Prototype

> **Technical Requirements Document.** 프로토타입의 기술 스택, 아키텍처 경계, 제약, 금지 패턴, Phase 순서를 단일 참조 문서로 모은다. 이 문서는 에이전트 호출 시 **매번 컨텍스트로 함께 전달되는 문서**이며, 개별 Phase 문서(`PHASE0.md` 등)와 함께 읽힌다.
>
> PRD(`PROTOTYPE_PRD.md`)가 "왜 만드는가 / 무엇을 검증하는가"를 다룬다면, 이 문서는 **"어떤 제약 위에서 만드는가"**를 다룬다. 기술 세부 설계(클래스 다이어그램, 시퀀스 등)는 포함하지 않으며, 그 영역은 구현 에이전트의 재량이다.

---

## 0. 이 문서의 역할

- **누가 읽는가**: 구현 에이전트(Superpowers 등), 팀 개발자
- **언제 읽는가**: 모든 구현 호출 시작 시점. 단일 작업마다 이 문서가 프롬프트 컨텍스트에 포함된다.
- **무엇을 포함하는가**: 기술 스택 / 아키텍처 경계 / 제약 / 금지 패턴 / Phase 순서
- **무엇을 포함하지 않는가**: 검증 가설(PRD), 스코프 이유(PRD), 세부 클래스 설계(에이전트 재량), Phase별 상세 구현 범위(각 Phase 문서)

**관련 문서 구조**:

```
PROTOTYPE_PRD.md   — 검증 가설, 스코프, 실패 대응, 검증 프로토콜
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
| 보조 타겟 | Unity Editor 플레이 모드 (일상 개발) |
| 최소 API 레벨 | Android 8.0 (API 26) 또는 그 이상 |

**다른 플랫폼 고려 금지.** iOS, Windows, WebGL 등 멀티 플랫폼을 위한 어떤 추상화도 프로토타입 단계에 도입하지 않는다. Android 한 개 플랫폼에서 안정적으로 동작하는 것이 최우선.

### 1.2 필수 패키지

다음 패키지만 사용한다. 이 목록에 없는 패키지는 플랜 내 채택 근거가 명시되지 않으면 금지.

| 패키지 | 버전 | 용도 |
|---|---|---|
| Entities | 6.x | ECS 프레임워크 |
| Entities Graphics | 6.x | ECS 엔티티 렌더링 |
| Burst | 최신 호환 | ECS 시스템 Burst 컴파일 |
| Collections | 최신 호환 | NativeArray, NativeQueue 등 |
| Mathematics | 최신 호환 | int2, float3 등 수학 타입 |
| Jobs | 최신 호환 | IJob, IJobParallelFor |
| TextMeshPro | 번들 | UI 텍스트 |

**금지 패키지**: NGO (Netcode for GameObjects), Mirror, Photon, DOTween, Zenject, UniRx, MessagePipe. 프로토타입 단계에 이들의 기능이 필요하다고 느껴지면 설계를 의심할 것.

---

## 2. 아키텍처 경계

### 2.1 원칙 — 하이브리드 ECS "최소한" 전략

본 프로토타입은 **ECS를 유닛 이동·전투 시뮬레이션에만 사용**한다. 그 외 모든 레이어(UI, 입력, 게임 상태, 로깅, 씬)는 전통적 MonoBehaviour + ScriptableObject 방식이다. 이 경계는 프로토타입 내내 고정되며, 개발 중 "이것도 ECS로 옮기면 좋겠다"는 판단은 기본적으로 기각한다.

**ECS 선택의 이유**: 본 게임은 20×10 그리드에서 수십~수백 유닛이 동시에 움직이는 구조이고, 헤드리스 시뮬레이션으로 밸런스를 튜닝할 계획이 있다. Burst 호환 순수 함수 + Job으로 작성된 전투 코드는 이 두 요구를 자연스럽게 만족시킨다.

**"최소한"인 이유**: ECS는 검증 가설(H1/H2/H3) 자체에 기여하지 않는다. ECS는 구현 품질과 본 게임 이식성을 위한 선택이다. 개발 중 ECS 관련 문제로 검증이 지연되면 **ECS 영역을 축소해서라도 검증을 우선**한다.

### 2.2 ECS 월드에 속하는 것

- 방어 유닛 엔티티 (배치 후의 상태, 스탯, 쿨다운, 타겟팅)
- 공격 유닛 엔티티 (경로 이동, 체력, 상태이상)
- 투사체 엔티티 (있는 경우)
- 전투 계산 시스템 (데미지 적용, 사거리 체크, 인접 시너지 적용 ← Phase 3 이후)
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

1. **전투 시작**: MonoBehaviour 쪽 데이터(배치된 유닛, 공격 패턴 등)를 받아 ECS 엔티티로 생성
2. **커맨드 전달**: 전투 중 플레이어 입력(추가 배치, 스킬 사용 등)을 ECS 커맨드 구조체로 전달
3. **결과 수집**: 전투 종료 시 결과를 MonoBehaviour 쪽으로 반환. ECS 월드 정리
4. **이벤트 pull**: ECS 시스템이 `NativeQueue` 또는 유사 구조에 쌓은 이벤트를 매 프레임 poll하여 로거에 전달

이 4가지 외의 책임을 `BattleBridge`에 두지 않는다. `BattleBridge`가 비대해지는 것은 경계 설계가 샜다는 신호다.

**데이터 입력 방식**: Baker 패턴으로 ScriptableObject → ECS Component 변환. **SubScene은 사용하지 않는다** — 동적 배치 구조라 이점이 적고 복잡도만 올라감.

**렌더링**: Entities Graphics 기본 사용. 프로토타입 기준 유닛 수가 적으므로(최대 50~100) `RenderMeshArray`로 충분. 커스텀 렌더링 금지.

**입력**: 터치/마우스 입력은 전적으로 MonoBehaviour 쪽에서 처리. 입력이 전투에 영향을 주는 경우 커맨드 구조체로 ECS 월드에 전달한다.

---

## 3. 아키텍처 원칙

### 3.1 데이터 주도

- 유닛/적/공격 패턴/스킬은 ScriptableObject로 정의
- 런타임에 Baker 또는 변환 헬퍼로 ECS Component로 옮긴다
- **하드코딩 금지**. 수치는 모두 데이터에서 나온다.
- 이유: 튜닝 빈도가 높고, Phase 2에서 헤드리스 시뮬레이션으로 자동 튜닝 예정

### 3.2 상태 명확

- 판의 상태는 enum 기반 StateMachine (`Draft / Placement / Combat / Result`)으로 명확히 분리
- StateMachine은 MonoBehaviour 쪽에 위치
- Phase 전환 이벤트는 관찰 가능해야 함 (event 또는 유사 패턴)

### 3.3 순수 로직 분리

- ECS 시스템은 가능한 한 **Burst 호환 가능한 ISystem**으로 작성
- `SystemBase`는 C# 오브젝트 참조가 필요한 경우에만 사용
- 전투 계산의 핵심 함수는 `static` 메서드 또는 `IJob` 구조로 분리 가능한 범위에서 분리
- 이유: 헤드리스 시뮬레이션에서 Unity 의존 없이 실행 가능해야 함

### 3.4 로깅 우선

- **로깅은 구현의 마지막이 아니라 첫 축**이다
- Phase 0부터 로깅 시스템이 존재해야 한다
- ECS 시스템이 `NativeQueue`에 이벤트를 쌓고, MonoBehaviour Logger가 매 프레임 poll하여 JSON 파일에 기록
- 로그 스키마는 `PROTOTYPE_PRD.md` 섹션 2.11을 기준으로 하되, Phase 단계에 맞게 확장

### 3.5 테스트 가능성

- ECS 시스템은 `EntityManager` 없이도 단위 테스트 가능하게 핵심 계산 함수를 분리
- 다만 과도한 추상화는 금지. 프로토타입 단계에서 테스트 커버리지는 목표가 아니며, **핵심 로직의 회귀 방지 수준**이면 충분
- 각 Phase는 최소 1개의 EditMode 테스트와 1개의 PlayMode 테스트를 요구 (Phase 문서에서 구체화)

---

## 4. 금지 패턴

다음은 프로토타입 전반에 걸쳐 금지된다. 구현 에이전트는 이 패턴 발견 시 스스로 거부해야 한다.

### 4.1 경계 위반

- `BattleBridge` 외부에서 `EntityManager`, `World.DefaultGameObjectInjectionWorld`, `SystemAPI` 직접 호출
- ECS 영역을 UI / 드래프트 / 결과 화면 등 경계 밖으로 확장
- UI 로직이 ECS Component를 직접 읽거나 쓰는 것

### 4.2 아키텍처 악취

- `XxxManager` 싱글톤 남발. `GameManager` 1개만 허용
- MonoBehaviour에 전투 로직 직접 작성 (전투는 ECS 시스템에서만)
- SystemBase 남발 (ISystem 우선)
- ECS Component 과다 (10개 이상 만들기 전에 재검토)
- "나중을 위한" 인터페이스, 추상화, 확장 포인트
- enum + switch 떡칠 (단, ECS Component는 Tag Component 패턴으로 다형성을 대체)

### 4.3 데이터 관리

- 하드코딩된 수치 (매직 넘버)
- public 필드 남발 (`[SerializeField] private` 사용. 단, ECS Component struct는 public 필드가 정상)
- ScriptableObject 대신 코드 상수로 유닛 스탯 정의
- JSON 하드코딩된 경로 (공격 패턴 데이터는 SO 권장)

### 4.4 패키지 / API

- 네트워크 관련 패키지/코드 (NGO, Mirror, Photon 등)
- SubScene 사용
- 에디터 전용 API를 런타임 코드에 사용
- Burst 컴파일이 실패하는 API를 ECS 시스템에 사용
- DOTween, Zenject 등 범용 라이브러리 (근거 없으면 금지)

### 4.5 스코프 침범

- PRD 섹션 0의 "명시적 비목표" 목록을 건드리는 것
- 현재 Phase의 범위를 넘어서는 시스템을 "미리 만들어두는" 것
- 다음 Phase를 위한 훅/인터페이스를 미리 까는 것

---

## 5. 폴더 구조 (권장)

아래는 **권장 구조**이며, 에이전트가 프로젝트 초기 셋업 시 준수한다. 세부 네이밍은 재량.

```
Assets/
  _Project/
    Scripts/
      Core/          — GameManager, GameStateMachine, 공통 타입
      Bridge/        — BattleBridge 및 경계 통신 구조체
      Battle/        — ECS Components, Systems, Jobs (전투 로직)
      UI/            — MonoBehaviour UI 컴포넌트
      Data/          — ScriptableObject 정의 (유닛, 공격 패턴, 스킬)
      Logging/       — Logger, JSON 스키마
    Data/            — ScriptableObject 애셋 파일
    Scenes/
    Prefabs/
    Tests/
      EditMode/
      PlayMode/
  Plugins/           — (필요 시) 외부 의존성
ProjectSettings/
Packages/
```

**Assembly Definitions**: Scripts 하위 각 폴더에 `.asmdef`를 두되 Phase 0~1에서는 과도한 분리 금지. Phase 3 이후 필요성이 드러나면 분리.

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
- 상세: Phase 1 완료 후 작성

### 6.4 Phase 3 — 배치 시 효과 / 인접 시너지

- 유닛별 배치 시 효과 1종
- 인접 시너지 효과 (옆 유닛 종류에 따른 스탯 증가)
- 배치 판단이 "어디에 놓을까"의 깊이를 갖게 됨
- 상세: Phase 2 완료 후 작성

### 6.5 Phase 4 — 마무리

- 3분 타이머 (Phase 0~3은 타이머 없이 타임라인 끝까지 진행)
- 더미 봇 스코어 5개와 결과 비교 화면
- 검증 프로토콜 실행 (H1/H2/H3 측정)
- 헤드리스 시뮬레이션 하네스
- 상세: Phase 3 완료 후 작성

### 6.6 Phase 이동 규칙

- 각 Phase는 해당 Phase 문서의 **종료 조건 이진 체크를 모두 통과**해야 다음 Phase로 이동
- 시간 기반 판단 금지 ("n주 지났으니 다음으로")
- 종료 조건 외에 **주관 평가 게이트**가 있을 수 있음 (`PHASE0.md` 섹션 3.3 참조)
- 주관 평가에서 "재미없고 개선 욕구도 없음" 신호가 다수면 다음 Phase로 가지 않고 현재 Phase를 재조정
- Phase를 건너뛰지 않는다

---

## 7. Phase별 공통 산출물

각 Phase 완료 시 에이전트는 다음을 제공한다:

1. **동작하는 Unity 프로젝트** — 에디터 플레이 + Android 빌드 양쪽 동작
2. **최소 테스트** — EditMode 1개 이상, PlayMode 1개 이상
3. **결정 기록 문서** — `phase{N}-decisions.md`. 해당 Phase에서 에이전트가 내린 기술적 결정(폴더 구조, 클래스 분할, 타겟팅 규칙 등)과 그 근거를 짧게 기록
4. **로그 출력 확인** — 해당 Phase의 로그 스키마가 실제로 파일에 기록되는지 확인

결정 기록 문서는 다음 Phase 작성 시 참조된다. 이것이 없으면 Phase 간 일관성이 깨진다.

---

## 8. 에이전트 자율 결정 영역

다음은 **구현 에이전트의 재량**이며, 본 문서가 지시하지 않는다. 단, 섹션 4(금지 패턴)의 경계 내에서.

- 프로젝트 폴더 구조의 세부 네이밍
- 씬 구성 (단일 씬 / 멀티 씬)
- ECS Component 세부 설계 (이름, 필드, 분리 단위)
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

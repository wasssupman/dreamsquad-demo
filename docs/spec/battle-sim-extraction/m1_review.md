# M1 구현 리뷰

> 리뷰 기준: `c0a361cb` (`7fcdc9f7..c0a361cb`)
>
> 리뷰일: 2026-08-04
>
> 범위: M1 units 7~10 문서 산출물 + unit 11 선행 머지 1·2
>
> 방식: Track A common code review + Track B `$ecs-reviewer`; 더 엄격한 판정을 최종 적용

## 최종 판정

**REQUEST CHANGES**

- CRITICAL: 0
- HIGH: 2
- MEDIUM: 3
- LOW: 1

M1 전체는 완료되지 않았다. units 7·9·10은 산출물 커밋과 완료 기준을 충족한 것으로 확인했다.
unit 8은 필수 부속 인벤토리의 인코딩 손상 때문에 리뷰 승인을 보류한다. unit 11은 머지 1·2만
구현됐고 머지 3, Unity 전체 EditMode, Play smoke, CLAUDE 규칙 개정이 남아 있다.

## Unit별 완료 판정

| unit | 커밋 | 구현/작성 상태 | 리뷰 판정 |
|---|---|---|---|
| 7 — 세션 계약 청사진 | `14a86393` | `m1_blueprint_session_contract.md` 작성, critic 지적 반영 | **완료** |
| 8 — 데이터 대응표 | `d97f9e18` | 본문과 전수 인벤토리 2편 커밋 | **리뷰 미통과** — 부속 문서 인코딩 복구 필요 |
| 9 — 틱 파이프라인 | `e60f4ec3` | 44/44 시스템, 내부 9채널, RNG·사망 릴레이 명문화 | **완료** |
| 10 — salvage 판정표 | `048573a8` | 시스템 44 + 채널 27 + Bridge 16 = 87건 판정 | **완료** |
| 11 머지 1 — 비주얼 statics | `b564e768` | `BattleVisualKnobs` 이관 구현, dotnet 컴파일 확인 | **미완료** — Unity 회귀/Play 검증 없음 |
| 11 머지 2 — 임계 조회 역전 | `c0a361cb` | `StackThresholdRegistry` 도입, 테스트 3건 신규/해제 | **미완료** — 신규 테스트와 전체 EditMode 미실행 |
| 11 머지 3 — DebugMenu 퇴거 | 없음 | 미착수 | **미완료** |

“커밋 존재”는 완료 판정과 동일하지 않다. unit 11 스펙은 세 머지와 검증을 모두 요구하므로,
머지 1·2도 현재는 구현 완료가 아니라 **검증 대기**로 본다.

## Track A — common review

### HIGH 1 — unit 8 전수 인벤토리가 읽을 수 없는 상태다

근거:

- `m1_data_inventory_components.md:4` 이후에 `而댄룷?뚰듃`, `異붿텧` 같은 mojibake가 광범위하게 존재한다.
- `m1_data_inventory_gates.md:4` 이후도 동일하다.
- 인접한 M1 본문 문서는 UTF-8로 정상 해독되며, 위 두 파일은 명시적 UTF-8 읽기와 git blob 검색에서도
  같은 깨진 문자열이 확인됐다.

영향:

- unit 8의 핵심 산출물은 컴포넌트·게이트·부재 상태·writer를 후속 이식자가 행 단위로 확인하는 정본이다.
- 읽을 수 없는 인벤토리를 기반으로 unit 12+ 이식을 진행하면 필드, 게이트 또는 소유권을 누락할 수 있다.
- 따라서 unit 8은 커밋됐지만 자체 완료 목적을 충족하지 못한다.

조치:

- 두 부속 문서를 정상 UTF-8로 재생성하거나 원문에서 복구한다.
- 일반 에디터와 CLI 양쪽에서 재열람하고, `IComponentData 97`, `IBufferElementData 21`, `ISystem 44`,
  `RequireForUpdate 35`, `RequireAnyForUpdate 4`를 다시 대조한다.

### HIGH 2 — unit 11 머지 1·2의 필수 Unity 검증이 실행되지 않았다

근거:

- `11_preparatory_merges.md:54-57`은 각 머지 후 컴파일과 전체 EditMode를 요구한다.
- 같은 문서 `:68`은 비주얼 이관에 대한 라이브 Play smoke를 요구한다.
- `b564e768` 커밋 기록은 전체 EditMode와 Play smoke 미실행을 명시한다.
- `c0a361cb` 커밋 기록은 신규/Ignore 해제 테스트 3건과 전체 EditMode 미실행을 명시한다.
- Track A에서 `dotnet build Wassup.Runtime.csproj`는 성공했지만 Unity Test Framework 테스트 실행 증거를
  만들지는 못했다. Unity 생성 test csproj의 `dotnet test`는 Unity 테스트를 실행하지 않는다.

영향:

- 머지 1은 7개 presentation 소비자의 런타임 값을 옮겼으므로 컴파일만으로 실제 렌더 무변을 증명할 수 없다.
- 머지 2는 기존 Ignore를 해제하고 누락된 3중 AND 게이트 픽스처를 보완했으므로 테스트를 실제 실행해야
  의존 역전의 행동 무변을 증명할 수 있다.

조치:

- 머지 2 집중 테스트를 먼저 실행하고 전체 EditMode를 수행한다.
- 머지 1에 대해 전투 1판 Play smoke와 콘솔 오류 0을 확인한다.
- 결과를 `11_preparatory_merges.md`에 커밋별로 기록한다.

### MEDIUM 1 — unit 11 완료 기준이 내부적으로 성립하지 않는다

근거:

- 본문 `11_preparatory_merges.md:36-38`은 머지 2 뒤에도 DebugMenu 5개의 `BattleBridge` 참조가 남고,
  머지 3에서 이를 제거한다고 설명한다.
- 그러나 완료 기준 `:63`은 **머지 2 직후** `Battle/` 전체의 `BattleBridge` 코드 참조 0을 요구한다.
- 완료 기준 `:65`은 머지 3 뒤 `Battle/` 안 MonoBehaviour 0을 요구하지만, 이동 대상 5개 외에도
  `BlockingHazardPresenter`, `PickupPresenter`, `ResignationPresenter`가 `Battle/Effects`에 남아 있다.

영향:

- 현재 문구대로는 머지 2가 절대 통과할 수 없고, 머지 3의 대상 목록만 수행해도 MonoBehaviour 0이 되지 않는다.
- 향후 sim asmdef 분리의 기계적 게이트가 모호해진다.

조치:

- 머지 2 게이트를 “비디버그 프로덕션 `BattleBridge` 참조 0”으로 좁히고 전체 0 게이트를 머지 3으로 옮긴다.
- MonoBehaviour 0이 실제 목표라면 presenter 3종의 Presentation 이관을 머지 3 범위에 추가한다.
- presenter를 현 위치에 허용할 계획이라면 완료 기준을 “DebugMenu MonoBehaviour 0”으로 명시적으로 좁힌다.

### MEDIUM 2 — `BattleVisualKnobs`가 writer 소유권을 타입으로 강제하지 않는다

근거:

- `BattleVisualKnobs.cs:11`은 `BattleBridge`가 유일한 writer라고 선언한다.
- 그러나 `BattleVisualKnobs.cs:19-53`의 21개 값은 전부 public setter를 노출한다.
- 이관 전에는 `CharacterBillboardTilt`를 제외한 대부분이 `BattleBridge`의 private setter였다.

영향:

- 현재 다른 writer는 없지만, 리팩터링이 만들려는 경계를 주석에만 의존하게 만들었다.
- 이후 asmdef 분리·프레젠테이션 확장 중 임의 전역 쓰기가 생겨도 컴파일러가 잡지 못한다.

조치:

- 의도적으로 런타임 튜닝하는 `CharacterBillboardTilt`만 쓰기 가능하게 남긴다.
- 나머지는 `internal set`, 단일 `Apply` API 또는 Bridge 전용 writer 표면으로 제한한다.

### MEDIUM 3 — unit 8의 상위 완료 기준 계수가 자기 진행 기록과 다르다

근거:

- `8_data_mapping_blueprint.md:14,22`는 `IComponentData 96 + IBufferElementData 21`을 완료 기준으로 쓴다.
- 같은 문서 `:26-29`와 본문 `m1_blueprint_data_mapping.md:10-11`은 루트
  `BattleTimeScale`을 포함하면 HEAD 전체가 `97+21`이라고 정정한다.

영향:

- 이식자가 96과 97 중 어느 계수로 누락 검사를 해야 하는지 상위 unit 문서만 보고 결정할 수 없다.

조치:

- HEAD 전체 기준을 `97+21`로 통일한다.
- `96`은 “Units/Movement/Combat/Effects 네 폴더만 센 부분집합”이라고 같은 문단에 보조 계수로 남긴다.

### LOW 1 — README 실행 인덱스가 현재 M1 상태보다 뒤처졌다

근거:

- `README.md:3`은 아직 “M1 상세 units 미작성”이라고 표시한다.
- M1 표는 unit 11 문서가 생겼는데도 `11+` 하나로 접혀 있다(`README.md:39`).

조치:

- units 7~10 완료, unit 8 리뷰 보완 필요, unit 11 진행 중으로 상태를 갱신한다.
- `11_preparatory_merges.md`를 독립 행으로 연결한다.

## Track B — `$ecs-reviewer`

### ECS 변경 범위

- units 7~10은 문서 전용이며 ECS 런타임을 변경하지 않는다.
- unit 11 머지 1은 Bridge의 view-only static mirror를 Presentation으로 이동했다.
- unit 11 머지 2는 `StackModifierTickSystem → BattleBridge` 조회를
  `StackModifierTickSystem → StackThresholdRegistry ← BattleBridge 등록`으로 뒤집었다.
- 신규 ECS 시스템, 컴포넌트, NativeQueue, DynamicBuffer, 구조 변경 순서, update order 변경은 없다.

### 판정

- Entities 패키지는 6.4.0이며 신규 `ISystem`/ECB/Burst 관용구 위반은 발견되지 않았다.
- `StackModifierTickSystem`의 managed Dictionary 기반 non-Burst 제약은 기존과 동일하다.
- `BattleBridge` 이외 MonoBehaviour가 새로 `EntityManager`, default World 또는 `SystemAPI`에 접근하지 않는다.
- 새 NativeContainer 수명·중복 singleton·drain ownership 문제는 도입되지 않았다.
- 머지 2는 프로덕션 sim의 직접 `BattleBridge` 의존을 제거하는 올바른 방향이다.
- 단, unit 11의 DebugMenu/Presenter 경계와 CLAUDE 후계 불변식 개정이 남아 있어 asmdef 분리 준비는
  아직 완료되지 않았다.

Track B 단독 판정은 **COMMENT / unit 11 incomplete**다. 즉시 재현되는 ECS 런타임 결함은 없지만,
완료 선언에는 머지 3과 검증이 필요하다.

## 검토했으나 finding으로 채택하지 않은 항목

### unit 9 사망 창과 unit 10 `UnitLifecycle` rewrite

unit 9은 현 ECS의 관측 시맨틱, 즉 사망 마킹 뒤 `ResignationDrop`·`PatrolLifecycle`·이벤트 베이크가
끝난 후 제거되는 phase 창을 보존한다. unit 10의 rewrite는 ECS의 “유일 destroyer + 4개 query” 형태를
그대로 옮기지 않겠다는 판정이며, 제거 전에 동일 결과를 베이크하는 계약은 남긴다. 구현 형태와 관측
시맨틱의 구분이므로 현재 문구만으로는 모순 finding으로 채택하지 않았다.

## 재리뷰 게이트

다음 조건을 모두 만족한 뒤 재리뷰한다.

1. unit 8 부속 인벤토리 2편을 정상 UTF-8로 복구하고 97+21/44/35+4 계수를 재확인한다.
2. unit 11 머지별 완료 기준을 모순 없이 수정한다.
3. 머지 3을 구현하거나 범위를 명시적으로 재결정하고 CLAUDE 후계 불변식을 갱신한다.
4. `BattleVisualKnobs` writer 표면을 제한한다.
5. 머지 2 집중 테스트, 전체 EditMode, 머지 1 Play smoke를 실행해 실패 0을 기록한다.
6. README 상태와 unit 11 인덱스를 실제 진행 상태에 맞춘다.

위 조건 전에는 unit 12+ 규칙 적출에 진입하지 않는 것을 권고한다.

## 검증 메모

- 검토 파일: M1 범위 26개, 그중 코드/테스트/메타/active unit 11 문서 delta 15개.
- `IComponentData 97`, `IBufferElementData 21`, `ISystem 44`, `RequireForUpdate` 보유 시스템 35,
  `RequireAnyForUpdate` 보유 시스템 4를 현재 소스에서 독립 재계수했다.
- `git diff 048573a8..c0a361cb --check` 통과.
- Track A의 `dotnet build Wassup.Runtime.csproj` 통과. Unity EditMode/PlayMode는 실행되지 않았다.
- 사용자/메인 세션의 기존 asset·ProjectSettings dirty 파일은 리뷰 범위에서 제외했고 수정하지 않았다.

# dreamcatcher-attach-requirement — 부착 대상 제한 필드 (클래스 / 특정 유닛)

상태: 완료 2026-07-30 (units 0~5 + unit 7 rev + units 8·10).
기존 최신 커밋: unit 7 `12f5b644` · unit 8 `874a54ad`. 인계 → `9_handoff_summary.md`

최신 자동 검증(unit 10, 2026-07-30): Unity compile error 0 · 관련 EditMode **6/6** ·
관련 PlayMode **2/2**. 전체 EditMode 1574건 중 공유 워크트리의 map dirty 영향 1건,
전체 PlayMode 70건 중 서버/상태 오염 12건 실패. unit 8 Push payload 회귀 **2/2**.

## 상위 목표

Defender에 부착되는 Unit/Squad 타입 드림캐쳐에 **부착 시점 정적 술어**
("가디언에만 부착 가능" / "방패셔틀에만 부착 가능")를 데이터 필드로 제공한다.
Unit은 부착 host에 효과를 주고, Squad는 부착 host를 수명 앵커로만 사용하며 실제 수혜
집합은 `axis`가 결정한다. 기존 capability 게이트는 유지하며 필드는 시트(DcCards 탭)에서 제어한다.

검증 질문: *제한 카드가 비대상 유닛에서 invalid·무차감 거절되고 대상 유닛에는 부착되는가?
Squad 카드의 버프 수혜 범위는 부착 대상이 아니라 `axis`를 계속 따르며, 제한 없는 기존
Unit/Squad 카드는 무회귀인가?*

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_field_and_eval.md` | 토대 | 필드 append + `MeetsAttachRequirement` 독립 순수 함수 + EditMode |
| `1_bridge_gate.md` | 게이트 | UI 판정 + 커밋 preflight 두 소비처 배선 + PlayMode e2e |
| `2_sheet_sync.md` | 시트 | `DcCardDto` append + export blank 규칙 + 라운드트립 |
| `3_validator.md` | 위생 | 에디터 validator — 무효/없는 id/범위 밖 설정 조기 검출 |
| `4_card_text_formatter.md` | 문안 | 포매터 접두 조립 + 골든 (optional resolver) |
| `5_text_wiring.md` | 배선 | 소비처 4곳 resolver 주입 + 씬 wiring + Play 육안 |
| `6_handoff_summary.md` | (종료 시) | 인계 요약 |
| `7_field_shape_rev.md` | rev | 필드 형태 3→2 (`attachType`+`attachValue`) |
| `8_push_header_bootstrap.md` | 시트 | 제한 카드 0장에서도 Push가 `DcCards` 신규 2열을 자동 생성 |
| `9_handoff_summary.md` | 인계 | unit 7~8 반영 최종 인계 |
| `10_squad_attach_requirement.md` | Squad 확장 | 클래스 전군 버프의 부착 앵커도 `attachType`으로 제한 |

## Feature-wide 계약

- **정의 계층 append-only**: 카드 끝에 `attachType`(`DcAttachType{None, Class, UnitId}`) + `attachValue`(string) **2필드**. zero-init = 제한 없음 → 기존 카드 무손상. `attachType` 이 `attachValue` 를 읽는 방식을 정한다 — `Class`=클래스 이름(대소문자 무시), `UnitId`=유닛 id(ordinal). 값 칸이 하나라 "종류를 바꿨는데 옛 값이 되살아나는" 함정이 구조적으로 없다(unit 7 rev).
- **적용 범위 = defender에 부착되는 `CardType.Unit` + `CardType.Squad`** (unit 10, 2026-07-30 사용자 결정). Active·BountyMark 비적용. 클래스 제한은 **단일 클래스**. 유닛 매칭 키는 `DefenderUnitData.id`(저장용 안정 키, ordinal) — 표시명 아님.
- **Squad의 두 축은 분리**: `attachType/attachValue`는 카드를 유지할 **부착 앵커**만 제한하고, `axis`는 실제 버프 수혜 집합을 계속 결정한다. 예: `Squad + ClassRanger + Class/Ranger`는 Ranger에게만 부착 가능하며 현재·미래 모든 Ranger를 버프하고, 그 host가 죽으면 전역 버프가 회수된다.
- **판정 = 단일 함수, 두 소비처**: `MeetsAttachRequirement` 를 `WouldDreamcatcherCardApply`(UI 스냅샷)와 실제 커밋 preflight가 각각 호출한다. Unit은 `ApplyDreamcatcherCardToUnit`, Squad는 공용 dispatcher `ApplyDreamcatcherCard`에서 첫 쓰기 전에 검사한다. **`WouldApply` 시그니처는 확장하지 않는다** — 부착 제한은 카드 기여도와 독립인 정적 술어다.
- **fail-closed**: 값을 못 읽거나(빈 값 · `Class` 인데 오타·숫자·`None`) host 데이터 조회 실패 시 불허 + loud 경고. 제한이 조용히 풀리는 쪽보다 눈에 띄게 안 붙는 쪽을 택한다. 값 해석 규칙의 단일 지점은 `DreamcatcherAttachEval.TryParseAttachClass`.
- **커밋 거절 = 카드 전체·무차감**: preflight 는 `DefenderUnitTag` 검사 직후·모든 쓰기 전. `-1` 반환이 `DreamcatcherHandController.cs:342` 에서 `AttachAndSpend` 전에 걸려 무차감·카드 잔류를 보장한다.
- **시트**: `DcCardDto` 에 **2필드** append(reflection 양방향 자동). `attachType` 셀은 이름 문자열(`None`/`Class`/`UnitId` — `StringEnumConverter`), `attachValue` 는 자유 문자열(`Guardian` / `shield_shuttle`). **제한 해제는 `attachType=None` 을 명시** — 빈 셀은 "유지"라서 해제 수단이 아니다. 전 카드가 `None` 이어도 Push 전용 키 없는 헤더 시드가 두 키를 운반하므로 첫 `Push to Sheet`에서 `DcCards` 오른쪽에 두 컬럼이 자동 생성된다(unit 8). 일반 카드 export의 blank 규칙은 유지한다.
- **문안 자동 표기** (2026-07-25 사용자 결정): 포매터가 "가디언 전용" / "{유닛명} 전용" 접두를 조립하고, 유닛 표시명은 caller 주입 resolver(실패 시 id 폴백)로 해석 — 포매터는 카탈로그를 직접 알지 않는다.

### 의도된 동작 (버그로 오인 금지)

- **사망 teardown 창의 비대칭**: `_defenderByTile.Remove`(`BattleBridge.cs:2660`)와 ECS 엔티티 파괴는 수명이 달라, 바인딩은 제거됐지만 엔티티는 남은 프레임에 제한 카드만 먼저 거절된다(무제한 카드는 `_em.Exists` 기준이라 통과). 무차감이라 실피해 없음. 조회 방식을 바꿔도 둘 다 `_defenderByTile` 소스라 해소되지 않는다.
  - 실제 창은 이 서술보다 **더 좁다**(review 확인): 드래그 픽(`TryPickDefenderAtScreen`)도 같은 `_defenderByTile` 을 돌기 때문에 바인딩이 사라진 프레임에는 hover 자체가 안 잡혀 드래그 경로로는 도달하지 않는다. 커밋 경로에 직접 진입하는 코드(테스트·미래 자동 부착)만 해당. 거절 시 전용 경고 문구가 나오므로 로그로 구분된다.
- **BountyMark×제한 = 조용한 무효**: `Classify` 가 `HasBountyMark()` 카드를 `AimMode.EnemyMark` → `ApplyBountyMark` 로 라우팅해 defender 게이트를 통과하지 않는다(배제는 코드 수준 자동 보장). 게이트 함수를 안 타므로 런타임 경고조차 없어 unit 3 validator 가 유일한 검출 수단.
- **`axis` 재사용 안 함**: `CardTargetAxis` 에도 ClassGuardian/Cost1 이 있으나 ① `axis` 는 Squad 효과 대상 집합 + PlacementAura 수혜 축으로 load-bearing(Unit 카드도 `RegisterPlacementAura` 로 소비) — 겸용하면 두 의미가 얽힌다 ② `axis` 는 Ranger/Guardian 뿐, `DefenderClass` 는 Fighter/Caster/Support 까지 ③ "특정 유닛 id" 가 axis 어휘에 없다.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음 (부착 판정 데이터 + UI 문안만).

## 후속 후보

- 복수 클래스 제한(flags) — 카드 기획이 생기면
- 부착 제한 ∧ 발동 게이트(`dreamcatcher-trigger-gates`) 동시 표기 시 문안 길이 정리 — 태그 칩化(hand-card-face `TargetTag` 전례)
- 덱빌더 카드 상세에 제한 대상 유닛 초상 표기

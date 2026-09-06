# 8 — Handoff Summary (최종, 2026-09-01)

> ⚠ **이 문서는 unit 8 시점의 인계다. spec 은 그 뒤로 unit 23 까지 갔다.**
> 최신 계약은 README 와 번호 문서가 정본이다. 특히:
> - **unit 12** 몸 = footprint 파생(방어유닛 **가로/2**) · **unit 14** 광역 멤버십 = 몸 걸침
> - **unit 22** 가디언 피해 선정을 게이트와 같은 자로 — 「공격은 하는데 피해 0」
> - **unit 23a/23b** 전투 도달 판정 **전면 통일** → `CLAUDE.md` **절대 제약 13** 으로 승격.
>   **종료 2026-09-07**(Play 육안까지). 골든 A/B 2회 — 8/8 무회귀이되 코퍼스가 이 축을
>   **관측할 수 없다**(전역 백로그 참조). 「일치」를 «효과 없음»으로 읽지 말 것.
>   「원점 항」은 «효과의 형»이 정한다(몸에서 나오나 / 자리에 떨어지나).
>   진입점 2개 + 상수 private 로 **컴파일러가 방어**한다. 남은 결함은 `docs/spec/README.md` 백로그.


> spec 완료 시점의 인계 지도. 최신 계약은 README(계약 1 rev 3)가 정본이다.

## Commit

`691c1a22`(rev 3 문서) → `f9d329af`(12 원 회귀) → `9eafbf90`(13 티어) → `e2101a32`(14 몸 걸침)
→ `cc375d06`(15 그림자) → `b3793fba`(16 소켓) → `0ebb1b6d`(11 facing 은퇴 −1102줄)
→ `d706a096`(18 전수조사 전폐) → `23417bc5`(19 존 틱) + 골든 4회(`824f87aa`·`e1c11669` 등).
units 0~10 은 이전 세션(`c587e15b` 까지).

## Implemented

- **몸 = 원 하나** (계약 1 rev 3): 방어유닛 `min(W,H)/2` 내접원 파생 · 적 티어(소0.25/중0.5/대1.0/보스 개별)
  · 구조물 `FootprintOf/2`. 판정 = `d² ≤ (사거리+selfR+targetR)²` — 술어 본체는 `SkillMath.InBodyReach` 하나
- 스킬 광역·에미터·자장가 부속·회오리·광역 착탄·버프장·재조준·해저드 존 틱까지 **전투 판정 격자 0**
  (에미터 결정론은 row-major 셀 키 → **simId rank** 로 재정의)
- **캐리어 3종**: 그림자(지름 2r·lift 크기 불변)·링(사거리+selfR — 「그림자가 링에 닿으면 안」= 판정식과 동치)
  ·임팩트 소켓(뷰 전용, 보스 0.8 저작). facing(레인·조준 페이즈) 은퇴
- 원장 2장 + 전복 인벤토리 = `docs/blueprint/`

## Key Files

`Skills/SkillMath.cs`(술어+이력) · `Combat/AttackReach.cs`(정본 진입점) · `Data/DefenderUnitData.cs`
(`BodyRadiusTiles` 파생) · `Data/AttackUnitData.cs`(`bodySize`) · `Effects/ZoneApplySystem.cs`(존 원 직접 곱)
· `Emission/PatternScope·PatternTargeting`(연속+simId) · `Presentation/UnitLiftVisual·SpineUnitView`(그림자 2r)

## Verified

EditMode 2,669건 전건 초록(선행 실패 2건 = bomb_man·boomerang 문안, 시트 소관) ·
골든 재베이크 4회 전부 귀속(총 킬 경제 69→69 중립, no_defense 카나리아 바이트 동일,
units 18·19 는 바이트 무변) · 결정론 2회 900틱 일치.

## Notes (되돌리면 안 되는 것)

- **Burst lookup 함정 5회 재발**: OnUpdate 의 `SystemAPI.GetComponentLookup` 로컬 형태는
  지우는 것도 **더하는 것도** NRE 를 부른다. 신규는 항상 필드(OnCreate+Update).
  `AttackSystem.cs` 의 `facingLookupRetired` 는 소비처 0 이지만 **지우면 안 되는 줄**이다
- 실효 도달 +0.25(1×1 selfR 0.5)는 **의도**(계약 5 rev) — 리베이스 금지
- fan 시차 슬롯·조준 저작(칸)·어그로 타일 필드·착지 셀·표기는 **사유 있는 격자 존치**(unit 18 표)
- `blobShadowSize`(씬) = tileSize 가 「그림자─링 동치」의 전제

## Follow-up

README 후속 후보 참조 — 1순위: **보스전 축**(long_boss −4킬, 시트 결정) + Play 육안 체크리스트.
다음 착수 spec: `docs/spec/dreamcatcher-attach-range-preview/`(결정 확정·미착수).
M1(battle-sim-extraction)은 골든 기준선 안정으로 착수 조건 충족.

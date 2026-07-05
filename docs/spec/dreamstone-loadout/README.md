# dreamstone-loadout

> 상태: **완료 2026-07-06** (units 0~7 — 확장 라운드: 개별 아이템 64종·티어 스탯·코스트 생산속도·아이콘 스크롤 피커). handoff → `4_handoff_summary.md`
> 커밋: 0 `5a47adf` · 1 `926aea6` · 2 `5d6ef22` · 3 `6d28b74` · 배선/e2e `2289362` · UI픽스 `a691144`·`d93fa82`·`7ab66b0` · REDRAFT 누수 `f4bfa09` · 5 `f23eb8f` · 6 `140d8f6` · 7 `c51338c`(+아이콘 임포트 M1)
> 리뷰: 설계 크리틱 1회 + unit별 + unit3 투트랙 + Codex 외부 리뷰(HIGH 1건 수정) 반영 · UI 구조 C안(슬롯 탭→피커 모달)
> 선행: `squad-loadout` (완료) · `ingame-dreamcatcher` (완료 — 매치 효과 레지스트리 재사용)
> 브레인스토밍 결정 2026-07-04: 하드캡=데이터 계약+검증 테스트 · 스탯 4종 재사용 · 같은 스톤 중복 장착 허용 · 인벤토리 없음(카탈로그 전체 목록 장착) · UI 구조=슬롯 탭→피커 모달(C안)

## 검증 질문

스쿼드 페이지에서 드림스톤을 최대 4개 장착·저장하고 게임을 시작하면, 배치된(그리고 이후 배치되는) 모든 아군 유닛에 스톤 스탯이 매치 내내 적용되는가? 같은 종류(예: 유니크 공격력) 4개 장착 시 표기 수치 합(유니크 = 7.5+6+6+4.5 = +24%)이 정확히 적용되는가?

## 상위 목표

스쿼드를 하나의 집합체로 만드는 첫 장비 시스템. 스쿼드 페이지의 편성 UI 아래 드림스톤 슬롯 4개를 추가하고, 장착된 스톤의 스탯을 게임 시작 시 아군 유닛 전체에 매치 상시 버프로 적용한다. 스코프는 **정의 + 장착 UI + 반입**: 획득/보유/강화/세트효과/아트는 전부 후속.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_stone_data_model.md` | DreamstoneData/Grade/Catalog SO + 등급 캡 validator + 테스트 에셋 16종 |
| 1 | 저장 | `1_squad_save_slots.md` | `SquadSave.stoneIds` 4슬롯 + 정규화 + 프로필 라운드트립 테스트 |
| 2 | UI | `2_squad_page_ui.md` | 스쿼드 페이지 재편: 슬롯 탭 → 피커 모달 (유닛 7 + 스톤 4 동일 인터랙션) |
| 3 | 반입 | `3_battle_carry_in.md` | BattleBridge 매치 효과 레지스트리 일반화(axis All) + 시작 시 등록 + PlayMode smoke |
| 4 | 인계 | `4_handoff_summary.md` | handoff (구현 종료 시 작성) |
| 5 | 확장 | `5_stone_copies.md` | 개별 아이템 64종(순차 id) + 등급 캐파 내 소수1자리 상중하 티어 + 평면 피커 |
| 6 | 확장 | `6_cost_rate_stones.md` | MOVE 스톤 폐기 → 코스트 생산속도 스톤 + CostRuntime 배율 배선 |
| 7 | 확장 | `7_icon_scroll_picker.md` | 스탯 타입별 드림스톤 아이콘 SO 매핑 + 스크롤 아이콘 피커 UI |

## Feature-wide 계약

- **스톤 = 카탈로그 타입**: `DreamstoneData` SO + `DreamstoneCatalog`(id→에셋). 보유/획득 개념 없음 — 스쿼드 페이지는 카탈로그 전체 목록을 노출하고 바로 장착한다. 획득/인벤토리는 후속.
- **슬롯 4 고정 + 개별 아이템 모델** (rev 2026-07-06, unit 5): 스톤 64개는 전부 고유한 순차 id(`stone_001`~`064`)를 가진 개별 아이템이며 **장착 제한 규칙 없음** (장착된 아이템의 딤드는 "하나의 아이템 = 한 슬롯"이라는 물리적 사실). 같은 종류를 여러 슬롯에 = 그 종류의 다른 아이템들을 장착. `SquadSave.stoneIds` 길이 4, 빈칸 `""` — 스토리지는 관대 정책 유지. (이전: 같은 id 중복 장착 — 개념 자체 폐기)
- **스탯 티어** (rev 2026-07-06b, 소수 1자리): 종류당 4개 = [상=캐파, 중=0.8캐파 ×2, 하=0.6캐파] (캐파=등급캡÷4) — Unique 7.5/6/6/4.5 · Epic 5/4/4/3 · Rare 3/2.4/2.4/1.8 · Common 2/1.6/1.6/1.2. 종류 4개 합 = 3.2×캐파 (유니크 +24%, 30% 하드캡 이내).
- **등급 4종**: Common / Rare / Epic / Unique. **하드캡은 데이터 계약**: 스톤 개당 표기 수치 ≤ 등급 캡 ÷ 4 (유니크 30% → 개당 7.5%). EditMode validator 가 전 스톤 에셋에 강제한다. 런타임 클램프 없음.
- **캡은 "표기 수치 합" 기준**: 공격력/공속/이속은 additive 합산이라 표기 합 = 실효(티어 모델에서 유니크 종류 4개 = +24%). 체력(EffectiveHealth→DmgTakenMul)은 감소형이라 곱연산 스택 — 표기 합 30% 시 실효 EHP 약 +33.5%. 스탯별 실효 차는 밸런싱 단계에서 캡 표 확정 시 반영.
- **스탯 구성** (rev 2026-07-06c, unit 6): 공격력/공속/체력 = 엔티티 StatModifier 경로(`MapDcEffect`) · **코스트 생산속도(CostRate) = 매치 레벨 경로**(`CostRuntime.RegenRateMultiplier`, GameManager 가 kind 분리 적용). MoveSpeed 는 배치형 디펜더에 무의미 → 스톤에서 폐기(enum 값은 직렬화 보존). 배율 설정은 매치 진입 결정 지점만(스쿼드/테스트모드=계산값, 드래프트 확정=1.0) — ResetToStart/Configure 무접촉.
- **적용 범위**: 아군 디펜더 전체, 매치 상시(현재 + 미래 배치 모두). BattleBridge 의 드림캐쳐 매치 효과 레지스트리(`_activeDcEffects`)를 대상축 `All` 로 일반화해 재사용. **ECS 변경 0.**
- **반입 등록은 set-then-apply**: GameManager 는 `SetDreamstones` 로 pending 전달만, `BeginPlacement` 가 레지스트리 클리어 직후 적용. `BeginPlacement` 의 클리어에 등록이 지워지는 순서 결함(설계 크리틱 CRITICAL)과 재시작 leak 을 동시에 차단.
- **화면 구조 = 슬롯 탭 → 피커 모달** (2026-07-04 UI 리뷰 C안): 메인 화면은 슬롯만(유닛 7 + 스톤 4), 슬롯 탭 시 피커 모달에서 선택/해제. 유닛·스톤 동일 인터랙션, 뎁스는 모달 1장. 기존 "보유 그리드 클릭 = 첫 빈 슬롯" 방식은 폐기.
- **드래프트 폴백 경로 미적용**: 스톤은 스쿼드 소속. 스쿼드 미설정 → 드래프트 폴백 시 스톤 개념 없음.
- **밸런싱 placeholder**: 등급 캡(유니크 30% 확정, Common 8 / Rare 12 / Epic 20 잠정)과 스톤 수치는 데이터 — 밸런싱 단계에서 숫자만 조정.

## 후속 후보 (범위 밖)

- 스톤 획득처(가챠/꿈런 파밍/교환), 보유 인벤토리/수량 모델
- 강화/합성/분해, 세트 효과, 슬롯 조건(스탯별 전용 슬롯 등)
- RegenPerSec 등 절대치 스탯 스톤
- 런타임 클램프 (크로스 소스 스태킹이 밸런스 문제가 되면)
- 다중 스쿼드 간 스톤 공유/배타 규칙 (보유 모델 도입 시)
- (크리틱 이관) headless 드림캐쳐 auto-pick 이 `BeginPlacement` 클리어 직전에 등록돼 지워지는 기존 버그 (PlacementPhaseView.cs:56-58 순서)
- (크리틱 이관) `DreamcatcherSelectionView.Summary` axis 라벨 체인에 `All`/default 가드 추가
- (크리틱 이관) 체력 스톤 표기 vs 실효(EHP +33.5%) 차이의 플레이어 노출 방식 (툴팁 등)
- (리뷰 이관) StartSquadMatch 경유 통합 검증 자동화 → 구현됨 (`DreamstoneCarryInSmokeTest.EquippedSquad_StartSquadMatch_EndToEnd`, 2026-07-04)

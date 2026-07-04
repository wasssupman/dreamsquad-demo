# dreamstone-loadout

> 상태: 스펙 확정 2026-07-04 · 구현 대기 (Codex 이관 — `CODEX_GUIDE.md` 참조)
> 설계 크리틱(ecs-reviewer) 1회 반영 완료 · UI 구조 C안(슬롯 탭→피커 모달) 확정
> 선행: `squad-loadout` (완료) · `ingame-dreamcatcher` (완료 — 매치 효과 레지스트리 재사용)
> 브레인스토밍 결정 2026-07-04: 하드캡=데이터 계약+검증 테스트 · 스탯 4종 재사용 · 같은 스톤 중복 장착 허용 · 인벤토리 없음(카탈로그 전체 목록 장착) · UI 구조=슬롯 탭→피커 모달(C안)

## 검증 질문

스쿼드 페이지에서 드림스톤을 최대 4개 장착·저장하고 게임을 시작하면, 배치된(그리고 이후 배치되는) 모든 아군 유닛에 스톤 스탯이 매치 내내 적용되는가? 유니크 공격력 스톤 4개 장착 시 공격력이 정확히 +30%인가?

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

## Feature-wide 계약

- **스톤 = 카탈로그 타입**: `DreamstoneData` SO + `DreamstoneCatalog`(id→에셋). 보유/획득 개념 없음 — 스쿼드 페이지는 카탈로그 전체 목록을 노출하고 바로 장착한다. 획득/인벤토리는 후속.
- **슬롯 4 고정, 중복 허용**: `SquadSave.stoneIds` 길이 4, 빈칸 `""`. 같은 스톤 id 를 여러 슬롯에 장착 가능("유니크 공격력 4개" 시나리오).
- **등급 4종**: Common / Rare / Epic / Unique. **하드캡은 데이터 계약**: 스톤 개당 표기 수치 ≤ 등급 캡 ÷ 4 (유니크 30% → 개당 7.5%). EditMode validator 가 전 스톤 에셋에 강제한다. 런타임 클램프 없음.
- **캡은 "표기 수치 합" 기준**: 공격력/공속/이속은 additive 합산이라 표기 합 = 실효(유니크 4개 = 정확히 +30%). 체력(EffectiveHealth→DmgTakenMul)은 감소형이라 곱연산 스택 — 표기 합 30% 시 실효 EHP 약 +33.5%. 스탯별 실효 차는 밸런싱 단계에서 캡 표 확정 시 반영.
- **스탯 4종 재사용**: `CardBuffKind`(AttackDamage/AttackSpeed/EffectiveHealth/MoveSpeed) → `StatKind` 기존 매핑(`MapDcEffect`) 그대로. RegenPerSec 제외(절대치 — 후속).
- **적용 범위**: 아군 디펜더 전체, 매치 상시(현재 + 미래 배치 모두). BattleBridge 의 드림캐쳐 매치 효과 레지스트리(`_activeDcEffects`)를 대상축 `All` 로 일반화해 재사용. **ECS 변경 0.**
- **반입 등록은 set-then-apply**: GameManager 는 `SetDreamstones` 로 pending 전달만, `BeginPlacement` 가 레지스트리 클리어 직후 적용. `BeginPlacement` 의 클리어에 등록이 지워지는 순서 결함(설계 크리틱 CRITICAL)과 재시작 leak 을 동시에 차단.
- **화면 구조 = 슬롯 탭 → 피커 모달** (2026-07-04 UI 리뷰 C안): 메인 화면은 슬롯만(유닛 7 + 스톤 4), 슬롯 탭 시 피커 모달에서 선택/해제. 유닛·스톤 동일 인터랙션, 뎁스는 모달 1장. 기존 "보유 그리드 클릭 = 첫 빈 슬롯" 방식은 폐기.
- **드래프트 폴백 경로 미적용**: 스톤은 스쿼드 소속. 스쿼드 미설정 → 드래프트 폴백 시 스톤 개념 없음.
- **밸런싱 placeholder**: 등급 캡(유니크 30% 확정, Common 8 / Rare 12 / Epic 20 잠정)과 스톤 수치는 데이터 — 밸런싱 단계에서 숫자만 조정.

## 후속 후보 (범위 밖)

- 스톤 획득처(가챠/꿈런 파밍/교환), 보유 인벤토리/수량 모델
- 강화/합성/분해, 세트 효과, 슬롯 조건(스탯별 전용 슬롯 등)
- 스톤 아이콘/아트 (MVP 는 등급 색 + 텍스트 라벨)
- RegenPerSec 등 절대치 스탯 스톤
- 런타임 클램프 (크로스 소스 스태킹이 밸런스 문제가 되면)
- 다중 스쿼드 간 스톤 공유/배타 규칙 (보유 모델 도입 시)
- (크리틱 이관) headless 드림캐쳐 auto-pick 이 `BeginPlacement` 클리어 직전에 등록돼 지워지는 기존 버그 (PlacementPhaseView.cs:56-58 순서)
- (크리틱 이관) `DreamcatcherSelectionView.Summary` axis 라벨 체인에 `All`/default 가드 추가
- (크리틱 이관) 체력 스톤 표기 vs 실효(EHP +33.5%) 차이의 플레이어 노출 방식 (툴팁 등)

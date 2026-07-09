# Dreamcatcher Card Taxonomy — 타입(스쿼드/유닛) 축 도입 + 등급 캡 이전

> 상태: **작성 2026-07-09, 구현 대기**

## 목표

드림캐쳐 카드를 **타입축(스쿼드/유닛)** 으로 명시 분류하고, 덱 제약을 등급(고유)에서 타입(스쿼드)으로 옮긴다.

- **스쿼드 타입** = 축 매칭 스탯 버프(기존 stat 카드). `binding=Axis`.
- **유닛 타입** = 개별 유닛 부착 메커니즘(이번 세션 신규: 콕콕바늘/통통구슬/작별선물/마지막불꽃/가시갑옷). `binding=Unit`.

## 결정 사항 (사용자 확정 2026-07-09)

1. **명시적 `CardType { Squad, Unit }` 필드 신설.** `binding`(Axis/Unit)은 타겟팅 상세로 유지. type 은 자체 필드로 명확화.
2. **덱 캡을 스쿼드 타입으로 이전.** 기존 "고유(category==Unique) ≤2" → **"스쿼드 타입 ≤2"**. `DeckRules.MaxUnique` 제거, `MaxSquad=2` 도입. 덱 크기(10) 불변.
3. **`category`(Normal/Unique) 필드 유지, 역할 축소.** 이름 변경 없음. 덱 캡 역할이 타입으로 넘어가 **cosmetic 라벨/프레임 색상 전용**이 된다 → "일반-고유 덱 관계성" 제거됨.

## 작업 단위

| # | 문서 | 작업 |
|---|---|---|
| 0 | `0_type_field_and_deck_rule.md` | `CardType` enum + SO 필드 + `DeckRules` 캡 이전(Squad≤2) + deck-builder 캡 체크 갱신 |
| 1 | `1_asset_migration.md` | 카드 15장에 type 지정(스탯=Squad, 메커니즘 5장=Unit) + 기본 덱/카탈로그 검증 |

## Feature-wide 계약

1. **타입 = 자체 필드.** `CardType type` append(직렬화 끝). 기본값 = Squad(int 0). 기존 stat 카드는 zero-init 로 자동 Squad, 유닛 카드만 명시 Unit 지정.
2. **type ↔ binding 정합.** Squad 는 binding=Axis, Unit 은 binding=Unit 이 원칙(어긋나면 authoring 실수). 코드는 type 을 신뢰(binding 은 스쿼드 타겟 상세).
3. **덱 규칙 = 크기 10 + 스쿼드 ≤2.** `DeckRules` 는 catalog 로 카드의 `type==Squad` 개수를 세 ≤2 검증. Unique 기반 로직 삭제. 반복(repeat) 규칙은 기존과 동일(캡 없는 카드는 반복 허용).
4. **category 는 cosmetic 만.** deck-builder 프레임 색/라벨은 category 유지(당분간). 덱 유효성엔 무관.
5. **직렬화 append-only.** `CardType` 필드는 끝에 추가(기존 카드 값 보존).

## 후속 후보

- ~~deck-builder 프레임 색/라벨을 category → type 전환~~ **완료 2026-07-09** (Unit=금/Squad=청, 라벨 UNIT/SQUAD).
- ~~category 무효화~~ **완료 2026-07-09** — 소비처 0, SO 필드만 dormant 유지(back-compat). 완전 삭제는 후속 cleanup.
- ~~기본 덱 유효성~~ **해결 2026-07-09** — config 무제한(maxSquad/maxUnit=-1, deckSize=10)으로 기본 덱 valid. 실제 타입 캡은 config 조정 시 발효.
- 무의식(Subconscious) 컨셉/전용 슬롯 도입(현재 미구현, deck-builder 후속).

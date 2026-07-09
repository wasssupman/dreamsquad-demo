# 2 — Active 타입 데이터 (CardType.Active + skill 래핑 카드 에셋)

## 목적

공용 스킬을 드림캐쳐 우산 아래로 흡수하는 데이터 토대. `CardType.Active` 케이스와 스킬 참조 필드를 만들고, 기존 스킬 6종을 각각 래핑하는 Active 카드 에셋을 만든다. 런타임 소비자는 아직 없다 — 컴파일 + 에셋만.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` (enum 케이스 + 필드 append)
- `Assets/_Project/Data/.../DreamcatcherCards/Active_*.asset` (신규 6종 — 기존 카드 에셋 폴더 관례)

## 구현

1. **`CardType.Active`** — enum **끝에 append** (Squad=0, Unit=1 보존).
2. **`DreamcatcherCard.skill`** (`SkillData`, nullable) — Active 카드가 래핑하는 스킬. 직렬화 끝 append + 주석. `type==Active` 일 때만 유효(다른 타입은 무시). 정의 계층 원칙 유지 — `SkillData` 는 순수 데이터 SO 라 ECS 무참조 계약 위반 아님.
3. **Active 카드 에셋 6종**: 기존 `SkillData` 6종(SlowField/PowerSurge/RapidFire/Tornado/Meteor/Portal — `Assets/_Project/Data/Skills`) 각각에 `DreamcatcherCard { type=Active, skill=해당 SkillData, id="active_{skillId}", displayName=스킬명 }`. `art` 는 비움(뷰가 `skill.uiTint` 폴백 — unit 6). `effects`/`mechanics`/`attackMods` 는 빈 채로.
4. **카탈로그 미등록**: Active 카드는 덱빌더 구성 대상이 아니다(매판 공통 배정). `DreamcatcherCardCatalog` 에 넣지 않는다 — 매핑은 unit 4 의 컨트롤러가 serialized 배열로 직접 참조.
5. **덱 규칙 무변경**: `DeckRuleConfig`/`DeckRules` 는 아웃게임 10장 덱만 다룬다 — Active 는 판내 주입이라 규칙 대상 아님.

## 완료 기준

- [ ] 컴파일 클린.
- [ ] Active 카드 에셋 6종 생성, 인스펙터에서 type=Active + skill 참조 확인.
- [ ] 기존 카드 에셋 직렬화 값 변동 없음 (zero-init: type 기존값 보존, skill=None).
- [ ] 덱빌더 페이지에 Active 카드가 노출되지 않음 (카탈로그 미등록 확인).

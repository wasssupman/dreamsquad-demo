# 9 — 시트 새로 짜기 (시트)

## 목적

옛 시트 모양을 남기지 않는다. 카드 전용 탭 2개를 없애고(unit 8 이 비운다), 열 이름 규칙을 하나로, 기획자가 속는 칸(액티브 쿨다운 · 안 쓰는 CC 칸)을 없앤다.

## 탭 (10 → 8)

| 탭 | 한 줄 = | 키 | 바뀌는 것 |
|---|---|---|---|
| `Skills` | 효과 하나 | `effect_id` | + `ally_filter` · **export 는 그 종류가 쓰는 칸만**(unit 8 의 종류별 사용 칸 표) · import 는 안 쓰는 칸에 값이 있으면 경고 |
| `SkillOwners` | 소유 줄 하나 | (`owner_kind`, `owner_id`, `slot`) | **한 탭 유지**(D3) · 검증 U20 추가 |
| `Cards` | 카드 고유 값 | `id` | 옛 `DcCards` 개명 · + `host_kinds` · `cooldown_sec` · `needs_two_tiles`(액티브 — 오늘 에셋에만 있어 시트로 못 고쳤다) |
| `Defenders` · `Enemies` | 스탯 | `id` | 열 이름만 스네이크 |
| `DcSkills` | 액티브 문안(U18 분리 전까지) | `id` | 수치 칸(`range` · `magnitude` · `durationSec` · `cooldownSec` · `warningSec`) **삭제** — 문안만 움직이던 두 번째 원천 |
| `DcConfig` · `CostConfig` | 설정 | `id` | 열 이름만 스네이크(통합은 후속) |
| ~~`DcCardEffects`~~ · ~~`DcAttackMods`~~ | — | — | 은퇴(→ `Skills` + `SkillOwners`) — **코드 은퇴는 unit 8 단계 B 에서 끝남**(`5825fed06` — `DcSheetTabs` 5탭 · DTO · 재구성 · export · push). 이 unit 에 남은 것 = 시트 쪽(탭 삭제 · 서버 키)뿐 |

- 열 이름 = 전 탭 스네이크(`[JsonProperty]` — C# 필드 이름은 그대로 · `SkillSheetDto` 선례). 정보 열(`_skillId` · `_effect` — 임포터가 안 읽는다)은 `_` 머리 규약을 `tables.md` 에 적는다.
- **U20 검증(시트 층 — 코어 · 빌더는 소유자 종류를 묻지 않는다)**: 공격 변형 효과(`HeavyStrike` · `ProjectileBounce` · `FrontmostTarget` · `DamageVsSleeping`)를 가리키는 소유 줄은 `owner_kind = defender` 이거나 숙주가 방어유닛인 `card` 여야 한다. 적 소유 줄 · 적 숙주 카드 줄이면 **그 소유자의 시트 소유 줄 전체를 건너뛴다**(에셋의 소유 줄은 그대로 · 보고). 줄만 빼고 재구성하면 인스펙터 저작이 로그인마다 지워진다 — 인스펙터 저작은 계약 13 이 받아들인 구멍이다.
- 적 소유 `FactionStatBuff` 의 `ally_filter` ≠ `All` 이면 경고(직업 · 코스트는 방어유닛의 값이라 아무도 못 받는다).

## 변경 대상

- `Scripts/Data/StatImport/{DcSheetTabs,DcSheetImportDto,DcSheetApplier,SkillSheet,SkillSheetDto,UnitStatImportDto,CostConfigDto}.cs` · 런타임 refresher(`Scripts/Core/Dreamcatcher/DcSheetRuntimeRefresher.cs` · `AllRuntimeRefresher.cs`)
- `Editor/UnitStatImport/{DcSheetExporter,UnitStatExporter,CostConfigSheetExporter,SheetPushPayload,UnitStatImportWindow}.cs` — `SheetPushPayload.cs:46-47` 이 `nameof(DcCardDto.attachType)` 를 헤더로 밀어 넣는다 → 스네이크 뒤 옛 열이 되살아나지 않게 JSON 이름으로.
- 테스트: `Tests/EditModeAssets/SkillSheetRoundTripTests.cs`(전 탭 왕복 = 스냅샷 동치) · `Tests/EditMode/UnitStatImport/*`
- 문서: `tables.md`(§0 표 목록 · §4 · §7 · §8 을 이 표로) · `5_sheet_io.md` 「실제 시트 설정」(전 탭 헤더 · 서버 키)

## 시트 교체 절차 (사용자 몫 · 에이전트는 시트에 쓰지 않는다)

한 번에 바꾼다: 에디터 export → 새 헤더로 탭 재생성 → 서버 키 설정. 그전까지 로그인 자동 import 는 옛 열을 못 읽어 **아무것도 바꾸지 않는다**(모르는 헤더 무시 → 빈 칸 = 그대로 · `SheetEnvelopeParser.cs:62-76` · 필드 매퍼는 C# 이름으로 짝짓는다 · 없는 탭 = 섹션 없음 — 비평이 확인). 옛 탭은 보관 · 삭제 자유.

## 완료 기준

- 왕복: 라이브 에셋 → 전 탭 JSON → 파싱 → 적용 → 굽기 스냅샷 동일.
- `Skills` export 에 종류가 안 쓰는 칸 0(오라 줄의 `cc_kind` 같은 잡음 0).
- U20: 적 소유 줄 × 튕김 → import 거절 테스트 · 방어유닛 소유 줄 × 튕김 → 통과.
- 액티브 쿨다운: `Cards.cooldown_sec` 를 고치면 굽기 값과 카드 문안이 같이 바뀐다(테스트).
- EditMode 아웃게임 · Assets lane 초록(알려진 빨강 외 0).
- 구현 2026-09-29(에이전트 · 시트 쓰기 0 · 에셋 무변) — `609a52ef3` 스탯 탭 스네이크(Defenders · Enemies · CostConfig) · `18de2e81f` DC 탭(`DcCards` → `Cards` · 새 칸 `host_kinds` · `cooldown_sec` · `needs_two_tiles` · 평면 탭 diff 줄 · `DcSkills` 수치 칸 삭제(+ `cost` — 아무도 안 읽는다) · 스네이크 · 정보 열 `_skill_id` · `_effect` · push 헤더 시드 = JSON 이름 · 에디터 prefs `.v4`) · `e2a29fe35` `Skills` = 종류가 쓰는 칸만(export) · 안 쓰는 칸 경고 + 무시(import) · `3c8c279aa` U20 · `237a97596` 전 탭 왕복(8탭 · 사본 줄 = 원본 줄 · 스냅샷 2 동일) · 문서 커밋(헤더 정본 `5_sheet_io.md` + 문서 대조 테스트 `SheetHeaderDocTests`).
- 결정: 안 쓰는 칸 = **경고하고 무시**(쓰지 않음 · 요약 `ignored cells N`) · 라이브 효과 62 개가 안 쓰는 칸(`cc_kind` Stun · `stack_kind` Fire — unit 4 이전 기본값)을 들고 있다 → 에셋에만 남고 해시에 그대로(재베이크 0) · 시트엔 안 보인다 · 폐기 호환 열 `attack_damage` 는 DTO 에 남되 헤더 문서에서 뺐다.
- 남은 것(사용자): `5_sheet_io.md` 「실제 시트 설정」 절차대로 8탭 재생성 · 서버 키 · 옛 탭 보관/삭제. (리드): Unity EditMode(아웃게임 · Assets) · 전 탭 왕복 테스트 초록 확인.

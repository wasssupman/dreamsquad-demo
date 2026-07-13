# 0. 카드 id/displayName 수치 제거 + 네이밍 규칙

## 목적

수치가 박힌 카드 id(9종)·displayName(10종)에서 수치를 제거하고, displayName 을 스탯 기준 한글 통일 이름으로 재작성한다. 수치는 effects[].percent(SoT)에만 두고 덱빌더 자동 라인이 표시한다.

## 네이밍 규칙 (확정 2026-07-13)

- **id = `{axis}_{stat}` 안정 슬러그**(매그니튜드 숫자 없음). 티어 변형 시 `_2`,`_3` 서수 접미.
- **displayName = 스탯 테마 + 축 한정어**(게이머 슬랭 톤). 티어 변형 시 이름 뒤 서수(예: `올 핵딜 2`).
- 스탯 테마: 공격력→**핵딜** · 공격속도→**폭타** · 유효체력→**존버** · 이동속도→**발업**
- 축 한정어: All→**올** · Cost1→**1코** · ClassRanger→**레인저** · ClassGuardian→**가디언**

## 변경 표 (확정)

| 현재 id | → id | 현재 displayName | → displayName | effect(수치 SoT) |
|---|---|---|---|---|
| `all_atk_8` | `all_atk` | All ATK +8% | 올 핵딜 | AttackDamage 8% |
| `all_move_10` | `all_move` | All Move +10% | 올 발업 | MoveSpeed 10% |
| `cost1_as_5` | `cost1_as` | Cost-1 AS +5% | 1코 폭타 | AttackSpeed 5% |
| `cost1_hp_10` | `cost1_hp` | Cost-1 HP +10% | 1코 존버 | EffectiveHealth 10% |
| `guardian_as_8` | `guardian_as` | Guardian AS +8% | 가디언 폭타 | AttackSpeed 8% |
| `guardian_hp_15` | `guardian_hp` | Guardian HP +15% | 가디언 존버 | EffectiveHealth 15% |
| `ranger_as_10` | `ranger_as` | Ranger AS +10% | 레인저 폭타 | AttackSpeed 10% |
| `ranger_atk_10` | `ranger_atk` | Ranger ATK +10% | 레인저 핵딜 | AttackDamage 10% |
| `ranger_hp_12` | `ranger_hp` | Ranger HP +12% | 레인저 존버 | EffectiveHealth 12% |
| `guardian_fortress` | (유지) | Guardian Fortress (HP +50% / AS -50%) | 가디언 풀존버 | EffectiveHealth 50 / AttackSpeed -50 |

- `guardian_fortress` 는 id clean → displayName 수치만 제거. effects 2슬롯(HP+/AS-)은 자동 라인 2줄로 표시됨.
- sub_deepsleep/sub_dreamhaste 는 이번 제외(README 후속 후보).

## 변경 대상

- 카드 SO 9종의 `id` + 10종의 `displayName` 필드 (`Assets/_Project/Data/Dreamcatcher/Card_*.asset`). 파일명(.asset)·GUID 는 불변.
- `Assets/_Project/Tests/PlayMode/DreamcatcherDeckCarryInTest.cs` (`ranger_atk_10`→`ranger_atk`, 3곳).
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs` (`ranger_atk_10`→`ranger_atk`, 3곳).
- 리네임 후 **Export Dreamcatcher → 시트 페이로드** 실행 → 사용자가 시트 DcCards.id + 자식탭 cardId 갱신(붙여넣기).

## 구현 노트

- SO 편집은 unityMCP `manage_scriptable_object`(또는 일회성 MenuItem) 로 id/displayName 만 수정 — effects/mechanics/GUID 불변.
- 저장된 덱(`DeckSave.cardIds`)에 옛 id 가 있으면 그 카드만 해석 실패(fallback) — dev 환경이라 무시, 리네임 후 새로 저장.
- displayName 에서 수치를 빼도 덱빌더 %는 `effects[]` 자동 라인으로 유지됨을 Play 로 확인.

## 완료 기준

- [x] 9 id + 10 displayName 리네임, effects/mechanics/GUID 불변 (git diff 로 id/displayName + 스키마 재직렬화 라인만 변경 확인).
- [x] EditMode 718 pass / PlayMode 덱 캐리인 pass (하드코딩 id 갱신 반영).
- [x] Export 재시드 → 시트 3탭 전량 교체(orphan 제거) → import 왕복 **Matched 60/0/0, 값 드리프트 0**.
- [ ] (선택) 덱빌더 상세 % 표시 Play smoke — 코드상 `DreamcatcherDeckBuilderView.cs:499-506` 이 effects[].percent 로 렌더하므로 회귀 위험 낮음. 미실시.

확인 2026-07-13 — 왕복 IDENTICAL. displayName=게이머 슬랭 확정(올 핵딜/1코 폭타/가디언 존버/…). 시트 rename 은 upsert 불가 → 전량 교체로 처리(README 후속 후보 기록).

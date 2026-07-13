# 2. Handoff Summary — dreamcatcher-data-hygiene

## Commit

- (이 커밋) feat(dreamcatcher): data-hygiene unit 0·1 — 카드 id/이름 수치 제거 + DcMechanics 판별자 blank export

## Implemented

- 스탯 버프 카드 10종 id/displayName 에서 매그니튜드 수치 제거. id=`{axis}_{stat}` 슬러그(all_atk_8→all_atk 등 9종), displayName=게이머 슬랭 한글(올 핵딜/1코 폭타/가디언 존버/… 10종, guardian_fortress 는 id 유지·이름만).
- 수치 SoT 는 effects[].percent 단일화 — 덱빌더 자동 라인(`DreamcatcherDeckBuilderView.cs:499-506`)이 표시하므로 이름에서 빼도 UI % 유지.
- DcMechanics exporter 가 payload 판별자(ccKind/stackKind/buffStat)를 소비 kind 행에만 emit, 나머지는 blank(null→키 생략). 시트 노이즈(전 행 Stun/Fire/AttackDamage 기본값) 제거.
- 테스트 하드코딩 id 갱신(ranger_atk_10→ranger_atk): DcSheetImportTests, DreamcatcherDeckCarryInTest.

## Key Files

- `Assets/_Project/Data/Dreamcatcher/Card_*.asset` — 리네임된 10종(Card_AllAtk8/AllMove10/Cost1As5/Cost1Hp10/GuardianAs8/GuardianHp15/RangerAs10/RangerAtk10/RangerHp12/GuardianFortress). 파일명·GUID 불변, id/displayName 만 변경.
- `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` — MechanicRow 판별자 조건부 emit.

## Verified

- EditMode 718 pass / 0 fail / 2 pre-existing skip. PlayMode 덱 캐리인 1 pass.
- 시트 3탭(DcCards/DcCardEffects/DcMechanics) 전량 교체 후 import 왕복: Matched 60/unmatched 0/skipped 0, 값·텍스트 드리프트 0(diff=rename+스키마 재직렬화만). 시트 판별자 분포 정확(frost_arrow/ember_bite/devouring+last_stand).

## Notes

- **id 는 GUID 무관 안정 슬러그**: 카탈로그·기본덱·기프트config 는 GUID 참조라 리네임에 안 깨짐. id 문자열 의존은 시트·저장된 덱(DeckSave.cardIds, dev-disposable)·테스트뿐.
- **시트 rename 은 전량 교체로만 가능**: 챗봇 upsert(키=id)는 옛/새 id 를 별개 행으로 봐 orphan 을 남긴다. 재시드 때 실제로 겪음 — DcCards/DcCardEffects/DcMechanics 를 JSON 으로 전량 교체해 해결(dead 컬럼 binding/placementWarmupSec 도 이때 제거됨).
- 검증 import 가 재저장한 비-리네임 카드 20종의 스키마 재직렬화는 restore(스코프 유지) — 시트값과 동일함이 왕복으로 확증됐으므로 무손실.

## Follow-up

- description 수치 드리프트(farewell "100"↔실제 500 등) — 이번 스코프 제외, README 후속 후보.
- triggerPeriod vs triggerPeriodSeconds 이름 겹침 / triggerPeriodSeconds dead — README 후속 후보.
- sub_* 카드 네이밍 통일, 시트 replace-mode export 프롬프트 — README 후속 후보.

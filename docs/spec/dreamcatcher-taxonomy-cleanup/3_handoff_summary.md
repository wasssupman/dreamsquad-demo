# 3 — handoff summary

드림캐쳐 카드 택소노미의 이중 필드·잔재를 정리한 B1 리팩터 인계 지도. 최신 계약은 README + 0~2 번호 문서가 우선.

## Commit

- (this commit) `refactor(dreamcatcher): dreamcatcher-taxonomy-cleanup — CardBinding 제거·commit 경로 통합·warmup 잔재 제거`
- 3 유닛을 한 커밋으로 묶음(사용자 지시).

## Implemented

- `CardBinding {Axis, Unit}` enum·`DreamcatcherCard.binding` 필드 제거. 런타임 스코프 경계는 이제 `CardType` 하나에서 파생(`type != Unit` = 무부착 가드).
- `DcSheetApplier.WarnTypeBindingMismatch`(type↔binding 일관성 경고) + `DcCardDto.binding` 컬럼 제거.
- 컨트롤러 `CommitSquad`/`CommitUnit` → 단일 `CommitAttach(entryId, host)`. `TryGetUsableAttach`(Squad|Unit 허용) 추가.
- bridge 단일 디스패처 `ApplyDreamcatcherCard(host, card)` 추가 — `type` 으로 `ApplyDreamcatcherCardToUnit`(host mechanics) / `ApplyDreamcatcherCardHosted`(axis 버프) 라우팅. 두 apply 머신은 그대로(테스트가 직접 검증).
- `DreamcatcherCardDragSlot` Defender 커밋 switch 축소: Unit/Squad → `CommitAttach` 하나, Active-DefenderUnit 만 분기.
- 구 Squad warmup 잔재 `placementWarmupSec` 필드·sheet 컬럼·테스트 흔적 제거.

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — 정의 계층(enum·필드 제거)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 가드 전환 + 디스패처
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — CommitAttach
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 커밋 switch 축소
- `Assets/_Project/Scripts/Data/StatImport/{DcSheetImportDto,DcSheetApplier}.cs` — sheet 스키마

## Verified

- `dotnet build` 4개 어셈블리(Runtime·Tests.EditMode·Tests.PlayMode·Editor.UnitStatImport) **오류 0개**.
- 제거 심볼 grep 0건: `CardBinding`·`CommitSquad`·`CommitUnit`·`placementWarmupSec` (설명 주석 제외).
- **한계**: Unity 세션 unavailable 로 테스트 **실행**은 미실시. 컴파일만 검증.

## Notes

- `category`/`CardCategory` 는 **제거 안 함** — `DreamcatcherDeckBuilderView.IsSubconscious` 가 무의식 프레임 색으로 사용하는 살아있는 소비처. (구 "no consumer" 주석은 오기, 정정함.)
- `binding`/`placementWarmupSec` 는 Unity 필드명 직렬화라 제거 안전 — 다음 로드 때 카드 에셋 YAML 에서 키만 드롭(의미는 `CardType` 이 보존). 신규 에셋 없음 → .meta 변경 없음.
- 디스패처는 `ApplyDreamcatcherCardHosted` 에 host 를 넘기지만 그 머신은 host 를 안 씀(축 집합 적용, host 는 컨트롤러가 회수 앵커로 `_attachedTo` 에 기록). 의도된 동작.

## Follow-up

- Unity 복구 시 테스트 실행: `PlacementAuraTest`·`DreamcatcherEffectTest`·`DreamcatcherCombatDamageTest`·`DcSheetImportTests`.
- **B2 (dreamcatcher-scope-payload-unify)**: baked-slot revoke/미래상속 머신 → Squad/Unit 을 scope 값으로 강등해 `CardType` 물리 삭제, "모두에게 바운싱" 실현. README 후속 후보 참조. cross-scope 페이로드를 요구하는 신규 카드가 생길 때 착수.

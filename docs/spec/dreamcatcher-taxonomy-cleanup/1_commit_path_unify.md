# 1 — commit/apply 경로 통합

## 목적

host-부착 카드(Squad·Unit)의 near-duplicate 커밋 경로 2벌을 하나로 합친다. 호출자(컨트롤러·DragSlot)는 타입 분기 없이 **단일 진입점** 하나만 본다. 스코프 분기(축-집합 vs host-only)는 bridge 의 **단일 디스패처**가 `CardType` 을 보고 고른다. 실제 apply 머신 2개(축 StatModifier / host baked-slot)는 근본이 달라 그대로 유지한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`
  - `CommitSquad`/`CommitUnit` → 단일 `CommitAttach(int entryId, Entity host)`.
  - `TryGetUsableAttach`(Squad|Unit 허용, Active 거부) 추가. 기존 `TryGetUsable(expected)` 는 Active 전용으로 잔존.
  - 관련 주석 갱신(`re-checked in CommitUnit` → `CommitAttach`).
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
  - `ApplyDreamcatcherCard(Entity host, DreamcatcherCard card)` 디스패처 추가: `type==Unit → ApplyDreamcatcherCardToUnit`, 그 외 → `ApplyDreamcatcherCardHosted`. int 핸들 규약(<0 실패 / 0 무회수 / >0 회수핸들) 그대로 반환.
  - 기존 두 apply 메서드는 public 유지(테스트가 각 머신을 직접 검증).
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`
  - Defender 모드 커밋 switch: Unit/Squad 두 case → `CommitAttach` 하나. Active-DefenderUnit 만 분기 잔존(셀 대상 스킬 캐스트).
- `Assets/_Project/Tests/PlayMode/PlacementAuraTest.cs`
  - `ctrl.CommitUnit(entryId, host)` → `ctrl.CommitAttach(entryId, host)` (카드 type=Unit → 디스패처가 ToUnit 라우팅, 동작 동일).

## 구현

1. bridge 디스패처 추가 → 컨트롤러가 apply 머신 선택을 bridge 로 위임.
2. 컨트롤러 두 Commit 을 `CommitAttach` 로 병합(캡 확인·attach·spend tail 은 이미 공유).
3. DragSlot switch 축소, 테스트 호출 개명.

## 완료 기준

- [x] `CommitSquad`/`CommitUnit` 심볼이 코드베이스에 없다 (grep 0건, 주석 제외). `CommitAttach` 로 대체.
- [x] 4개 어셈블리 `dotnet build` 오류 0개 (Runtime·EditMode·PlayMode·Editor.UnitStatImport).
- [ ] Squad/Unit 커밋 동작 회귀 검증: 로직 불변(개명·디스패처 위임)이나 Unity 테스트 실행은 미실시.

확인: 2026-07-12 — dotnet build 컴파일 검증 (테스트 실행은 미실시).

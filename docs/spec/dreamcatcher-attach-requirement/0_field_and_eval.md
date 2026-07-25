# 0 — 정의 계층 필드 + 순수 판정 함수

## 목적

`DreamcatcherCard` 에 부착 제한 3필드를 append 하고, 제한 판정을 `DreamcatcherAttachEval` 의 **독립 public 순수 함수**로 추가한다. 이 unit 은 정의 계층 + 순수 로직만 — bridge 배선은 unit 1.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs`
- `Assets/_Project/Tests/EditMode/DreamcatcherAttachEvalTests.cs`

## 구현

1. enum append (DreamcatcherCard.cs, 파일 상단 enum 군 옆):
   ```csharp
   // dreamcatcher-attach-requirement unit 0 — 부착 시점 정적 술어. 발동 게이트
   // (DcGateKind)와 레이어가 다르다. append-only.
   public enum DcAttachRequireKind { None, Class, UnitId }
   ```
2. 카드 필드 append (직렬화 순서 안정 — 클래스 끝, `leakAllowanceCost` 뒤):
   ```csharp
   public DcAttachRequireKind attachRequire;   // None = 제한 없음(기존 카드 zero-init)
   public DefenderClass attachRequireClass;    // attachRequire==Class 일 때만 읽힘
   public string attachRequireUnitId;          // attachRequire==UnitId 일 때만 읽힘 (DefenderUnitData.id)
   ```
3. 순수 판정 — **`WouldApply` 시그니처는 건드리지 않는다**:
   ```csharp
   // bake/UI 시점 전용 — per-frame 호출 금지(mechanics 배열과 동일 규율).
   public static bool MeetsAttachRequirement(DreamcatcherCard card, DefenderClass hostRole, string hostUnitId)
   ```
   - `None` → true
   - `Class` → `attachRequireClass != DefenderClass.None && hostRole == attachRequireClass` (무효 설정 = fail-closed false)
   - `UnitId` → `!string.IsNullOrEmpty(attachRequireUnitId) && string.Equals(hostUnitId, attachRequireUnitId, StringComparison.Ordinal)`
   - `kind` 가 판별자이므로 나머지 두 필드는 해당 분기에서만 읽힌다(잔존 값 inert).

**왜 `WouldApply` 확장이 아닌가** (리뷰 확정): ① 커밋 경로(`ApplyDreamcatcherCardToUnit`)는 애초에 `WouldApply` 를 부르지 않고 자체 preflight 체인을 쓴다 ② 비-Unit 호출처(`BattleBridge.Dreamcatcher.cs:698`)는 Squad 조기 return 이라 새 인자를 절대 읽지 않는데 더미를 넘겨야 한다 ③ 독립 함수로 두면 `DreamcatcherAttachEvalTests` 편집이 3곳 → **0곳**. 제약 8(불필요한 추상 레이어 금지)에도 부합.

## 완료 기준

- compile 통과 (콘솔 에러 0).
- EditMode 신규: None 통과 / Class 일치·불일치 / Class×None fail-closed / UnitId 일치·불일치 / UnitId×빈문자열 fail-closed — 각 1 어서션 이상.
- 기존 `DreamcatcherAttachEvalTests` **무수정으로** 전부 green (`[Test]` 10개).

확인 2026-07-25 — Unity 컴파일 에러 0 · EditMode 전체 1320건(1318 pass / 0 fail / 2 기존 Ignore) · `DreamcatcherAttachEvalTests` 16/16 pass(기존 10 무수정 + 신규 6). 카드 에셋 diff 0.

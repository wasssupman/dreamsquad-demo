# 4 — 문안 포매터 접두 ("○○ 전용")

## 목적

제한이 걸린 카드의 문안 최상단에 "가디언 전용" / "{유닛명} 전용" 접두를 포매터가 조립한다. 이 unit 은 **포매터와 골든 테스트만** — 실제 화면 노출은 unit 5(배선).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs`
- `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs`

## 구현

1. `Body` / `BodyLinesOnly` 에 **optional resolver 파라미터** 추가:
   ```csharp
   public static string Body(DreamcatcherCard card, Func<string, string> unitNameOf = null)
   ```
   optional 이므로 기존 호출처 4곳은 **무수정으로 컴파일**된다(unit 5 에서 실제 resolver 를 넘긴다).
2. 접두 조립:
   - `Class` → `"{클래스 한글명} 전용"`. 라벨은 기존 `AxisLabel`(레인저/가디언)을 재사용하되 `DefenderClass` 는 Fighter/Caster/Support 까지 있으므로 이 파일에 `DefenderClass` 전 케이스 라벨을 추가한다. 하드코딩 아님 — enum→표시문자 매핑은 문안 계층의 정당한 소유물(`AxisLabel` 선례).
   - `UnitId` → `"{unitNameOf(id)} 전용"`, resolver 가 null 이거나 빈 문자열을 돌려주면 **id 문자열 폴백**.
   - `None` → 접두 없음(기존 문안 그대로).
3. 무효 설정(Class×None 등)에는 접두를 붙이지 않는다 — fail-closed 는 게이트와 validator 가 담당하고 문안은 조용히 넘어간다(플레이어에게 "None 전용" 같은 문구를 보이지 않는다).
4. `description` 필드(시트 수기 미러)는 건드리지 않는다 — 접두는 포매터 조립분에만.

## 완료 기준

- compile 통과, 기존 호출처 4곳 무수정.
- EditMode 골든: Class 제한 첫 줄 = "가디언 전용" / UnitId + resolver 성공 = "{표시명} 전용" / resolver null = id 폴백 / 무제한 카드 = 기존 골든 무변화 / 무효 설정 = 접두 없음 — 5케이스.
- 기존 `DreamcatcherCardTextTests` 전부 green.

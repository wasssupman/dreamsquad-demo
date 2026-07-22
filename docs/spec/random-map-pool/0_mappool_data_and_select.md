# 0. MapDocumentPool SO + MapPoolSelect 순수 선택 함수

## 목적

풀의 데이터 컨테이너와, seed→인덱스 결정론 선택 로직을 만든다. 선택 로직은 아키텍처-중립 순수 함수(제약 10)로 분리해 EditMode 로 검증한다 — `Math.Abs(int.MinValue)` 오버플로·음수 modulo·count=1 같은 비자명 엣지가 있어 회귀 가치가 있다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentPool.cs` (신규)
- `Assets/_Project/Scripts/Data/MapGrid/MapPoolSelect.cs` (신규, 순수 static)
- `Assets/_Project/Tests/EditMode/MapPoolSelectTests.cs` (신규)

## 구현

**`MapDocumentPool`** (ScriptableObject, `[CreateAssetMenu]`):

```csharp
[Serializable]
public struct Entry
{
    public MapDocument document;   // 배틀필드
    public AttackDeck deck;        // 이 맵과 함께 도는 공격 덱 (적 패턴). null 이면 BattleBridge 레거시 deck 폴백.
}
[SerializeField] private List<Entry> entries = new();
public int Count => entries?.Count ?? 0;
public Entry Get(int i) => entries[i];
```

- `AttackDeck` 은 `Wassup.Data` 네임스페이스 → `MapDocumentPool` 이 참조. asmdef 경계 확인(둘 다 런타임 어셈블리).
- `MapDocument` 에 덱 참조를 넣지 않는다 — 맵 지오메트리가 공격 덱을 알면 소유가 뒤집힌다. 페어링은 풀 엔트리가 소유.

**`MapPoolSelect.SelectIndex`** (순수):

```csharp
public static int SelectIndex(int seed, int count)
{
    if (count <= 1) return 0;
    return (int)((uint)seed % (uint)count);  // uint 캐스트로 Abs(int.MinValue)/음수 modulo 회피, 항상 [0,count)
}
```

**테스트** (`MapPoolSelectTests`): 반환값 항상 `[0, count)`; `count=1`/`count=0` → 0; `seed=int.MinValue` 예외/오버플로 없음; 같은 (seed,count) → 같은 결과(결정론); count=2 에서 인덱스 0·1 둘 다 도달하는 seed 존재.

## 완료 기준

- [ ] compile 0 errors (신규 .cs 는 scope=all refresh — CS0246 cascade 회피)
- [ ] `MapPoolSelectTests` 전 케이스 green (EditMode 폴더는 `Assets/_Project/Tests/EditMode/`)
- [ ] `MapDocumentPool` 이 인스펙터에서 `Entry` 리스트(document + deck)로 노출됨

# 2 — Controller: handle 배선 + host 사망 회수

## 목적

`CommitUnit` 이 `ApplyDreamcatcherCardToUnit` 의 int 반환(회수핸들)을 `_attachedTo` 에 저장해,
host 사망 시 기존 `OnDefenderDied` → `RevokeDreamcatcherEffects` 경로가 오라를 회수하게 한다.

## 변경 대상
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`

## 구현

### CommitUnit (bool→int 반환 대응)
```csharp
public bool CommitUnit(int entryId, Entity target)
{
    if (!TryGetUsable(entryId, CardType.Unit, out var card)) return false;
    if (AtAttachCap(target, card)) return false;
    int handle = bridge.ApplyDreamcatcherCardToUnit(target, card); // <0 실패 / 0 무회수 / >0 회수
    if (handle < 0) return false;         // 실패 = 무차감·무순환 (contract 9)
    return AttachAndSpend(entryId, card, target, handle);
}
```
- `AttachAndSpend(..., handle)` 는 이미 `_attachedTo[entryId]=(host,handle)` 저장 + `OnDefenderDied`
  가 `if (handle > 0) RevokeDreamcatcherEffects(handle)` 호출 → **오라 회수 경로 기존 재사용**.
- 0(무회수)·>0(회수) 둘 다 `AttachAndSpend` 통과. host 사망 시 handle>0 만 revoke.

### 주석 갱신 (L8)
`_attachedTo` 필드 주석의 "Unit cards: handle=-1" → 실제 규약(0=무회수 엔티티부착 / >0=회수 오라)으로 정정.

## 완료 기준
- [ ] 컴파일 클린. `ApplyDreamcatcherCardToUnit` 반환 int 로 정상 소비.
- [ ] 일반 Unit 카드(콕콕바늘 등, handle 0) 부착·사망 동작 회귀 없음.
- [ ] 오라 카드(느린 각성, handle>0) host 사망 시 RevokeDreamcatcherEffects 호출됨.

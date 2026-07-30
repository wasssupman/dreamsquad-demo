# 3 — 스쿼드 페이지 픽업

## 목적

페이지 진입 시 예약을 소비해 **새 프리셋을 만들고**(빈 내용, 즉시 디스크) **작업본을 랭커의 유닛·스톤으로 채운다**(미저장). 제외가 있었으면 개수를 안내한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs`
- `Assets/_Project/Tests/EditMode/Profile/PresetApplyPickupTests.cs` (신규)

## 구현

`OnCreatePreset` 의 생성부를 `private SquadPreset CreatePreset(string name)` 로 추출한다(실패 시 null). 호출처 2곳 — 사용자 `[+]`(이름 `"스쿼드 N"`)와 이 픽업. 상한/`CanPersist` 판정은 호출처가 먼저 하고 여기서 반복하지 않는다.

`OnEnable` 에 픽업을 끼운다. **자리는 `LoadWorking` 뒤**다 — 저장본(빈)을 복제한 다음 그 위에 랭커 편성을 얹어야 dirty 가 성립한다:

```csharp
LoadWorking(_viewingPresetId);
if (PresetApply.TryConsume(PresetApply.Target.Squad, out var req)) ApplyStaged(req);
EnterUnitMode(initial: true);
RefreshBarEntries();
```

`ApplyStaged` 순서(가드가 쓰기보다 앞 — 부분 적용 없음):

1. `CanPersist()` 실패 → `NoticePopup.ShowAlert` + `Debug.LogError`. 미주입/미로드를 조용한 무동작으로 위장하지 않는다(`confirmPopup` fail-closed 와 같은 정책).
2. `squads.Count >= PlayerProfile.MaxPresets` → 알림 `"프리셋이 30개로 가득 차 새로 만들 수 없습니다. 하나를 삭제한 뒤 다시 시도하세요."` 후 return. 여기가 삭제할 수 있는 화면이다(계약 7).
3. `PresetApply.FilterUnits(req.unitIds, catalog, out int du)` · `FilterStones(req.stoneIds, stoneCatalog, out int ds)`.
4. `CreatePreset(PresetApply.UniqueName(기존 이름들, req.presetName))` → null 이면 return.
5. `_viewingPresetId = created.id; LoadWorking(created.id);` — 빈 저장본을 복제해 작업본을 초기화.
6. 필터 결과를 작업본에 얹는다. `_workingUnits`/`_workingStones` 는 이미 슬롯 수만큼 `""` 로 차 있으므로 **앞에서부터 덮고 나머지는 비운 채로 둔다**(리스트 길이를 바꾸지 않는다 — `CopySlots` 가 보장한 불변식이다).
7. `du + ds > 0` 이면 알림 `"{N}개 항목은 현재 버전에서 사용할 수 없어 제외했습니다."` 제외가 없으면 알림 없음.

`Save()` 는 `CreatePreset` 안에서 한 번만(구조 변경 = 즉시 디스크). **작업본 내용은 저장하지 않는다** — `[저장]` 이 유일한 기록 경로다(계약 1).

`RefreshBarEntries()` 는 `OnEnable` 말미가 이미 부른다. 새 프리셋 셀의 썸네일은 **저장본**을 그리므로 비어 있는 게 맞다(목록은 "저장된 프리셋들"이다) — `[저장]` 후 채워진다.

## 완료 기준

- [x] 컴파일 그린
- [x] EditMode(리플렉션으로 실제 컨트롤러 구동 — `PresetCommitSemanticsTests` 패턴):
  - 예약 후 진입 → `squads.Count` +1, 이름 = `"{owner}의 덱"`, `_viewingPresetId` = 새 id
  - 작업본 = 필터 결과 · **저장본은 빈 상태 유지** · `IsDirty()` true
  - `[저장]` → 저장본에 반영 + dirty 꺼짐
  - `[되돌리기]` → 작업본이 빈 프리셋으로 (저장본 기준 복원)
  - 같은 이름이 이미 있으면 ` 2`
  - 상한 가득 → 프리셋 미증가 + **예약은 소멸**(재진입에 되살아나지 않는다)
  - `IsLoadedThisSession == false` → 미증가, `LogAssert.Expect(LogType.Error, ...)` 로 가드 발화를 못박는다
  - 예약 없이 진입 → 기존 동작(확정 프리셋 표시) 무변경
- [x] 페이지 픽업 계약 고정: 스쿼드 적용 시 스쿼드만 +1, 드림캐쳐 목록 불변, 작업본 dirty 및 저장/되돌리기 동작을 실제 컨트롤러 테스트로 검증

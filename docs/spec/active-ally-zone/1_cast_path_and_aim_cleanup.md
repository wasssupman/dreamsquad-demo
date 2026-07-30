# 1 — 캐스트 경로 교체 + 조준 예고 정리

## 목적

`PowerSurge`/`RapidFire` 를 즉시 버프에서 장판 스폰으로 바꾸고, 그에 따라 필요 없어진 조준
장치(아군 카운트 예고, 0기 거절)를 걷어 액티브 6종의 조준을 완전히 같게 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`
- `Assets/_Project/Scripts/Data/SkillData.cs` (`TargetsAllies` 의 남은 용도 재판정)

## 구현

1. **`ApplyAllyBuff` → 장판 스폰**: `CastSkillAtTile` 의 두 case 가 즉시 모디파이어 대신
   `EffectSpawner.SpawnAllyBuffField(...)` 를 호출한다. 캐리어 수명 = `skill.durationSec`.
   **반환값은 항상 성공** — 대상 0기 거절이 사라진다(계약 2).
   `affectedCount` 는 스폰 시점 반경 내 아군 수를 **로그용으로만** 채운다(성공/실패 판정에 쓰지 않음).
   ⚠ **이 자리에서 모디파이어를 직접 걸지 않는다** — 계약 3-1. 즉발감을 위해 `skill.durationSec` 로
   한 번 걸어 두면 이후 갱신이 그 값을 내릴 수 없어 스냅샷 동작으로 회귀한다. 지연 0 은 시스템이
   매 프레임 도는 것으로 이미 보장된다.
2. **`CountDefendersInRange` 제거**: 유일 소비자였던 조준 예고가 사라진다. 로그용 카운트는
   `CollectAlliesInRange` 를 bridge 내부에서 직접 쓴다(공개 API 를 남기지 않는다).
   스크래치 리스트는 **하나만 남긴다** — 남는 용도가 로그 스냅샷뿐이므로 `_allyApplyScratch` 를
   그 이름에 맞게 정리하고 `_allyCountScratch` 는 삭제(L4).
   ⚠ `CastSkillAtTile` 의 "아군 0기 거절" 근거 주석 블록도 **함께 삭제**한다 — stale 근거 주석은
   이 레포가 기록해 둔 자체 함정이다(L5).
3. **드래그 슬롯 정리**:
   - `_aimAllyCount`, `AllyCountStatus` 캐시, `_allyStatusCount/_allyStatusCache` 삭제.
   - `AimCellValid` 는 "보드 안 + (포탈 2단계면 입구≠출구)" 만 본다.
   - 상태줄: `놓으면 이 위치에 시전` 단일(포탈 2단계 문안은 유지).
   - `TargetsAlliesNow()` 소비처가 사라지면 함께 삭제.
4. **`SkillData.TargetsAllies` 는 이 unit 에서 삭제한다**(unit 2 를 기다리지 않는다 — 순환 의존은
   실재하지 않는다). 소비처는 `DreamcatcherCardDragSlot` 3곳(`StatusFor` / `UpdateTileAim` /
   `TargetsAlliesNow`)뿐이고 전부 이 unit 이 지우는 코드다. unit 2 는 이 판별을 쓸 수 없다 —
   아군 장판의 뷰는 `AllyBuffField` 스폰 경로에서 생기므로 아군/적 구분이 **구조적**이다.
   `IsPortal` 은 소비처가 살아 있어 유지.
5. **로그**: 기존 `RecordSkillUsage` 그대로. `affected_count` 의 의미가 "지금 안에 있던 아군 수"
   (스냅샷)로 바뀌는 것을 주석에 명시 — 이후 장판에 들어온 유닛은 세지 않는다.

## 완료 기준

- [ ] 아군 없는 빈 칸에 공격폭증/속사를 놓을 수 있고 각성치가 차감된다(장판이 생긴다).
- [ ] 조준 중 상태줄이 6종 전부 동일. 붉은 "범위에 아군이 없습니다" 가 더 이상 나오지 않는다.
- [ ] `CountDefendersInRange` 잔존 참조 0. 선행 spec 의 관련 PlayMode 케이스를 장판 기준으로 갱신.
- [ ] 콘솔 에러/워닝 0.

# 1. AreaSleep 페이로드 정의 + bake [ECS]

## 목적

실드 파열 시 "N타일 내 가장 가까운 M명을 L초 수면"의 데이터 정의 + bake. 신규 페이로드 kind 하나 + defender bake 브랜치. (드림캐쳐 A 의 데미지는 기존 `SelfTileAoe` 로 처리 — 신규 없음.)

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind` 에 `AreaSleep = 16` **append**.
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — defender bake 스위치에 `AreaSleep` 브랜치 추가.

## 구현

1. **enum**: `DcPayloadKind { …, BountyMark=15, AreaSleep=16 }`. 필드 재사용(신규 `DcPayloadSpec` 필드 0): `magnitude=M`(적 수 cap)·`tileRange=N`(Chebyshev 반경)·`duration=L`(수면 초). `ccKind` 불필요(payload 자체가 Sleep 확정 — `DcCcKind` 에 Sleep 추가 안 함).
2. **bake 브랜치**(`SelfTileAoe` 다음): 검증 — `magnitude >= 1`(대상 있음)·`tileRange >= 1`·`duration > 0`, 위반 시 경고+skip(타 payload 선례 동형). 통과 시 `slot.tileRange = N`·`slot.duration = L` 복사(magnitude=M 은 slot 초기화에서 이미 복사됨). 투사체/뷰 불요.
3. 트리거 무관 — OnShieldBreak+AreaSleep 도 generic slot builder(388, `trigger=m.trigger.kind`)로 실림. defender bake 경로엔 OnShieldBreak-적대 게이트 없음(확인). 보스 arm 게이트(BakeNightmareMechanics)는 별 경로라 무관.

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- (다음 유닛에서 실증) AreaSleep 카드 bake 시 DcTriggerSlot 에 tileRange/duration/magnitude 실림, 미달값 카드는 경고+skip.
- 실행(적 선정+Sleep)은 유닛 2, 카드는 유닛 3.

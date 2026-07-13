# 0 — 카드 비행 presenter (손패 UGUI → 타겟 추적 가속 비행)

## 목적

스와이프-부착 **커밋 성공** 순간, 그 손패 카드가 자기 슬롯에서 타겟(유닛/타일)으로 **가속 접근해
찰싹 스쿼시**하고 **즉시 dissolve** 되는 비행 연출을 만든다. 이 unit 은 **비행 + splat + 소멸**까지만.
묵직 임팩트 반응(유닛 펀치/플래시/링/카메라 킥/SFX)은 unit 1. 타일 타겟 배선은 unit 2.

## 변경 대상

- **신설** `Assets/_Project/Scripts/UI/Dreamcatcher/CardAbsorbFlightPresenter.cs` (MonoBehaviour, View 소유·구동).
- **수정** `DreamcatcherCardDragSlot.cs` — `CommitNow` 시그니처 확장(target descriptor 캡처) + 성공 시 발화.
- **수정** `DreamcatcherHandView.cs` — presenter 참조 노출/생성, 슬롯 art 스냅샷 게이트, 비행 트리거 진입점.

## 구현

### 트리거 (계약 공백 닫기 — README 배선 §)
`CommitNow(Func<bool>)` → `CommitNow(Func<bool> commit, in FlightTarget target)`. `ok==true` 일 때
슬롯의 현재 스크린 위치(`slot.rect`)를 발사점으로, `target` 을 도착점으로 presenter 에 넘겨 발화.
실패(`!ok`)는 기존대로 `RestoreSlotHome` — **연출 없음, 비용 0**.
- Defender/Attach: `FlightTarget.Unit(host Entity)` — 유닛 뷰 앵커 **Transform 추적**.
- Active-Defender/Tile/Portal: unit 2 에서 `FlightTarget.World(Vector3)` 로 일반화. unit 0 은 **유닛 케이스만** 발화.

### 좌표 제공 (Transform/Vector3 공용)
presenter 는 `System.Func<Vector3?>` **worldProvider** 를 받아 **매프레임** 호출 → 카메라 project 로 스크린 좌표 산출.
- 유닛: `() => bridge.TryGetUnitViewAnchor(host, out var t) ? t.position : (Vector3?)null` (행진 추적).
- 타일(unit 2): `() => bridge.GridToWorldCenterVector(cell)` (고정).
- provider 가 null(유닛 뷰 소멸) → 마지막 스크린 좌표로 비행 완료.

### 비행 (하스스톤식 3축 아치 — 코루틴 시간 구동)
고정 종점이 아니므로 baked tween 대신 presenter 코루틴. **rise → slam** 2페이즈 시간 구동:
- **rise**(EaseOut): 스크린 Bézier 로 apex(start/end 위쪽 하늘)까지 솟구치며 감속 hang, depth 스케일 **커짐**(카메라 근접).
- **slam**(EaseIn): apex→타겟으로 **가속 하강**하며 depth 스케일 **작아짐**(보드로 꽂힘). 착지 = 임팩트.
매프레임 worldProvider→screen 재투영으로 end(타겟) 추적. apex 높이는 초기 수평거리 비례(짧은 비행 축소). 미세 tilt.

### 고스트 비주얼 (셰이더 dissolve 회피)
슬롯 `art.sprite` 로 **단순 UGUI Image** 고스트를 Canvas(HandPanel 상위/오버레이)에 생성. 크럼플 face
(`UiCardFaceMesh`) **재사용 안 함** — 커스텀 셰이더 채널 함정 회피, 머무름 없으니 평평해도 무방.

### 찰싹 + dissolve
- splat: 닿는 1~2프레임 가로↑ 세로↓ 스쿼시(scale).
- dissolve(~0.08s): scale→0 + alpha→0 동시 페이드 후 고스트 Destroy. **머물지 않음.**

## 완료 기준

- [ ] compile 통과, 콘솔 에러 0.
- [ ] Play: 손패 카드를 유닛에 스와이프-부착 성공 → 고스트가 슬롯에서 그 유닛으로 가속 비행 → splat → 즉시 소멸.
- [ ] 유닛이 행진 중이어도 고스트가 유닛 위치를 추적해 정확히 안착(스크린샷/영상).
- [ ] 커밋 취소/실패(빈 곳 touchup, 타겟 없음) 시 **고스트 안 뜸**, 카드 손패 복귀.
- [ ] ECS 시뮬 변경 0(순수 프레젠테이션). 임팩트 반응(펀치/링/SFX)은 아직 없음 = unit 1 범위.

---
**확인 2026-07-13**: compile 클린(에러 0) + presenter 스폰 no-throw 스모크 통과. 사용자 Play 검증 통과
(손패 카드→유닛 가속 비행→찰싹 splat→즉시 소멸, 행진 추적 정확, 취소 시 고스트 없음). 커밋: 56d78acf.

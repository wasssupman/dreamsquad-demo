# 0 — 통합 Billboard 컴포넌트

## 목적

틸트/페이싱 회전 로직이 SpineUnitView(인라인) · PropBillboard(Full/YAxis) · QuadUnitView(셰이더)
**3갈래로 분산**돼 있다. 단일 `Billboard` MonoBehaviour 로 통합해 틸트 공식의 유일 소유자를 만든다.
이 단위는 **회귀 없는 리팩터**가 목표 — 동작 변경(각도 변화)은 unit 2/4 에서.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Presentation/Billboard.cs`
- 수정: `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` (인라인 틸트 → Billboard 위임)

## 구현

`Billboard` MonoBehaviour:

```csharp
public enum BillboardMode { None, YAxis, Full, Tilted, TiltedDynamic }
```

- `[ExecuteAlways]` 불필요(런타임 전용; 에디터 프리뷰는 unit 1/2 에서 카메라로 확인). `LateUpdate` 에서 회전 갱신.
- 필드: `BillboardMode mode`, `float tiltAngle`(**주입값** — 캐릭터/프랍 레이어가 각자 다른 값 전달), `bool flip180`.
- 회전 공식:
  - `None`: 회전 안 함 (return)
  - `YAxis`: `LookRotation(target.pos − cam.pos, up)` 의 yaw (PropBillboard 기존 로직 이관)
  - `Full`: `cam.rotation`
  - `Tilted`: `Euler(tiltAngle, 0, 0)` (월드 X, 카메라 yaw=0 전제)
  - `TiltedDynamic`: 스텁만(미사용; 후속). 당장은 `Tilted` 로 폴백
  - `flip180` 시 마지막에 `Rotate(0,180,0)`
- 카메라 캐시: `Camera.main`, 무효화 시 재취득 (PropBillboard 패턴 동일).
- 회전 대상은 `transform` (피벗=발). 위치/스케일/ScaleX 는 절대 건드리지 않는다.

SpineUnitView 변경:
- `LateUpdate` 의 `transform.rotation = Euler(CharacterBillboardTilt,0,0)` 제거.
- `Spawn` 끝에서 `Billboard` 컴포넌트 추가/설정: `mode=Tilted`, `tiltAngle=BattleBridge.CharacterBillboardTilt`.
  - 단, **이 단위에서는 값 의미 불변** — Tilemap=0, Legacy=35 그대로 들어가 동일 동작 유지.
- ScaleX 페이싱(`FaceToward`)·cast anchor 로직은 그대로(틸트와 독립 채널).

> 주의: `CharacterBillboardTilt` 는 빌드 시 모드별로 세팅되는 static. Billboard 가 매 프레임 읽지 말고
> Spawn 시 1회 주입(스폰 후 모드 안 바뀜). 모드 전환은 맵 재빌드 = 재스폰이므로 안전.

## 완료 기준

- compile 통과, 콘솔 에러 없음.
- Tilemap 모드 Play: 캐릭터 기존과 동일하게 직립(틸트 0 유지) — **회귀 없음**.
- Legacy3D Play: 35° 틸트 그대로 유지.
- PropBillboard 는 이 단위에서 미변경(unit 4 에서 수렴).

# 0. 은퇴한 카메라 프리셋 계통 제거

## 목적

`ApplyTilemapCameraPreset()` 는 **호출부가 0** 이다. 호출 라인은 주석 처리돼 있고(`BattleBridge.cs:1172`), 메서드 자신이 `[은퇴 — camera-direction unit 0]` 이라 적혀 있다. 카메라 포즈는 `CameraDirector` + `CameraDirectionConfig` 가 단독 소유하며, 이 메서드가 카메라를 만져봐야 다음 `LateUpdate` 에 덮여 무효다.

메서드만 남기면 "언젠가 쓸 것" 처럼 보이지만, 실제로는 `CameraDirector` 와 **경쟁하는 두 번째 카메라 소유자**의 잔해다. 계통 전체를 걷어낸다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `ApplyTilemapCameraPreset()` 메서드 (`:5938~` 주석 포함 전체)
  - `[SerializeField] tilemapCameraPresetRect` / `tilemapCameraPresetIso` (`:140-141`)
  - 주석 처리된 호출 라인 (`:1172`)
- `Assets/_Project/Scripts/Data/BoardCameraPreset.cs` — **파일 삭제**
- `Assets/_Project/Data/Camera/CameraPreset_TilemapRect.asset` (+ `.meta`) — **삭제**
- `Assets/_Project/Data/Camera/CameraPreset_TilemapIso.asset` (+ `.meta`) — **삭제**

## 구현

- 위 5개를 삭제한다. `BoardCameraPreset` 타입을 참조하는 코드는 삭제 대상 SerializeField 2개와 삭제 대상 메서드뿐이므로 컴파일 파급이 없다.
- **`CameraDirectionConfig.asset` 은 건드리지 않는다.** 이름이 비슷하지만 살아있는 `CameraDirector` 의 설정이다.
- `BattleBridge.cs:5952~5964` 의 보드 bounds 산출 로직은 `CameraFramingMath` + `TryGetPlayfieldWorldBounds` 로 이미 대체됐다. 옮겨 살릴 것 없음.
- 두 `.asset` 은 `BattleScene.unity` 가 guid 로 참조하지만, 참조를 담는 SerializeField 가 같은 커밋에서 사라지므로 그 YAML 키는 orphan 이 되어 역직렬화되지 않는다. **씬은 편집하지 않는다.**
- `.meta` 를 반드시 짝으로 삭제·스테이징한다.

## 완료 기준

> ✅ 검증 2026-08-12 — compile 0 errors. EditMode **2192 중 실패 0**(통과 2189, skip 3 = 전부 기존 `Ignore`).
> 콘솔을 비운 뒤 `BattleScene` 로드 → **error 0 / warning 0**: 씬이 삭제된 에셋 guid 를 아직 들고 있으나
> 대응 SerializeField 가 사라져 역직렬화되지 않는다(orphan 키 무해 확인 = 본 단위의 핵심 리스크).
> Play 진입→종료 정상, 보드 전체 프레이밍 유지, 콘솔 error/warning 0. `BattleScene.unity` 무변경
> (에디터 `isDirty: false` + git status 부재). 순변경 −184/+5.

- Unity compile 0 errors.
- EditMode 전체 green (회귀 0). 본 단위는 테스트를 수정하지 않는다 — 수정이 필요하면 "참조 0" 전제가 틀린 것이므로 정지하고 재조사한다.
- Play 1판: 카메라 프레이밍·연출이 정리 전과 동일. 맵 진입 시 보드 전체가 화면에 들어온다.
- **콘솔 신규 error/warning 0** — 특히 씬의 orphan guid 참조로 인한 missing asset 경고가 없는지 확인한다.
- `git status` 에 `BattleScene.unity` 가 본 커밋의 스테이징에 포함되지 않았는지 확인.

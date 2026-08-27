# unit 18 — 포스트 볼륨이 스테이지 소유로 넘어간다 (DoF 은퇴 · 비네트 개통)

## 목적

`map-diorama-stage` 가 전역 Post 볼륨을 씬에서 스테이지 프리팹 안으로 옮겼다. 그 결과
`CameraDirector.postVolume`(SerializeField)이 `null` 로 끊겨 **마음 스트레스 비네트가 화면에
아무것도 그리지 않는다**(`SetStressVignette` 첫 줄에서 return). 스테이지 볼륨은 런타임
인스턴스라 씬에서 미리 배선할 수 없다 — 참조를 **브리지가 밀어주는 방식**으로 바꾼다.

동시에 DoF 는 전부 걷어낸다. 스테이지 프로파일마다 `DepthOfField` 오버라이드가
`active: false` 로 저작돼 있고 `ApplyDof` 는 `active` 를 켜지 않아, 볼륨을 배선했어도
DoF 는 나오지 않는다. 상태별 흐림(unit 9·13)은 여기서 은퇴한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — DoF 구동 제거, `postVolume` 을 주입 필드로
- `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — `DofSolution` · `BlendDof` 제거
- `Assets/_Project/Scripts/Presentation/CameraFramingMath.cs` — `DofRange` 제거
- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — `dofEnabled/dofStart/dofEnd/dofMaxRadius` 제거
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` — 두 레시피의 dof 키 4개 제거
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 스테이지 볼륨 push (빌드 시) / 해제 (teardown 시)
- `Assets/_Project/Editor/MapStageCameraFraming.cs` — 결과 문자열의 `dof=` 항 제거
- `Assets/_Project/Tests/EditMode/CameraComposeMathTests.cs` · `CameraFramingMathTests.cs` — DoF 테스트 제거

## 구현

**볼륨 seam = 브리지 단방향 push.** `SetBoardBounds` 와 같은 계약이다 — Director 는 맵·브리지에서
볼륨을 당겨오지 않는다(그 유혹이 경계 우회의 입구다). 브리지는 스테이지 조립이 성공한 뒤
`_stageInstance.GetComponentInChildren<Volume>(true)` 로 찾아 `SetPostVolume` 으로 넘긴다 —
마커(`GoalMarker`/`SpawnMarker`)를 스테이지에서 스캔하는 기존 경로와 같은 방식이라
프리팹에 새 필드를 요구하지 않는다. teardown 에서는 `null` 을 밀어 파괴된 볼륨을 붙들지 않는다.

**프로파일이 스테이지마다 다르다**(Street/Subway). `Volume.profile` 은 sharedProfile 의 런타임
인스턴스라 스테이지가 바뀌면 **다른 객체**가 온다. 그래서 `SetPostVolume` 은 비네트 캐시
(`_vignetteWritten`)를 리셋한다 — 안 하면 새 프로파일의 첫 write 가 "같은 값" 으로 판정돼
건너뛰어지고, 스테이지 교체 후 비네트가 한 판 내내 죽는다.

**볼륨이 없는 스테이지는 경고 1회.** 조용히 죽는 것이 이 결함의 본질이었다.

DoF 는 코드·데이터·테스트에서 전부 삭제한다. 되살릴 때는 git 에서 꺼낸다(unit 9·13 문서는
설계 이력으로 남는다). 비네트는 `CameraDirector` 에 그대로 둔다 — 포스트 볼륨 소비자가
비네트 하나뿐이라 전용 컴포넌트는 지금 과잉이다(사용자 결정 2026-08-26).

## 완료 기준

- [ ] 코어 EditMode 테스트 통과 (DoF 테스트 삭제분 제외, 나머지 초록)
- [ ] `dof` 식별자가 `Assets/_Project/Scripts` · `Tests` · `Editor` 에서 0건
- [ ] Play: 마음 스트레스가 1단계 이상일 때 화면 테두리에 비네트가 보인다
- [ ] Play: 맵을 바꿔(스테이지 교체) 다시 진입해도 비네트가 계속 동작한다
- [ ] 콘솔 에러 0 — 볼륨 없는 스테이지에서는 경고 1회만

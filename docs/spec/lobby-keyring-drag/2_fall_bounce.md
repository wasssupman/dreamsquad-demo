# 2 · 중력 낙하 + 바운스 착지 + 재잡기 + 클릭 가드

## 목적

unit 1 의 임시 즉시 스냅을 실제 연출로 교체: 놓으면 중력 낙하 → 바닥에서 작은
바운스 후 정지 → 행동 재개. 낙하 중 다시 잡기 허용, 드래그/낙하 중 클릭 리액션 차단.

## 변경 대상

- 수정: `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs`
- 수정: `HelloLobbyRoamer.cs`, `WorldLobbyCharacter.cs` — OnPointerClick 가드
- 신설: 낙하 스텝 EditMode 테스트 (기존 EditMode 테스트 asmdef 위치를 따른다)

## 구현

**Falling 상태** (LobbyKeyringDrag):

- OnEndDrag: 리그 파괴, x = 놓은 위치 클램프(landingMinX/MaxX), 스윙 속도의 y 성분을
  초기 낙하 속도로 승계(툭 놓는 연속감), Falling 진입.
- 매 Tick: `vy -= gravity·dt`, y 적분. 회전은 낙하 동안 0 으로 보간 복귀.
- 착지(y ≤ 바닥): `|vy| ≥ bounceMinSpeed` 이면 `vy = -vy × bounceDamping` 반동,
  미만이면 y 스냅 + 회전 0 + `ResumeFromKeyring()` → Idle. (기본 튜닝에서 눈에
  보이는 바운스 1회가 목표 — unit 3 에서 확인.)
- **재잡기**: Falling 중 OnBeginDrag → Suspend 재호출 없이(이미 suspended) 리그
  재생성 + Dragging 재진입. 낙하 속도는 스윙 속도 초기값으로 승계.

**낙하 계산 분리**: 중력 적분 + 착지/반동 판정을 순수 static 함수로 분리
(예: `LobbyKeyringDrag.FallStep(ref y, ref vy, floorY, dt, in settings값) → bool landed`).
EditMode 테스트 1~2개: ① 높은 낙하 → 반동 1회 이상 후 결국 floorY 정지,
② `bounceMinSpeed` 미만 착지 → 반동 없이 즉시 정지.

**클릭 가드** (hello/world 각 `OnPointerClick`):
`LobbyKeyringDrag.IsBusy`(Dragging∨Falling) 이면 리액션 무시. 참조는 Awake
`GetComponent`. (이 프로젝트에서 드래그 후에도 클릭이 발화한 전례 —
`DraftCardView.cs` 가드 패턴 승계.)

## 완료 기준

- compile 클린, EditMode 테스트 통과.
- (unit 3 에서 시각 확인): 공중에서 놓으면 가속 낙하 → 작은 바운스 1회 → 정지 →
  행동 재개. 낙하 중 캐릭터를 다시 잡으면 키링 모드 재진입.
- 드래그 직후/낙하 중 클릭해도 리액션이 발화하지 않는다.

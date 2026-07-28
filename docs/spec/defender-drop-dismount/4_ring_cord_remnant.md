# 4 — 고리·줄 잔류 페이드

## 목적

릴리스 시 고리(손가락 자리)와 줄이 즉시 사라지지 않고: 반동 동안 유닛과 연결 유지 → 분리 순간 줄 스냅 → 놓은 자리에서 페이드아웃(사용자 확정: "체조 바가 그 자리에 남는" 인상).

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `CleanupSession` 의 dismount 분기 + 잔류 구동을 `RunDropDismount` 에 통합

## 구현

- **detach**: dismount 경로의 커밋에서 `CleanupSession` 전에 고리(`_session.ring`)+줄(`_session.cordLine`) 서브트리를 preview root 에서 분리해 임시 홀더(`KeyringRemnant_{n}`)로 옮긴다. 이후 `CleanupSession` 의 `Destroy(_session.preview)` 는 실루엣만 파괴 — 세션 코드는 무변경(분리된 서브트리는 이미 root 밖).
- **반동 구간** (0 ~ dropRecoilSeconds): 고리는 릴리스 지점 고정, 줄 끝점만 dismount 코루틴이 매 프레임 비행 유닛 머리 위치(= 오버라이드 위치 + camUp·unitHeight, 캡처값)로 갱신 — 줄이 벙는 그림.
- **분리 프레임**: 줄 갱신 중단 → `dropCordSnapFade` 동안 줄 알파 페이드(자명한 lerp — 인라인, 함수 추출 금지). 고리는 `dropRingFade` 로 페이드.
- **자멸**: 잔류 홀더는 `max(dropCordSnapFade, dropRingFade) + 0.1s` 후 Destroy. dismount 가 abandon 되면 즉시 페이드로 전환(공중에 줄이 붙박이지 않게). 코루틴 사망(OnDisable) 대비 홀더 자체에 수명 타이머(`Destroy(go, t)`) 병행 — 고아 방지.
- 머티리얼: 줄/고리 공유 머티리얼(`_cordMaterial`)을 페이드용으로 **복제**해 잔류에만 적용(공유본 알파를 건드리면 다음 드래그 세션 줄이 투명해짐). 복제본은 홀더 파괴 시 함께 Destroy.
- 연속 드롭: 잔류 홀더는 드롭당 1개, 수명 <1s 라 동시 2~3개가 최대 — 풀링 불필요(기존 드래그 세션도 매번 GameObject 생성하는 비용 클래스).

## 완료 기준

- compile 클린 · Play 육안: 릴리스 자리에서 고리가 잠시 남았다 사라짐, 반동 동안 줄이 유닛을 따라 벙음, 분리 후 스냅 페이드
- 연속 드롭 3회 빠르게 → 잔류 오브젝트 누수 없음(Hierarchy 에서 자멸 확인), 다음 드래그 세션 줄 알파 정상
- 드래그 취소(무효 셀 릴리스) 경로는 잔류 미생성 — 기존 CleanupSession 그대로

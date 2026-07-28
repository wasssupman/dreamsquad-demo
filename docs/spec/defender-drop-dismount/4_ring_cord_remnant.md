# 4 — 고리·줄 잔류 페이드

## 목적

릴리스 시 고리(손가락 자리)와 줄이 즉시 사라지지 않고: 반동 동안 유닛과 연결 유지 → 분리 순간 줄 스냅 → 놓은 자리에서 페이드아웃(사용자 확정: "체조 바가 그 자리에 남는" 인상).

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `CleanupSession` 의 dismount 분기 + 잔류 구동을 `RunDropDismount` 에 통합

## 구현

- **detach**: dismount 경로의 커밋에서 `CleanupSession` 전에 고리(`_session.ring`)+줄(`_session.cordLine`) 서브트리를 preview root 에서 분리해 임시 홀더(`KeyringRemnant_{n}`)로 옮긴다. 이후 `CleanupSession` 의 `Destroy(_session.preview)` 는 실루엣만 파괴 — 세션 코드는 무변경(분리된 서브트리는 이미 root 밖).
- **반동 구간** (0 ~ dropRecoilSeconds): 고리는 릴리스 지점 고정, 줄 끝점만 dismount 코루틴이 매 프레임 비행 유닛 머리 위치(= 오버라이드 위치 + camUp·unitHeight, 캡처값)로 갱신 — 줄이 벙는 그림.
- **분리 프레임**: 줄 갱신 중단 → `dropCordSnapFade` 동안 줄 알파 페이드(자명한 lerp — 인라인, 함수 추출 금지). 고리는 `dropRingFade` 로 페이드.
- **자멸**: 페이드 완료 시 코루틴이 Destroy. 페이드 노브가 비행보다 길면 착지 후 꼬리 루프로 마저 굴린다(동결 방지). 코루틴 사망(OnDisable) 대비 생성 시 하드캡 `Destroy(go, t)` 병행 — 고아 방지. abandon 시엔 **즉시 파괴**(구현 중 정정 — abandon = teardown 맥락이라 페이드 연출이 무의미, 붙박이 방지만).
- 머티리얼: **복제 불필요**(구현 중 정정) — 페이드는 per-renderer 색(`SpriteRenderer.color` / `LineRenderer.start·endColor`)으로만 하므로 공유 머티리얼을 건드리지 않는다. 단 스타일 셰이더가 버텍스 색을 무시하면 페이드가 안 보이고 하드캡 파괴로 사라진다(우아한 열화).
- 연속 드롭: 잔류 홀더는 드롭당 1개, 수명 <1s 라 동시 2~3개가 최대 — 풀링 불필요(기존 드래그 세션도 매번 GameObject 생성하는 비용 클래스).

## 완료 기준

- compile 클린 · Play 육안: 릴리스 자리에서 고리가 잠시 남았다 사라짐, 반동 동안 줄이 유닛을 따라 벙음, 분리 후 스냅 페이드
- 연속 드롭 3회 빠르게 → 잔류 오브젝트 누수 없음(Hierarchy 에서 자멸 확인), 다음 드래그 세션 줄 알파 정상
- 드래그 취소(무효 셀 릴리스) 경로는 잔류 미생성 — 기존 CleanupSession 그대로

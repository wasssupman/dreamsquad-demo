# 4 — 선택·부착 히트테스트 (footprint 영역 · 자석 · 앞면 우선 · 히스테리시스 구멍)

## 목적

탭 선택과 드림캐쳐 부착이 공유하는 유닛 픽킹을 재설계한다: ① 겹친 스프라이트에서 **앞에 보이는 유닛**이 뽑히게(중심 최근접의 구조적 오선택 제거 — 요구 문서 9절), ② 스프라이트보다 **넓은 픽 영역**(패딩) + **자석**(반경 내 최근접 — 요구 문서 7·8절), ③ 락온 히스테리시스의 **렉트 이탈 즉시 무력 구멍** 수정, ④ footprint 발밑 영역 선택(unit 1 의 셀 해석이 이미 제공 — 2차 폴백 그대로). 렌더 순서·입력 판정 분리는 유지된다(판정이 렌더를 **읽기만** 한다).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryPickDefenderAtScreen` 재설계(paddingPx·magnetPx·앞면 우선) + `ScreenDistanceToRect`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusConfig.cs` — `unitPickPaddingPx`/`unitPickMagnetPx` 노브
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 노브 전달 + 히스테리시스 거리 일원화
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 노브 전달 (handView 경유 같은 SO)

## 구현

- **픽 순서**: ① 확장 렉트(패딩) 포함 후보 중 **렌더 순서 앞면 우선**(`BoardSortOrder.Compute` — 판정이 렌더 순서를 읽는 것이지 렌더가 판정을 정하는 게 아니다. 동률은 중심 최근접) ② 없으면 자석: 확장 렉트까지의 거리 ≤ magnetPx 인 최근접(동률 앞면) ③ 호출부 2차 폴백(보드 셀 → footprint owner 해석)은 기존 그대로.
- **가려진 뒤 유닛**은 노출된 부위(머리 등 자기 렉트만 있는 영역)·발밑 셀·자석으로 여전히 선택 가능 — 앞면 우선은 «겹침 영역에서만» 판정을 바꾼다.
- **히스테리시스 일원화**: 기존 게이트(`curRect.Contains`)는 손가락이 현재 렉트를 벗어나는 순간 전환 지연이 0 이 되는 구멍. 거리(rect 까지) 비교로 바꿔 새 후보가 `lockSwitchHysteresisPx` 이상 우세할 때만 전환 — 자석 반경 안에서도 유효.
- **적 표식 경로의 «유닛 위» 판정은 기본 파라미터(패딩·자석 0) 유지** — 그 판정의 뜻은 «정말 유닛 위인가»라 넓히면 오탐.
- 유닛별 픽 영역·1폭 가로 전용 확대는 전역 노브로 시작 — per-unit 튜닝은 후속 후보(요구 문서 13절).

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 코어 무회귀 — 2494 전건 실패 0
- [x] 기본 파라미터(0,0) 호출의 포함 판정은 기존과 동일 집합(앞면 우선만 변경)
- [ ] 육안 Play: 밀집 배치에서 앞 유닛 탭 = 앞 유닛 선택 · 부착 자석·히스테리시스 체감 (**사용자 확인 대기 축**)

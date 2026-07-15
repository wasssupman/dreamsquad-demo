# 3 — 앰비언트 브리딩

## 목적

정지화면을 탈피하는 상시 저진폭 카메라 생명감. 인지 임계 이하의 아주 느린 dolly/sway로 "살아있는 화면"을 만든다. 은은함이 목적 — 눈에 띄면 실패.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — ambient 채널 실동작
- 수정 `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — 브리딩 파라미터
- 수정 `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — 브리딩 오프셋 순수 함수
- 에셋 `CameraDirectionConfig.asset` — 명시값

## 구현

- 다주기 sin 합성(주기 6~12s 2~3개 중첩) 기반 위치/미세 pitch 오프셋. config: `breathPosAmp`(권장 시작 0.02~0.05 월드유닛), `breathRotAmp`(0.1° 이하), `breathWaves[]`(주기+위상+축가중 — 위상도 SO에 둔다, "모든 수치는 SO" 계약 준수).
- 시계는 `Time.unscaledTime` 절대값이 아니라 **파동별 위상 누적기**(`phase += dt/period`, 각자 [0,1) wrap — 구현이 "주기 공배수 wrap"보다 강함) — 장세션 float 정밀도 저하로 저주기 sin이 양자화되는 것 방지. 같은 위상 → 같은 오프셋(결정론 유지).
- **합성 피크 주의**: 파동은 합산되므로 실제 피크 = `amp × Σ|축가중|` (현 에셋 x≈0.036, y≈0.047). 호버 셀 검증은 합성 피크 기준으로 본다. Play 중 SO에 파동을 **추가**해도 위상 누적기는 Awake 크기 고정이라 무시됨(축소/값 수정은 라이브 반영) — 튜닝 시 주의.
- 페이즈 비행 중 가중치 0으로 크로스페이드(README 계약), 비행 종료 후 수 초에 걸쳐 복귀 — 급격한 on/off 없음.
- 페이즈별 on/off 플래그(config) — 기본 Draft/Placement/Battle on, Gift/Result off(각 페이즈 자체 연출과 간섭 방지).
- 시간 진행은 unscaledDeltaTime(타임스케일 비의존). 절대 시각 미사용 — 위 위상 누적기 항목 참조.

## 완료 기준

- EditMode: 브리딩 오프셋 결정론(같은 t → 같은 값), 진폭 클램프 테스트.
- Play: 배틀 아이들 화면에서 미세 무빙 확인(스크린샷 2장 diff), 콘솔 클린.
- **호버 셀 안정성**: 브리딩 진폭은 "정지 포인터의 스크린→셀 매핑이 셀 경계에서 플립하지 않는" 수준이 상한 — 셀 경계 근처에 포인터를 고정한 드래그 상태로 수 초 관찰해 호버 셀이 흔들리지 않음을 확인. 흔들리면 진폭 하향이 정답(입력 잠금/히스테리시스 신설 금지).
- 사용자 Play 체감 확인 — "은은한가, 거슬리는가" 판정. 거슬리면 진폭 하향이 1차 조치.

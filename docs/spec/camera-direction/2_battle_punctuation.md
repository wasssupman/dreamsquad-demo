# 2 — 배틀 이벤트 구두점 (줌 펄스 + 킬 스트릭 셰이크)

## 목적

배틀 중 임팩트 순간에 카메라가 "반응"하게 한다. additive 오프셋만 — 카메라가 이벤트 지점으로 날아가는 연출(push-in)은 하지 않는다(브레인스토밍 결정). 1차 대상: (a) 헤비 임팩트 줌 펄스, (b) 킬 스트릭 연동 셰이크.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — punctuation 채널 실동작 (`ZoomPulse(strength)`, `SetShakeHeat(heat01)` API)
- 수정 `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — 펄스/셰이크 파라미터
- 수정 `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — 펄스 envelope/셰이크 진폭 순수 함수
- 배선: 헤비 임팩트 프레젠테이션 경로(응축된 일격/메테오 착탄이 도달하는 기존 프레젠터 — 구현 시 정확한 앵커 확정) + `Assets/_Project/Scripts/UI/ScoreHudView.cs`(킬 스트릭 heat)

## 구현

- **줌 펄스**: `ZoomPulse(strength)` → FOV 델타를 짧은 envelope(진입 빠르게, 감쇠 k²)로 얹음. config: `pulseFovDelta`(음수=줌인), `pulseSec`. 연타 시 max-hold — **해석 확정(리뷰 합의)**: 진폭은 현재/신규의 max(합산 누적 금지), 타이머는 재시작. 감쇠 중 재트리거는 피크로 스냅 복귀(재타격 체감 의도). 펄스 **타이머는 비행 가중치와 무관하게 실시간 감쇠** — 비행 중 얼렸다 뒤늦게 재생되는 지연 펄스 금지, 억제는 가중치(시각)가 담당.
- **최종 FOV 클램프**: 합성 마지막에 `finalFov = clamp(홈FOV + 페이즈델타 + 펄스, fovMin, fovMax)` — config에 클램프 범위 명시. 자동 보드-fit 프레이밍이 비활성인 현 구조에서 SO 튜닝만으로 위험 FOV(보드 잘림/왜곡)가 나오는 것을 코드 계약으로 차단. 클램프는 `CameraComposeMath`의 순수 함수(EditMode 테스트 대상).
- **셰이크**: 킬 스트릭 heat(0~1)에 비례하는 저진폭 연속 노이즈. `ScoreHudView`가 이미 유지하는 `_soundHeat`(킬 스트릭 heat, 사운드 pitch용)을 정규화해 `SetShakeHeat()`로 전달 — heat 산정 로직 중복 구현 금지. 노이즈는 index/시간 기반 결정론(sin 합성) — 프로젝트의 결정론 선호(seeded RNG 지양) 준수. config: `shakeMaxPosAmp`, `shakeMaxRotAmp`, `shakeFreq`.
- **heat 전달 타이밍**: `ScoreHudView`는 자기 `LateUpdate`(킬 flush 지점)에서 `SetShakeHeat` 호출. Director는 `-100` 순서라 이미 이번 프레임 합성을 끝냈으므로 **heat 반영은 다음 프레임(지연 ≤1프레임)** — 저진폭 연속 노이즈라 체감 무관, 이 지연을 계약으로 명시하고 순서 의존 버그로 오인하지 않는다.
- 헤비 임팩트 배선 — **앵커 확정(구현)**: `BattleBridge.DrainProjectileHitEvents`의 `PayloadKind.TileAoe && radiusWorld > 0` 착탄(시각 라우팅 무관, 이벤트당 1회). **faction-blind** — 보스 광역 폭격(SkyFall×TileAoe)도 펄스(적 헤비 임팩트도 드라마 연출로 수용). 응축된 일격 등 단일 타격 헤비 히트는 히트 이벤트에 배율 정보가 없어 **후속 후보로 이관** (ECS 신규 이벤트 채널 신설 금지 계약).
- heat 푸시 지점(구현): `ScoreHudView.Update`(감쇠 직후) — heat 상승은 같은 컴포넌트의 LateUpdate 킬 flush(order 0, Director -90 이후)라 지연 정확히 1프레임, 감쇠는 같은 프레임 반영.
- 두 채널 모두 페이즈 비행 중에는 가중치 0으로 감쇠(비행이 최우선).

## 완료 기준

- EditMode: FOV 클램프 양단, 셰이크 진폭-heat 비례/결정론/제로 가중 항등 테스트. (펄스 envelope 감쇠는 KickEnvelope 재사용 — 기존 테스트가 커버. 연타 max-hold 는 Director 상태라 순수 함수 테스트 대상 아님 — Play 검증 대체, 리뷰 합의)
- Play(스크립트 배틀 e2e): 헤비 임팩트 시 줌 펄스 발동 스크린샷, 킬 스트릭 상승 시 셰이크 진폭 증가 확인, 전투 없음 상태에서 카메라 완전 정지(오프셋 0 수렴).
- 사용자 Play 체감 확인.

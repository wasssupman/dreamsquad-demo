# 5 — 배치·부착 취소 유예 (연출 구간 = 유예, 결정 4)

## 목적

입력 실수 복구(요구 문서 11절). 전략 기능이 아니다. 「유예 중 실제 효과·행동 미시작」을 **상태로 보장**한다 — 배치는 `PendingDeployment`(전투 미참여 구간이 이미 있음), 부착은 **커밋 자체를 흡수 도착 프레임으로 지연**(되감을 것이 없음 — README 계약 9 의 «적용 시점 정렬» 선택지). 이미 발생한 전투 결과의 롤백은 없다.

## 변경 대상

- `Assets/_Project/Scripts/Core/PlacementCooldownRuntime.cs` — `ClearCooldown`(되감기용)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryCancelPendingDeployment`(환불·점유 해제·엔티티 수거) + `IsDefenderPendingDeployment` + `DefenderDeploymentCancelled` 이벤트
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 이벤트 선언부
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 취소 이벤트 구독(소진 상태 리페인트)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 유예 중 «되돌리기» 버튼(유닛 추종 오버레이) + 하마/활성화 코루틴 중단
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑭ 되돌리기 버튼 노브
- `Assets/_Project/Scripts/UI/Dreamcatcher/CardAbsorbFlightPresenter.cs` — 지연 커밋 비행(`FlyDeferred`) + 비행 중 고스트 탭 취소
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `FlyCardToUnitDeferred`(슬롯 예약·복귀)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — Defender 부착 경로만 지연 커밋으로 전환

## 구현

- **배치**: 드롭 커밋 후(비-facing 경로) 활성화 전까지 유닛 위에 «되돌리기» 버튼. 누르면 활성화 코루틴 중단 → 하마 비행 중단(등록부 키 제거 = 자진 종료 규약) → `TryCancelPendingDeployment`: footprint 점유 해제 + 코스트 **전액 환불** + 배치 쿨다운 되감기 + 엔티티 파괴·뷰 반납(퇴근 수거 형태 미러). 창은 최신 배치 1건(연속 배치 시 이전 창은 닫힘 — 실수 복구 목적에 부합). 방향 지정(facing) 유닛의 조준 페이즈 취소는 기존 backlog 항목대로 후속.
- **부착**: Defender 부착 경로의 커밋을 touchup 즉시 → **흡수 도착 프레임**으로 이연. 비행(~0.42초) 중 나는 카드 고스트를 탭하면 취소(카드 손패 복귀·무차감). 도착 시 커밋 실패(호스트 사망·재화 부족)면 카드 복귀 — «성공으로 보였는데 실패»는 카드가 돌아오는 것으로 표현. 적 표식·타일 캐스트는 즉시 커밋 유지(부착이 아니라 조준 캐스트 — 요구 문서 11절 범위 밖).
- 확정 비트(리티클 수렴·펄스)는 릴리즈 즉시 유지 — 조준 확정감은 유예와 별개 신호.

## rev (2026-08-30 사용자 지적) — 비행 중 버튼 노출 금지

「되돌리기 UI 가 비행 시뮬레이션 중에 나온다」. 유예 창은 커밋 시점부터 열려 있고(그 구간의
취소도 유효하다) 그 설계는 유지하되, **노출**만 착지 이후로 민다 — 날아가는 유닛을 버튼이
따라다니면 «아직 도착도 안 했는데 되돌리기»가 먼저 읽힌다. 판정 = `_activeDismounts` 재중.
노출 소유권도 `UpdateUndoWindow` 단독으로 옮겼다(`BeginUndoWindow` 가 켜면 위치를 잡기 전
프레임에 직전 배치 좌표에서 번쩍인다). 상세 = `8_dnd_silhouette.md`.

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 코어 무회귀 — 2494 전건 실패 0
- [x] 배치: 유예 중 취소 → 코스트·쿨다운·점유·트레이 소진 전부 원복, 활성화 후엔 버튼 소멸 (구현·정적 확인 — Play 검증은 아래 육안 축)
- [x] 부착: 비행 중 취소 → 게이지·덱·부착 수 전부 무변(커밋 자체가 없음), 도착 시 정상 부착 (구현·정적 확인 — Play 검증은 아래 육안 축)
- [x] 육안 Play: 배치 직후 버튼 탭 회수·부착 비행 탭 취소 체감 (**사용자 확인 대기 축**)

확인 2026-08-28 — 사용자 육안 Play 확인(multi-cell 4종 저작 상태). 커밋 해시는 handoff 에 기록.

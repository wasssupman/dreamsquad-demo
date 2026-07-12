# Unit Dreamcatcher Icons — 배치 유닛 머리 위 부착 카드 미니 아이콘

> 상태: **초안 2026-07-12** (사용자 승인 대기)
>
> 배경: Follow-up Backlog "유닛 상태 표현 / 인디케이터" 축 중 **드림캐쳐 부착 표기**만 분리한 spec. 모디파이어 인디케이터(버프/디버프 아이콘)는 스코프 밖 — backlog `unit-modifier-indicators` 항목 유지.

## 목표

배치된 방어유닛에 부착된 드림캐쳐 카드(Unit 부착 + Squad hosted)를 유닛 머리 위 **소형 미니 카드 스트립**으로 표시한다. 순수 프레젠테이션 — ECS 변경 0, 채널 0, 신규 에셋 저작 0.

## 검증 질문

> 카드를 유닛에 붙이면 그 유닛 머리 위에 해당 카드의 미니 아이콘이 즉시 나타나고, 호스트 사망(카드 회수)·매치 리셋 시 사라지는가? Unit/Squad 카드가 프레임 색으로 구분되는가?

## 데이터 소스 (실측 2026-07-12)

- **부착 레지스트리**: `DreamcatcherHandController._attachedTo` (`entryId → (host Entity, handle)`) — 부착/사망 회수/매치 리셋 수명주기 완비. `_deck.TryGetCard(entryId)` 로 카드 해석.
- **변경 신호**: `HandChanged(Used/Recovered/Reset)` 이벤트 — 부착 변경 시점마다 발화. per-frame poll 불필요.
- **아이콘**: `DreamcatcherCard.art` (1024×1536 타로, 부착 가능 16장 전원 할당). 각성 손패 UI 가 전투 중 이미 로드 → 재사용 메모리 비용 0. null 폴백 = 카테고리/타입 색 플레이트(덱 페이지 선례).
- **상한**: `AwakeningConfig.maxAttachPerUnit`(3, Unit+Squad 합산) → 고정 3슬롯, 오버플로 UI 불필요.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_attachments_read_api.md` | 계약 | `DreamcatcherHandController` 에 부착 목록 읽기 API + `AttachmentsChanged` 통지 — 기존 로직 변경 0 |
| 1 | `1_icon_strip_view.md` | 뷰 | `DcIconStrip` 스포너/뷰 — 미니 카드 3슬롯, 앵커 추종+빌보드(StatusFx 패턴), 타입별 프레임 틴트, 이벤트 리빌드 |
| 2 | `2_wiring_play_validation.md` | 배선+검증 | 씬 wiring(unity-feature-wiring) + Play e2e (부착→표시 / 사망 회수→소멸 / 리셋) |
| 3 | `3_handoff_summary.md` | 인계 | 종료 요약 |

## Feature-wide 계약

1. **읽기 전용 프레젠테이션.** ECS 컴포넌트/시스템/채널 변경 0. 부착 사실의 source of truth 는 `DreamcatcherHandController` 레지스트리 — 뷰는 그것만 믿는다 (ECS `DcTriggerSlot` 을 다시 읽지 않는다).
2. **이벤트 구동.** `HandChanged`(또는 신설 `AttachmentsChanged`) 시점에만 전체 리빌드. 배치 유닛 수는 그리드 상한이라 전체 리빌드로 충분. per-frame 은 앵커 추종/빌보드만.
3. **아이콘 = `card.art` 재사용.** 신규 스프라이트 필드/에셋 추가 금지(이번 spec). 가독성 문제 시 전용 `icon` 필드는 후속.
4. **Unit/Squad 프레임 구분.** Squad hosted 카드도 표시한다(호스트 사망 = 스쿼드 버프 소실이라는 전술 정보). 프레임 틴트로 구분.
5. **앵커/오프셋은 StatusFx 와 공존.** Sleep "Zz" 등 상태 연출과 y-오프셋 분리. 오프셋/스케일은 SerializeField 튜닝.
6. **매치 수명주기 준수.** 전투 teardown/재시작 시 전량 회수 (StatusFxSpawner.Clear 선례).
7. **Active 카드는 대상 아님.** 시전 즉시 소모 — 레지스트리에 안 남으므로 자연 제외.

## 파이프라인 커버리지 (상태연출 아키타입 대조)

`docs/reference/object-pipeline-map.md` §상태연출(StatusFx) 기준:

| 정거장 | 이번 spec | 비고 |
|---|---|---|
| 데이터 SO | N/A — 카드 `art` 직독, 신규 registry SO 없음 | 튜닝값은 SerializeField |
| 스폰 진입점 | `DreamcatcherHandController` 이벤트 → 스트립 스포너 | ECS reconcile 아님 (이벤트 구동) |
| ECS 컴포넌트 | N/A — 순수 Mono | |
| 이벤트 큐 | N/A — Mono 이벤트 (`HandChanged`) | 신규 채널 0 |
| View/Pool | `DcIconStrip` 뷰 + 유닛별 재사용 | StatusFxSpawner 풀링 선례 |
| 씬 배선 | 스포너 GameObject + SerializeField (unit 2) | unity-feature-wiring |

## 후속 후보

- **트리거 진행도 뱃지** [S/M] · 콕콕 바늘 "4/5" — `DcTriggerSlot.counter`(Combat) 읽기가 필요해 BattleBridge 스냅샷 경로 신설. 아이콘만으로 1차 검증 후.
- **전용 `icon` 스프라이트 필드** [S] · 타로 아트 축소 가독성이 문제될 때. append-only + art 폴백.
- **부착/회수 연출** [S] · 팝 스케일 인, 회수 시 페이드/손패 방향 플라이.
- **아이콘 탭 → 카드 상세** [S] · 부착 카드 확인 UX.
- **모디파이어 인디케이터** — 별도 spec (backlog `unit-modifier-indicators` 유지).

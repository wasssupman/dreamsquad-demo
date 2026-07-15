# Unit Dreamcatcher Icons — 배치 유닛 머리 위 부착 카드 미니 아이콘

> 상태: **완료 2026-07-12** (units 0~2 + handoff — Play e2e 검증 통과, 아키텍트 리뷰 반영)
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
- **아이콘 타입 다형화 시 seam** [S] · 2026-07-15 사용자 결정 = **지금 추상화하지 않는다**(제약 8 — 구현체 1개). 확장이 필요해지는 시점의 변경점은 딱 둘: (1) `DcIconStripView.Show(...)` 가 `List<DreamcatcherCard>` 로 하드 타입, (2) `card.type == CardType.Squad ? squadFrame : unitFrame` 2분기가 N 타입으로 안 늘어남. 2번째 타입이 실제로 오면 값 struct(`{ Sprite art; Sprite frame; }`) 추출 + 카드→struct 해석을 스포너로 이동 → 뷰가 `Wassup.Data` 의존을 잃는다(~15줄, 뷰 국소). 추측으로 미리 만들지 말 것.
- ~~**풀링 뷰의 stale anchor**~~ — **2026-07-15 조사 결과 존재하지 않는 버그**(후보에서 제외). "앵커가 리빌드 시점에만 해석되므로 풀이 Transform 을 재할당하면 스트립이 엉뚱한 유닛을 따라간다"는 우려였으나, `SpineUnitPool`/`QuadUnitViewPool` 은 **이름만 Pool 이고 뷰를 재사용하지 않는다** — 저장소가 `Dictionary<Entity, View>` 뿐이고 free-list 가 없으며 `TrySpawn` 이 매번 `new GameObject`, `Dispose` 는 `Destroy`. **Transform 이 다른 엔티티에게 넘어갈 경로가 없다.** `Rebuild` 의 `if (!TryGetUnitViewAnchor(...)) continue;` 로 기존 스트립이 옛 anchor 를 유지하는 경로도, 호스트 사망 시 회수가 `_attachedTo.Remove` → `AttachmentsChanged`(`DreamcatcherHandController.cs:248`)를 먼저 쏘아 `_toHide` 로 정상 회수되므로 안전하다. `continue` 가 실제로 타는 건 "부착은 있는데 뷰 미스폰"인 배치 레이스뿐이고 그땐 스트립이 아직 없다. (풀 구현이 실제 재사용으로 바뀌면 이 결론은 무효 — 그때 재검토할 것.)

## 사후 수정 이력

- **2026-07-15 `d815bf59`** — 머리 위 뱃지 좌표계 버그. 오프셋이 월드 +Y 라 원근 카메라(pitch 55°)에서 외곽 타일 아이콘이 화면 바깥으로 밀렸다(보드 끝 ≈57px@1080w). `HeadAnchor.Lift` 로 카메라 평면 전환 + 오프셋 2.6→1.64 등가 이전. 같은 결함이던 StatusFx/EnemyHitBar/DamageNumber 도 동반 수정. 함정 상세는 `docs/reference/lessons/03-rendering-assets.md` "머리 위 뱃지를 월드 +Y 로 띄우면" 참조 — **오프셋 값을 만질 땐 그 등가식을 먼저 읽을 것**. 같은 커밋에서 `DcIconStripSpawner` 를 `BattleBridge.TeardownCurrentBattle` 회수에 배선(계약 6 누락분).

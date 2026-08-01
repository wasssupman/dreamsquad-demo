# 4 — Handoff Summary — gimmick-recognition-upgrade

세션 인계 지도. 최신 계약은 README + 번호 문서 우선.

## Commit

| 해시 | 내용 |
|---|---|
| `cf7f2199` | docs — spec 작성(진입 리빌 중심 + 롤백 격리 유닛 구성) |
| `afada286` | unit 0 — 문구 4단 분리(`ruleLabel`/`summary`/`icon`) |
| `b725ea14` | unit 1 — `GamePhase.Gimmick` + `GimmickPhaseView` + 라우팅 + 씬 배선 |
| `4b257454` | unit 1 fix — 틴트 0.28→0.12 |
| `89a38d43` | 드라이 메커니즘 문구 + 레이아웃 SO 승격 |
| `03c489da` | unit 2 — 등장 효과음 배선 |
| `34b0d1aa` | unit 2 — 클립 생성·배선(ElevenLabs) |
| `a91edfb8` | 레드불·사직서 아이콘 착지 |
| `f027a04e` | chore — GimmickIcons 폴더 meta |
| `27317ac5` | 요약 읽을 시간 확보(텀 축소 + 두 줄·의미 색 + 탭 힌트) |
| `921829a4` | unit 3 — 배치 안내 카드 은퇴 + 홀드 2.2s |

## Implemented

- 기믹 안내가 **배치 페이즈 안 카드** → **배치 앞 독립 페이즈 리빌**로 이동. 배치 타이머·코스트·슬롯 선택과 인지 예산을 다투지 않는다.
- `GamePhase.Gimmick`(값 7, **enum 맨 뒤**) 신설. 훅은 `GiftPhaseView.ProceedToPlacement()` 단일 퍼널.
- 문구 4단: `ruleLabel`(2~4자) / `summary`(두 줄, 조건→결과) / `displayName`(정서 카피, 부제) / `description`(상세, 현재 소비처 없음).
- `summary` 는 **수치 포함 드라이 서술** + TMP 의미 색(시안=조건·트리거, 초록=이득, 산호=손해, 흐린 회청=부가).
- 기믹 4종 전부 아이콘 보유. 번아웃·과열은 `StackIcons` 를 **복제 없이 참조**(오버헤드 스택과 같은 그림), 레드불·사직서는 Pillow 로 신규 제작.
- 등장 효과음(ElevenLabs 1.07s 스팅) + `SoundManager.PlayGimmickReveal(clip)`.
- 탭 스킵 + "탭하여 계속" 힌트. 자동 진행 시간은 탭하지 않은 사람에게만 적용되는 하한이다.
- `GimmickGuideView` 삭제 — 배치 화면에 기믹 UI 흔적 0.

## Key Files

- 데이터: `Data/Gimmick/GimmickData.cs`, `Data/GimmickRevealConfig.cs`, `Data/Config/GimmickRevealConfig.asset`, `Data/Gimmick/Gimmick_*.asset`
- 뷰: `UI/GimmickPhaseView.cs`
- 라우팅: `UI/Dreamcatcher/GiftPhaseView.cs`(`ProceedToPlacement`), `Core/GameManager.cs`(`GamePhase`)
- 사운드: `Audio/SoundManager.cs`(`PlayGimmickReveal`), `Audio/GimmickReveal.mp3`
- 아트: `Art/GimmickIcons/`, `Art/StackIcons/`(번아웃·과열 공유)
- 씬: `Scenes/BattleScene.unity`(`GimmickPhaseView` GO)

## Verified

- 컴파일 0, 콘솔 에러 0.
- 스킵 경로(`AssignedGimmick=null`) 콜백 **동기 즉시**, 페이즈 전이 없음.
- 정상 경로 콜백 **1회**, 타이밍이 설정 합과 일치.
- 문구 3층·아이콘 폴백·VFX 스폰·효과음 발화(공용 폴백 경로 포함) 전부 확인.
- 두 줄 렌더(rect 136 > 필요 95, 잘림 없음), 탭 힌트 알파 0.55.
- unit 3 후 씬 missing-script 슬롯 0, Play 스모크 정상.

## Notes (되돌리면 안 되는 것)

- **`GamePhase` 값은 맨 뒤에만 추가.** `CameraDirectionConfig.asset` 이 raw int 직렬화(`phase: 1/3/4/5`). 중간 삽입은 카메라 포즈·브리딩을 통째로 민다.
- **`onDone` 은 어떤 경로로도 정확히 한 번.** 유실되면 배치가 영영 시작 안 된다. 완주 경로는 자기 콜백 안에서 시퀀스를 멈추지 않는다(`BossWarningView` 교훈).
- **첫 판 판정은 리빌 뷰가 스스로** 한다. 훅 퍼널이 튜토리얼 스킵 경로까지 삼켜서 `GiftPhaseView` 판정만으론 뚫고 나온다.
- **`DragBegan`/`Armed` 이벤트 선언·invoke 유지.** first-session-tutorial 과 공용.
- **연출 자산은 nullable 슬롯.** 아트 없이도 성립해야 한다.
- 번아웃·과열 아이콘은 `StackIcons` **참조**다. 사본을 만들면 나중에 한쪽만 바뀐다.

## 검증 함정 (다음 세션용)

- **오버레이 캔버스는 카메라 샷에 안 잡힌다.** 일시정지 상태로 찍어야 그림에 들어온다(MENU 버튼까지 사라지면 그 증상).
- **일시정지로 찍어도 색은 못 믿는다** — 부분 합성돼 실제보다 옅다. 레이아웃·문구 판단에만 쓸 것.
- **툴 왕복이 연출보다 길다.** beat 값을 일시적으로 키웠다가 **반드시 원복 + `git diff` 확인**(Play 중 SO 변경은 에셋에 남는다).
- **Play 중 씬 편집은 무효다.** `Undo.DestroyObjectImmediate` 가 "성공"해도 런타임 씬에만 적용된다. 편집 전 `Application.isPlaying` 확인.
- **씬 저장은 타 스펙 default-fill 을 함께 굽는다.** 내 hunk 만 분리 스테이징(`git hash-object` + `update-index`).

## Follow-up

**남은 확인 없음.** 사용자 Play 확인 2026-08-01 로 4건 전부 통과 —
① 선물 → 리빌 → 배치 전체 흐름(첫 판 선물 홀드와의 간섭 없음)
② 등장음 톤(Take1 확정) ③ 첫 세션 튜토리얼 진행 ④ 배치 페이즈에 카드 미노출.

후속 후보 9건은 `docs/spec/README.md` 의 **Follow-up Backlog → 기믹 인지** 로 이관했다. 그중 둘(전투 중 상시 UI · 리빌 VFX 딤 아래)은 사용자 판정으로 닫힌 항목이라, 되살리려면 그 판정부터 뒤집어야 한다.

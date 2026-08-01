# 22 — 인계 요약 (units 19~21 + partial 분할)

`18_handoff_summary.md`(units 14~17) 이후분. 최신 계약은 README + 번호 문서 우선.

## Commit

| 해시 | 내용 |
|---|---|
| `ec1f62ed` | docs — 신규 스텝 3종 스펙 + README 2개 |
| `45d35fea` | unit 19 — 전투 HUD 안내 seam + 스트레스 |
| `34cf2a8d` | unit 20 — 다음 웨이브 안내 |
| `04127844` | code-reviewer 반영(죽은 억제 제거 · 정확성 2건 · 테스트 7건) |
| `2e72a742` | critic 반영(배지 기하 재계산 → 오프셋 310) |
| `65a4fb74` | unit 21 — 스트레스를 **전투 정지 + 탭**으로 (19 rev) |
| `3ebe1568` | 컨트롤러 partial 4분할 (동작 변경 없음) |
| `53b40c11` · `8b25fdbb` | 머지 — spine-weapon-trail · gimmick-recognition-upgrade |

## Implemented

- **스트레스 안내(unit 21)**: 첫 판 전투 시작 후 지연을 두고 **전투를 실제로 정지**시키고 배지에 링 + 한 문단. 탭하면 재개, 방치하면 폴백 시간 뒤 자동 재개.
- **다음 웨이브 안내(unit 20)**: 스트레스 종료 직후 좌하단 버튼에 링 + 2줄. **비차단** — 안내 중에도 버튼이 실제로 눌리고 배치·탭이 동작한다.
- 두 안내 모두 **첫 판에만** 뜬다. 신규 프로필 필드 없이 `_awakeningLockedThisMatch` 하나로 게이트한다.
- 컨트롤러를 관심사별 **partial 4개**로 분할했다(`BattleBridge.*.cs` 관례). 여러 세션이 동시 편집하는 파일이라 각 관심사가 자기 상태·SerializeField·코루틴을 소유한다.

## Key Files

- `UI/Tutorial/FirstSessionTutorialController.cs` — 코어 배치 플로우 · lifecycle · 페이즈 라우팅
- `UI/Tutorial/FirstSessionTutorialController.BattleHud.cs` — units 19~21(정지 lease 포함)
- `UI/Tutorial/FirstSessionTutorialController.Awakening.cs` — 각성 0·A·B단계 + 첫 판 봉인
- `UI/Tutorial/FirstSessionTutorialController.Gift.cs` — 선물 워크스루
- `UI/Tutorial/TutorialGuidanceView.cs` — `MessageAnchor{Default,WorldMarker,HudHint}`
- `Scenes/BattleScene.unity` — `scoreHud → ScoreHud`, `waveDock → NextWaveDock`

## Verified

- 컴파일 0 (Runtime · Tests.EditMode · Tests.PlayMode).
- EditMode **1777 중 실패 0** — 2026-08-01 **머지 기준** 재실행(인계 시점엔 미실행이었다).
- 씬 배선 실측: `scoreHud → ScoreHud`, `waveDock → NextWaveDock`.
- **사용자 Play 확인 2026-08-01 통과** — 전투 정지·탭 재개·폴백 자동 해제, **정지 중 씬 이탈 후 다음 판 정상 속도**(lease 누수 없음), 손패 슬로모 0.3x 겹침 후 복귀, 말풍선이 배지를 가리지 않음, 웨이브 안내 비차단, 두 번째 판 미노출, 로비 왕복 후 기믹 안내 정상.

## Notes (되돌리면 안 되는 것)

- **정지는 `TimeManager` Battle 도메인 lease** 다. 글로벌 `Time.timeScale` 이 아니다. 누수 = 그 판 영구 정지라 방어가 4겹이다: 만료 폴백 · 필드 하나가 소유 · `StopBattleHudHint` 단일 해제 · 획득 전 선해제. **한 겹도 빼지 말 것.**
- **`ContinueTapped` 소비자가 둘**(클래스 안내 · 스트레스). `OnContinueTapped` 첫 줄의 `TryConsumeStressHintTap()` 순서를 흐리면 한쪽이 남의 탭을 먹는다.
- **`OnDisable` 의 정리 경로 ②**: `EndCore` 에 기대면 안 된다. 체인 구간에선 core 가 이미 끝나 `!_coreActive` 로 조기 return 하므로 체인이 세운 상태(코루틴·말풍선·앵커·정지 lease)를 아무도 되돌리지 않는다. `StopBattleHudHint` 를 따로 부른다.
- **HUD 안내에 신규 프로필 필드가 없다.** `_awakeningLockedThisMatch` 하나로 게이트한다 — 새 플래그를 추가하려면 그 이유부터 확인할 것.
- **partial 분할은 동작 변경이 아니다.** 관심사 파일이 자기 상태를 소유한다는 규칙을 깨고 공유 파일로 필드를 올리지 말 것.

## 머지에서 생긴 것

`gimmick-recognition-upgrade` 가 배치 안내 카드를 은퇴시키면서 **같은 자리가 충돌**했다. 원격이 `SetWorldMarkerLayout` → `SetMessageAnchor` 로 개명했고, 저쪽은 `gimmickGuide?.SetTutorialSuppressed(true)` 를 지웠다. 양쪽을 합쳐 해소했다(`8b25fdbb`) — **새 API 를 쓰되 gimmickGuide 호출은 없다.** 필드 선언이 이미 사라져서 원격 줄을 그대로 두면 컴파일이 깨진다.

첫 판 기믹 안내 억제는 이제 `GimmickPhaseView` 가 `TutorialProgress.ShouldRunCore` 로 **스스로** 판정한다. 튜토리얼 쪽에서 억제할 대상은 없다.

## Follow-up

- 남은 확인 없음. 스텝 3종 + 챕터 C 전부 사용자 Play 확인 통과.
- 모바일 실기기 QA 는 계속 보류(사용자 결정, units 0~4 시절부터).

# 23 — 기믹 리빌 안내: 진행 상태 + 앵커 토대

## 목적

리빌 홀드 안내(unit 24)가 쓸 토대만 먼저 놓는다. **이 유닛만 착지하면 동작 변화가 0** 이다 —
아무도 아직 새 토큰·앵커를 읽지 않는다. 반대로 seam 을 먼저 넣으면 완료 저장이 없어
**매 판 홀드**가 되므로 순서를 뒤집지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs`
- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceView.cs` — `MessageAnchor` 열거 + 오프셋 분기
- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceStyle.cs`
- `Assets/_Project/Data/Config/TutorialGuidanceStyle_Default.asset` — 값 조정이 필요할 때만
  (신규 필드는 코드 기본값으로 채워지므로 재직렬화 전까지 asset diff 가 안 생긴다)
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`

## 구현

**프로필 토큰**: `public int gimmickRevealHintVersion;` 를 형제 필드 옆에 추가한다.

**`TutorialProgress`**: `GimmickRevealHintVersion = 1` 상수와 세 함수.

```
IsGimmickRevealHintPending(profile) => profile.gimmickRevealHintVersion < GimmickRevealHintVersion
ShouldRunGimmickRevealHint(holder) => holder != null && holder.IsLoadedThisSession
                                      && IsGimmickRevealHintPending(holder.profile)
CompleteGimmickRevealHint(profile)  // 형제와 동일한 "이미 최신이면 false" 형태
```

**선물·core 완료를 체인하지 않는다.** `ShouldRunGiftTutorial`/`ShouldRunLobbyLoadoutHint` 는
`!IsCorePending` 을 물지만, 그 형태는 백로그가 이미 결함으로 지적했다 — 선행 안내가 fail-open
경로(`giftConfig` 미배선 · TestMode fast-forward · 참조 누락)를 타면 뒤 안내가 **영영 발화하지
못한다**. 첫 판 배제는 리빌 자신의 `ShouldRunCore` 게이트(`GimmickPhaseView.cs`)가 이미 하므로
여기서 다시 걸 필요가 없다.

**`ResetAll` / `ResetAllInJson` 양쪽의 `changed` 표현식에 반드시 넣는다.** 빠뜨리면 이 토큰만
다를 때 `ProfileStore.ResetTutorialProgressAt` 이 파일 교체를 건너뛰어 리셋이 디스크에 닿지 않는다
(unit 17 · outgame unit 6 과 같은 함정).

**말풍선 앵커**: `MessageAnchor` 에 `GimmickReveal` 을 추가하고 오프셋 분기에
`Style.revealHintMessageTopOffset` 을 잇는다. 리빌 콘텐츠는 y `+390`(아이콘 상단) ~ `-290`(탭힌트)
을 점유하므로 기본 앵커(`messageTopOffset 184` → y `356`~`240`)는 **아이콘과 겹친다**. 값은
탭힌트 아래 하단 대역(1920×1080 기준 topOffset ≈ `880`, y ≈ `-340`)에서 출발하고 unit 24 Play 로
확정한다 — "읽고 탭"이 한 덩어리로 보이는 자리다.

**Style 신규 필드 1개** (제약 6 — 코드 const 금지): `revealHintMessageTopOffset` — 위 앵커 오프셋.

홀드 만료 폴백 값은 **여기 두지 않는다.** `GimmickPhaseView` 는 `Wassup.UI` 이고
`TutorialGuidanceStyle` 은 `Wassup.UI.Tutorial` 이라, 뷰가 튜토리얼 스타일 SO 를 알면 의존이
역방향으로 붙는다. 폴백은 뷰가 이미 소유한 `GimmickRevealConfig`(`summaryHoldSec`·
`tapSkipGraceSec` 의 이웃)에 unit 24 가 추가한다.

## 완료 기준

- [ ] compile clean (Runtime · Tests.EditMode · Tests.PlayMode).
- [ ] `TutorialProgressTests` 신규: pending 기본값 true · `Complete…` 1회만 true · `ResetAll` 이
      이 토큰만 다를 때도 `changed == true` · `ResetAllInJson` 이 같은 조건에서 `changed == true`
      이고 다른 토큰을 보존한다.
- [ ] EditMode 전체 실패 0 (직전 기준선 대비 증가 없음).
- [ ] **런타임 동작 변화 없음** — 리빌·선물·각성 안내가 이 커밋 전후로 동일하다.

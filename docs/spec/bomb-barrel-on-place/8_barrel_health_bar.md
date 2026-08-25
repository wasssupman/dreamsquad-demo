# 8 — 배럴 머리 위 체력 바

## 목적

「언제 터지나」의 답을 **남은 체력**이 말하게 한다. unit 7 로 시계가 사라진 자리에
들어가는 판독 장치이고, 퓨즈 틴트(색 하나로 뭉뚱그림)보다 정확하다 — 플레이어가 실제로
보고 싶은 것은 「적들이 얼마나 팼나」다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardSO.cs` — `overheadHeight`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncBlockingHazardOverheadGauges`
- `Assets/_Project/Data/Hazards/Blocker_BombBarrel.asset` — `overheadHeight: 1`

## 구현

- **신규 뷰 0.** 유닛·거점이 쓰는 `UnitOverheadUiLayer.SetUnit` 을 그대로 부른다.
  바 하나를 위해 레이어를 새로 만들면 스킨·풀·정렬 규약이 두 벌이 된다.
- ⚠ **반드시 오버헤드 창(`BeginFrame`/`EndFrame`) 안에서 부른다.** 밖에서 부르면
  `EndFrame` 의 `_seen` 소거에 걸려 **매 프레임 나타났다 사라진다**. 그래서 호출 자리는
  `SyncGoalOverheadGauges` 바로 옆이다(골 게이지가 같은 이유로 거기 있다).
- **스킨은 방어유닛.** 배럴은 플레이어가 놓은 물건이다. 카드 행은 저절로 빈다 —
  설치물은 드림캐쳐 호스트가 아니라 `_cardsByHost` 에 없다.
- **높이는 설치물 자신의 저작값**(`overheadHeight`). 브리지 공용 상수로 두면 1칸 배럴과
  3x3 방벽이 같은 높이를 쓴다. 거점이 `goalOverheadHeight` 를 따로 갖는 것과 같은 이유.
- **기본 0 = 바 없음.** 기존 길막 설치물(바위 2종)은 이 키가 없어 0 으로 떨어지므로
  무회귀다 — `explodeDamage` 와 같은 형태의 옵트인.
- 순회 대상은 `_blockingHazardVisualMap`(시각화된 설치물). 뷰가 없으면 바를 띄울 자리도
  없으므로 이 등록부가 곧 대상 목록이다.

## 형태 선택 (리뷰 2026-08-23 이후 사후 기록)

초판은 「오버헤드 바」를 **대조 없이** 골랐다. 두 선행 문서를 봤어야 했다:

- `docs/spec/unit-health-display/README.md` 의 후속 후보가 이 건을 이름 붙여 이미 갖고 있다 —
  「blocking hazard 체력 표시 [S] · 시각 니즈 생기면 **타일 게이지 재사용** 검토」.
- 같은 문서의 정보 비대칭 모델에서 방어유닛의 판독 형태는 **타일**이다. 초판은 스킨은
  방어유닛에서, 형태는 거점에서 가져와 두 어휘를 섞었다.

**결정: 오버헤드 바 유지**(사용자 결정 2026-08-23). 타일 게이지가 모델 정합·화면 혼잡
(배럴은 적 무리 한복판 = 데미지 숫자가 가장 붐비는 좌표)에서 낫다는 리뷰 권고를 보고도
오버헤드를 택했다 — 재론하려면 그 두 근거부터 반박해라.

⚠ **「게이지 형태 금지」는 이 프로젝트의 규율이 아니다.** `6_fuse_tint.md`(은퇴)가 그렇게
적었지만 실제 금지 대상은 `three-minute-kill-race` unit 2 가 명시 한정한 **「내 마음」
(`Faction.DefenderCore`) 하나**다. 본능·적 마음은 지금도 상시 바를 달고 있고(`BattleBridge`
구조물 오버헤드 루프), 그 상시 바 자체가 사용자 결정이다(2026-08-04 「체력바는 유닛처럼 띄워」).

## 알아 둘 것

- 만피에서는 바가 **흐려진다**(`_skin.fullHealthAlpha`). ⚠ 단 **배럴에서는 도달 불가**다 —
  노후화(unit 9)가 `ratio >= 0.999` 를 0.01초 안에 벗어난다. 알파 차이가 0.94↔1.0 이라
  시각 손해는 없지만, 이 문장을 「배럴이 갓 놓이면 흐리다」로 읽지 말 것.
- **레거시 폴백 구멍**: `unitHealthPresentationMode == Legacy` 면 방어유닛·거점은 타일 게이지로
  폴백하는데 **설치물은 판독 장치가 통째로 없다**(`SyncBlockingHazardOverheadGauges` 가 즉시
  return). 현재 씬은 `UnifiedOverhead`(1)라 휴면 상태다 — Legacy 를 되살리면 같이 처리해라.
- 피해 트레일(빨간 잔상)이 공짜로 따라온다. 배럴처럼 여러 적이 동시에 때리는 대상에서
  「방금 얼마나 깎였나」가 잘 읽힌다.

## 완료 기준

- [x] compile 0 에러.
- [x] (Play) 배럴을 세우면 오버헤드 창에 바가 등록된다 — `hasBar=True` · `visible=True` ·
      `ratio=1`.
- [x] (Play) 피해가 바에 실린다 — 60 피해 후 `hp=140/200` · `barRatio=0.7` ·
      `alpha=1.00`(만피 감쇠 해제).
- [x] (Play) 배럴이 죽으면 바도 같이 사라진다 — 등록부에 **고아 바 0**.
- [x] (Play) 같은 프레임 대조: `Hazard_Rock_1x1`(overheadHeight 0) **바 없음** ·
      `Blocker_BombBarrel`(1) **바 있음** → 기존 설치물 무회귀.
- [x] 전체 EditMode 2574건 중 실패 1건 = 사전 실패(무관).

확인 2026-08-23 · Play 실측(BattleScene · 콘솔 에러/경고 0).

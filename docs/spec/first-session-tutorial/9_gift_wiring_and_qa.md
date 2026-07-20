# 9 — 선물 튜토리얼 씬 배선 + QA

## 목적

신규 참조 2개를 BattleScene 에 배선하고, 첫 판 억제 → 두 번째 판 튜토리얼 → 세 번째 판 일반
연출의 판 전이를 자동 + Play 로 검증한다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs`

## 구현

씬 배선(둘 다 기존 소유자 직렬화 — 씬 이름/전역 Find 금지, unit 4 규약):

- `FirstSessionTutorial` GO → `giftView`(GiftPhaseView)
- `GiftPhaseView` GO → `profileSO`(PlayerProfileSO) — **unit 7 에서 배선 완료**(Play 검증 필요로 당김)

씬에 다른 WIP 가 있을 수 있으므로 저장 전 `git diff`로 튜토리얼 배선 delta 만 격리한다.

PlayMode smoke 추가(기존 `FirstSessionTutorialSmokeTest` 패턴 — ProfileSaver seam 으로 디스크
저장 없이 검증):

- core pending 프로필: Gift 진입 → 연출/패널 없이 Placement 도달, 덱 TotalCount 12.
- core 완료 + gift pending: 리빌 홀드 진입(자동 진행 없음) → 탭 → 셔플 홀드 → 탭 → 완료 저장
  1회 + 이후 배치 도달.
- gift 완료 프로필: 홀드 이벤트 0회, 기존 타임라인/탭 스킵 동작.
- profile 미로드(직접 Play): 억제·홀드·저장 모두 없음.

Editor Play QA: RESET TUTORIAL → 첫 판(선물 미노출·핵심 안내) → 전투 진입 → 로비 복귀 → 두 번째
판에서 홀드 2회·문구·말풍선 z-order(선물 패널 위, 메뉴 팝업과 충돌 여부) 확인. 루시드/림 양쪽
kind 는 `GiftConfig` weight 를 임시 조정해 각 1회 확인 후 원복.

## 완료 기준

- [ ] compile clean, 콘솔 오류 0.
- [ ] PlayMode smoke 4 케이스 green.
- [ ] Editor Play: 판 전이(억제 → 튜토리얼 → 일반)가 프로필 저장만으로 이어진다.
- [ ] 말풍선이 1920×1080 에서 선물 카드·타이틀을 가리지 않는다.
- [ ] Android 실기기 터치 확인은 기존 후속 QA 항목에 합류(README 후속 후보).

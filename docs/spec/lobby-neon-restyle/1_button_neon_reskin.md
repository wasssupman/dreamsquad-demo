# 1 — 메뉴 버튼 네온 리스킨

> **개정됨**: START CTA 부분은 `4_start_cta_banner.md` 가 대체한다. 아래 문서에서 START 를
> `LobbyNeonChip(Cta)` 로 굽는다는 서술과 `UiRoundedSprite.MakeHorizontalGradient` 는
> unit 4 에서 각각 `LobbyNeonCta` 신설·오버로드 삭제로 폐기됐다. 칩 3종 서술은 유효하다.

## 목적

로비 1차 화면 버튼(`SquadButton`/`DreamcatcherButton`/`HistoryButton`/`StartButton`)을
시안의 다크 칩 + 네온 테두리 + 흰 글리프 + 한글 라벨 스타일로 교체한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/UiRoundedSprite.cs` — `MakeHorizontalGradient(...)` 오버로드 추가
  (수평 그라디언트 채움 + 외곽 링, full-rect. START CTA 전용이지만 공용 유틸에 두는 게 결이 맞음)
- `Assets/_Project/Scripts/UI/Outgame/LobbyNeonChip.cs` (신규) — Awake 에서 칩/CTA 스프라이트를
  베이크해 대상 Image 에 할당하는 스킨 컴포넌트
- `Assets/_Project/Scenes/OutgameScene.unity` — 버튼 4개 하위 구조 재구성 + 컴포넌트 와이어링

## 구현

- `LobbyNeonChip`: `enum Kind { Chip, Cta }` + SerializeField 색상(기본값=시안 실측 팔레트)
  + 대상 `Image`(배경, 미할당 시 같은 GO 의 Image) 만 참조. Awake 에서
  `UiRoundedSprite.Make`(Chip) / `MakeHorizontalGradient`(Cta) 로 굽고 할당.
  글리프·라벨은 컴포넌트가 참조하지 않는 순수 씬 오브젝트 — 지오메트리는 씬이,
  스프라이트/색만 이 컴포넌트가 소유한다.
  아키텍처 중립 계산 없음(전부 렌더 파라미터) — 순수 함수 분리 대상 아님.
- 씬: 각 버튼 GO 이름·계층 위치·Button 컴포넌트·onClick 불변. 하위에
  글리프 Image + Jua 라벨(TMP) 배치, 기존 스티커 아이콘 Image 는 **sprite 참조만 교체하지
  말고 GO 비활성으로 보존**(revert 없이도 인스펙터에서 즉시 복원 가능).
- 라벨: 스쿼드 / 드림캐쳐 / 히스토리 (Jua SDF), START 는 Anton SDF(없으면 Jua).
- dev 클러스터·로그인 패널 불가침. 튜토리얼이 참조하는 rect 크기 유지(버튼 사이즈 불변).

## 완료 기준

- 컴파일 에러 0, Play 진입 시 콘솔 에러 0.
- 로그인 후 로비: 3개 칩 버튼(다크+네온 테두리+글리프+한글 라벨) + START 그라디언트 CTA 확인
  (에디터 Game 뷰 스크린샷).
- 버튼 클릭 → 기존 패널(스쿼드/덱/히스토리)이 열리고 START 가 기존 플로우 진입.
- 이 커밋 revert 시 버튼이 스티커 아이콘 모습으로 복원.

> 2026-07-31 구현 완료 — 커밋 `5ec7251a`. 컴파일 에러 0, Play 콘솔 에러 0,
> 스크린샷 `Assets/Screenshots/screenshot-20260731-123955.png` (칩 3종 + START CTA 확인).
> 실측 팔레트 확정: 칩 rgb(16,15,40)/테두리 rgb(121,98,160)→기본값 (150,110,220), CTA rgb(251,79,175)→rgb(125,97,233), 림 라이트블루.
> START 폰트 = Anton SDF 볼드이탤릭(TMP fontStyle=3; 10 은 LowerCase 비트라 오답이었음).
> 미실시: 버튼 클릭→패널 오픈 스모크(onClick 무변경이라 위험 낮음) — 사용자 확인 대기.

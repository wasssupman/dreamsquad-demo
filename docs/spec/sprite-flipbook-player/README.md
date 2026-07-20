# Sprite Flipbook Player

상태: 진행 중 · 2026-07-20

## 상위 목표

월드 공간 `SpriteRenderer` 를 대상으로 하는 **재사용 가능한 스프라이트 플립북 재생기**를 만든다.
프레임 소스는 두 모드를 모두 받는다 — **컷 모드**(프레임당 개별 `Sprite`)와 **통 모드**(그리드 시트 1장).

현재 프로젝트의 프레임 애니메이션은 두 군데뿐이고 둘 다 재사용이 불가능하다.

- 로비 캐릭터 — Unity `Animator` + `.anim` 클립. UI `Image` 전용, 상태 이름 매직 스트링에 결합.
- 배치 컷씬 — `Presentation`/`UI/DeployCutscenePlayer.cs`. 이름과 달리 범용 재생기가 아니라 배치 컷씬 **연출 디렉터**다.
  플립북 진행 위에 자체 루트 Canvas 생성 · 좌하단 슬라이드 인/아웃 · 뎁스 패럴랙스 틸트 · 드래그 세션 수명 계약이 한 덩어리로 붙어 있다.

이 spec 은 **새 독립 기능**을 추가한다. 위 두 기존 구현은 건드리지 않는다 (2026-07-20 사용자 결정).

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 순수 로직 | `0_flipbook_math.md` | 경과시간 → 프레임 인덱스 순수 함수 + EditMode 테스트 |
| 1 | 데이터 | `1_flipbook_data.md` | `SpriteFlipbookData` SO — 컷/통 2모드를 단일 프레임 배열로 확정 |
| 2 | 재생기 | `2_flipbook_player.md` | `SpriteFlipbookPlayer` MonoBehaviour — `SpriteRenderer` 구동 |
| 3 | 오소링 | `3_sheet_authoring.md` | 통 시트 슬라이스 오소링 경로 + 검증 |
| 4 | 인계 | `4_handoff_summary.md` | 구현 종료 요약 |

## Feature-wide 계약

- **타겟은 `SpriteRenderer` 단일.** UI `Image` 는 지원하지 않는다. 구현체가 하나뿐이므로 렌더 타겟 추상화(인터페이스)를 만들지 않는다 (제약 8). UI 수요가 실제로 생기면 그때 두 번째 구현체와 함께 추출한다.
- **프레임 선택은 순수 함수.** `(경과시간, fps, 프레임수, 루프여부) → 인덱스`. 아키텍처 타입을 모른다 (제약 10). EditMode 단위 테스트 대상.
- **재생기는 소스 모드를 모른다.** 컷/통 분기는 데이터 계층(unit 1)이 흡수하고, 재생기는 확정된 프레임 배열 하나만 본다. 모드 분기가 재생기로 새면 계약 위반.
- **클럭을 하드코딩하지 않는다.** `Time.deltaTime` 금지. `TimeManager.Instance.DeltaTime(domain)` 을 쓰고 `TimeDomain` 은 인스펙터 노출. 전투 이펙트는 `Battle`(슬로우모 동반), UI/연출은 `Interaction`. 기본값은 `Battle`.
- **모든 수치는 SO 또는 인스펙터에서 나온다** (제약 6). fps · 루프 · 재생 도메인 전부 데이터.
- **런타임에 스프라이트를 생성하지 않는다.** 프레임은 전부 임포트된 에셋이다 (아래 확정된 결정 참조).
- **정렬은 재생기가 건드리지 않는다.** `SpriteRenderer` 의 sortingLayer/order 는 소비자 GameObject 가 authored 로 소유. 재생기는 `sprite` 만 쓴다.
- **재생기는 자기 GameObject 를 생성·파괴하지 않는다.** 풀링·스폰은 소비자 책임. `DeployCutscenePlayer` 처럼 캔버스를 스스로 만드는 구조를 반복하지 않는다.

## 확정된 결정

**통 모드의 프레임 확정 지점 = 임포트 시 슬라이스** (2026-07-20 사용자 결정, 안 B).

Unity `Sprite Mode = Multiple` 로 자른 서브스프라이트를 에디터 유틸이 프레임 배열에 주입한다.
런타임은 컷/통 구분 없이 **항상 `Sprite[]` 하나만** 소비한다.

- 런타임에 `Sprite.Create` 가 없다 → 생성 스프라이트 수명 관리·leak 위험이 애초에 발생하지 않는다.
- 프레임이 정식 에셋이라 SpriteAtlas 팩킹·빌드 스트리핑에 그대로 얹힌다.
- 통 모드의 실익(에셋 개수 감소, 프레임 N개 수동 드래그 제거)은 임포트 시점에 전부 얻는다.

따라서 계약 "재생기는 소스 모드를 모른다" 가 **데이터 계층에서도** 성립한다 — 통/컷은 오소링 경로(unit 3)의 차이일 뿐,
`SpriteFlipbookData` 의 런타임 표면은 단일하다. 위 계약 항목 중 "런타임 생성 스프라이트 수명" 조항은 이 결정으로 해소되어 삭제됨.

## 파이프라인 커버리지

이 spec 은 구체적인 플레이 오브젝트를 신설하지 않는다 — 미래 오브젝트가 쓸 **View 정거장 하나**를 공급한다.
가장 가까운 아키타입은 `docs/reference/object-pipeline-map.md` 의 **VFX (one-shot)**.

| 정거장 | 이 spec | 비고 |
|---|---|---|
| 데이터 소스 | `Data/SpriteFlipbookData.cs` (SO) | VFX 아키타입은 SO 없이 Spawner SerializeField — 여기는 SO 로 둔다(여러 소비자 공유) |
| 트리거 | N/A | 재생 시작은 소비자가 호출. 이 spec 은 큐/폴링 채널을 만들지 않는다 |
| ECS | N/A | 순수 프레젠테이션. 시뮬을 읽지도 쓰지도 않는다 |
| View | `Presentation/SpriteFlipbookPlayer.cs` | 풀링 없음 — 소비자 소유 |
| 정렬 | N/A | 소비자 GameObject 의 `SpriteRenderer` 가 authored 소유 (계약 참조) |
| 씬 wiring | N/A | 첫 실사용 spec 에서 발생 |

파이프라인 맵 갱신은 **첫 실사용 소비자가 생길 때** 판단한다. 재생기 단독으로는 새 아키타입이 아니다.

## 후속 후보 (현 spec 범위 밖)

- **`DeployCutscenePlayer` 를 이 재생기 위로 재작성** · 연출(캔버스/슬라이드/틸트)은 유지하고 프레임 진행만 위임. 기존 동작 회귀 검증이 커서 분리.
- **UI `Image` 타겟 지원** · 두 번째 구현체가 실제로 필요해지면 그때 렌더 타겟 추출.
- **로비 캐릭터의 `Animator` 대체** · 리액션 길이의 이중 진실(C# 타이머 ↔ Animator exit time)과 상태 매직 스트링이 사라지지만, `.anim`/컨트롤러 에셋 정리 + 키링 연동 회귀 검증이 딸려온다.
- **이벤트 콜백(재생 완료/특정 프레임 도달)** · 첫 소비자가 실제로 요구할 때 추가. 미리 만들지 않는다.

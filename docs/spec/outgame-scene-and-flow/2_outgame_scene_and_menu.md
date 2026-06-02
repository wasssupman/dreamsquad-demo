# 2 — OutgameScene + 메인 메뉴

## 목적

부팅 씬 OutgameScene 을 만들고 3버튼 메뉴 + placeholder 패널을 구성한다.

## 변경 대상

- 신규 `Assets/_Project/Scenes/OutgameScene.unity`
- 신규 `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
- 신규 에셋: `PlayerProfileSO.asset`, (Unit 0 의 `DefenderCatalog.asset` 참조)

## 구현

씬 구성 (UnityMCP `manage_scene`/`manage_gameobject`/`manage_ui`):
- Main Camera + Directional Light + EventSystem.
- Canvas(Screen Space - Overlay) + 세로 버튼 그룹:
  - **[게임 시작]** · **[스쿼드]** · **[드림캐쳐 덱]**
- `SquadPanel`, `DreamcatcherPanel` — 각각 비활성 placeholder (제목 + "준비 중" 텍스트 + 닫기 버튼).

`OutgameMenuController` (MonoBehaviour, 씬 로컬, 싱글톤 아님):
```csharp
[SerializeField] DefenderCatalog catalog;
[SerializeField] PlayerProfileSO profileSO;
[SerializeField] GameObject squadPanel;
[SerializeField] GameObject dreamcatcherPanel;

void Awake() {
    profileSO.profile = ProfileStore.LoadOrCreate(catalog);
}
public void OnStartGame();      // Unit 3 에서 씬 로드 채움 (지금은 stub 로그)
public void OnOpenSquad()  => squadPanel.SetActive(true);
public void OnOpenDreamcatcher() => dreamcatcherPanel.SetActive(true);
public void OnClosePanels();    // 둘 다 비활성
```
버튼 `onClick` 을 UnityMCP 로 각 메서드에 wiring. `OnStartGame` 의 실제 씬 전환은 Unit 3.

## 완료 기준

- OutgameScene Play 진입 시 에러 없음, 3버튼 노출.
- 스쿼드/드림캐쳐 버튼 → 각 placeholder 패널 열림, 닫기 동작.
- Play 시 `ProfileStore.LoadOrCreate` 호출되어 `persistentDataPath/profile.json` 생성됨(콘솔/파일 확인).
- `PlayerProfileSO.profile` 이 null 아님.
- read_console clean.

> 완료 확인 2026-06-02 — Play 검증: profile.json 생성(15 units, schemaVersion 1), 패널 상호배타 토글/닫기 정상, 에러 0.
> 주의: 프로젝트에 한글 TMP 폰트 부재(LiberationSans만) → 라벨은 **영문**으로 통일. 한글화는 후속(로컬라이즈) 후보.

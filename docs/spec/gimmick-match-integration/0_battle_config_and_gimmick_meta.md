# 0 — BattleConfig SO + GimmickData 표시 필드

## 목적

기믹 기능 게이팅/목록을 담는 `BattleConfig` SO 를 신설하고, 안내 UI 가 읽을 표시 텍스트를 `GimmickData` 에 추가하고, **기본 에셋을 생성**한다. **순수 additive** — 아직 아무도 참조 안 함(unit 1 에서 배선). 에셋을 여기서 만들어 두어야 unit 1 이 dormant 창 없이 배선·검증 가능(README 계약 8).

## 변경 대상

- (신규) `Assets/_Project/Scripts/Data/BattleConfig.cs` — `Wassup.Data`.
- `Assets/_Project/Scripts/Data/Gimmick/GimmickData.cs` — `description` 필드 추가.
- (신규 에셋) `Assets/_Project/Data/Config/BattleConfig.asset` (+ `.meta`, `Data/Config/` 폴더 `.meta`).

## 구현

### BattleConfig (신규)

```csharp
namespace Wassup.Data
{
    // gimmick-match-integration unit 0 — 매치 전역 기믹 기능 config.
    // 추후 시트에서 데이터로 관리될 예정 (순수 데이터 컨테이너로 유지).
    [CreateAssetMenu(menuName = "Wassup/Config/BattleConfig", fileName = "BattleConfig")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Tooltip("기믹 기능 전체 on/off. false = 기존 클린 플레이(기믹 없음).")]
        public bool gimmickEnabled = true;
        [Tooltip("전체 기믹 목록. 매치 시작 시 여기서 시드 기반 랜덤 1개 배정.")]
        public GimmickData[] gimmickPool = System.Array.Empty<GimmickData>();
    }
}
```

### GimmickData 표시 필드

`GimmickData`(base)에 안내 UI 용 설명 추가. `displayName` 은 이미 존재.

```csharp
public string displayName = "";
[TextArea(2, 4)]
[Tooltip("배치 페이즈 안내 카드 본문 (플레이어용 룰 설명).")]
public string description = "";
```

- `description` 는 base 에 둔다(모든 기믹 공통 표시 계약). 수치/룰 값은 concrete SO 가 계속 소유.
- 아이콘은 후속 후보 — 이번엔 텍스트만.

### BattleConfig 기본 에셋

- 컴파일 후 `manage_scriptable_object` 로 `Assets/_Project/Data/Config/BattleConfig.asset` 생성.
- `gimmickEnabled = true`, `gimmickPool = [Gimmick_Overwork]`(기존 `Assets/_Project/Data/Gimmick/Gimmick_Overwork.asset` guid 참조).
- `Gimmick_Overwork` 의 `description` 에 플레이어용 룰 설명 1~2줄 기입(예: 피로도→번아웃 / 레드불→라스트런). 하드코딩 아님(SO 데이터).

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] `Wassup/Config/BattleConfig` CreateAssetMenu 노출 확인.
- [ ] `BattleConfig.asset` 생성·로드, `gimmickPool[0]==Gimmick_Overwork`, `Gimmick_Overwork.description` 채워짐. `.meta` 짝 + 폴더 `.meta` 존재.
- [ ] 기존 흐름 무회귀(순수 타입 추가, 아직 아무도 참조 안 함).

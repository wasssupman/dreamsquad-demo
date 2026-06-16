# 0 — WavePlanAsset 저작 데이터 모델

## 목적

에디터에서 직접 작성하는 웨이브 플랜의 데이터 컨테이너를 만든다. 웨이브당 **N개**의 (적 타입, 수량) 그룹과 트리거 시각을 인스펙터에서 드래그·입력할 수 있게 한다. 이 unit 은 순수 신규 SO 로, 런타임 소비(unit 1·2)와 독립이며 의존이 없다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/WavePlanAsset.cs`
- 신규(샘플): `Assets/_Project/Scripts/Data/WavePlans/WavePlan_Sample.asset`

## 구현

```csharp
namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "WavePlan", menuName = "Wassup/WavePlan", order = 12)]
    public class WavePlanAsset : ScriptableObject
    {
        public string displayName = "Test Plan";

        [Tooltip("0 = endless: 시간제한 없음. 전 웨이브가 dispatch되고 적이 전멸하면 승리. " +
                 ">0 이면 해당 초에 타임아웃 승리(라이브와 동일).")]
        public float timerDurationSec = 0f;

        [Tooltip("웨이브 내 개별 스폰 사이 간격(초). 기존 intraWaveSpacing 과 동일 의미.")]
        public float intraWaveSpacingSec = 0.35f;

        public List<AuthoredWave> waves = new();
    }

    [Serializable]
    public class AuthoredWave
    {
        [Tooltip("이 웨이브가 호출되는 시각(초). 오름차순 권장.")]
        public float triggerTimeSec;
        public List<AuthoredSpawnGroup> groups = new();
    }

    [Serializable]
    public class AuthoredSpawnGroup
    {
        public AttackUnitData unit;     // 적 SO 드래그
        [Min(1)] public int count = 1;
    }
}
```

- `timerDurationSec` 기본 `0` = endless. 기존 `BattleBridge._timerDuration <= 0` 시맨틱과 일치(unit 2 에서 연결).
- 적 타입은 기존 `AttackUnitData` SO 를 그대로 참조(source of truth 재사용).
- 그룹별 개별 오프셋 초는 비목표 — 웨이브 트리거 시각 + `intraWaveSpacingSec` 균등이면 충분.

### 샘플 에셋

`WavePlan_Sample.asset` — `timerDurationSec=0`(endless), 약 8개 웨이브. 최소 1개 웨이브는 **3타입 이상**(N>2 증명). 실재 적 SO 참조: `Enemy_Basic / Swift / Tanker / Needler / Runner` (`Assets/_Project/Scripts/Data/Units/`). 인스펙터에서 작성자가 자유롭게 늘리는 출발점.

## 완료 기준

- `WavePlanAsset.cs` 컴파일 성공(콘솔 에러 0).
- `WavePlan_Sample.asset` 생성됨: endless(`timerDurationSec=0`), N>2 웨이브 1개 이상 포함, 실 적 SO 참조.
- 인스펙터에서 웨이브/그룹 추가·적 SO 드래그·수량 입력이 동작.
- 런타임 연결은 본 unit 범위 밖(unit 1·2). 여기선 데이터 작성 가능까지.

---

*완료 확인*: 2026-06-16 — 에디터 인스펙터에서 8웨이브/그룹 표시·드래그·count 입력·`Create>Wassup/WavePlan` 생성 확인. 런타임 로드 검증(8웨이브, N>2 포함, 108 spawns, 적 SO 정상 해석). 커밋 `003f765`.

using UnityEngine;

namespace Wassup.Data
{
    // bonus-wave-pull unit 3 — 보너스 당기기의 **모든 수치**를 소유하는 단일 에셋(제약 6).
    //
    // 전 맵·전 트리거 공통 1벌이다(README 계약 2). 맵별 차등이 필요해지면 맵/덱 참조로
    // 승격하는 것이 후속 후보이고, 지금 그렇게 만들면 13개 덱에 같은 값이 복제된다.
    [CreateAssetMenu(fileName = "BonusWaveData", menuName = "Wassup/Bonus Wave", order = 14)]
    public class BonusWaveData : ScriptableObject
    {
        [Header("편성")]
        [Tooltip("보너스 웨이브에 나오는 적. 덱 풀에는 넣지 않는다(계약 4).")]
        public AttackUnitData enemyUnit;

        // ★**포탈 개수는 여기 없다 — 맵이 소유한다.** 런타임 분모는 언제나
        // `GeneratedMap.bonusSpawns.Length` 이고, 저작 계약의 「2」는
        // `BonusSpawnAuthoringRules.RequiredPortalCount` 하나다.
        // 여기에 `portalCount` 필드를 두면 읽히지 않는 값이 인스펙터에서 유효해 보이고
        // (툴팁이 "같아야 한다"고 말하는데 대조하는 코드가 없다), 밸런서가 3으로 바꿔도
        // 런타임은 그대로 2로 돈다. 저작 개수를 바꾸려면 그 상수와 맵 저작을 함께 옮긴다.

        [Tooltip("한 번의 보너스 당기기로 나오는 총 마리수. 포탈에 i % portalCount 로 배분된다.")]
        [Min(1)] public int enemyCount = 10;

        [Header("타임라인 (버튼을 누른 순간이 0)")]
        [Tooltip("포탈이 열리기까지.")]
        [Min(0f)] public float portalAppearDelaySec = 1f;

        [Tooltip("포탈이 열린 뒤 첫 적이 나오기까지.")]
        [Min(0f)] public float firstSpawnDelaySec = 2f;

        [Tooltip("적과 적 사이 간격.")]
        [Min(0f)] public float spawnIntervalSec = 0.35f;

        [Tooltip("마지막 적이 나온 뒤 포탈이 남아 있는 시간.")]
        [Min(0f)] public float portalLingerSec = 1.5f;

        [Header("트리거")]
        // ⚠ 이 값은 **일반 적 처치 수** 기준이다(계약 12). 보너스 적 처치를 세면 실효 임계가
        // (N − enemyCount) 로 내려가고 N ≤ enemyCount 에서는 발산한다 — 보너스 웨이브가 자기
        // 자신을 무한 재발화해 판 전체가 보너스 웨이브만 돈다. 아래 OnValidate 가 그 하한을 잡는다.
        [Tooltip("이만큼의 «일반 적» 을 처치할 때마다 보너스 당기기 크레딧이 1회분 쌓인다.")]
        [Min(1)] public int killThreshold = 30;

        // heart-stress-axis 연동 — 마음이 여유 있을 때만 보너스 판이 열린다.
        // 스트레스는 «차오르는» 값이다: 0 = 만피, 100 = 마음 파괴(StressMath).
        // 그래서 이 값은 **상한**이다 — 이 이하일 때만 등장한다.
        //
        // ⚠ **등장 조건이지 유지 조건이 아니다.** 한 번 뜨면 소비할 때까지 유지된다.
        // 매 프레임 재평가하면 스트레스가 문턱 근처에서 진동할 때(맞으면 오르고 잡으면
        // 내려간다) 버튼이 떨린다. 래치는 그 떨림을 구조적으로 불가능하게 만든다.
        [Tooltip("이 스트레스 이하일 때만 버튼이 등장한다. 0=만피, 100=마음 파괴. " +
                 "등장 조건이지 유지 조건이 아니다 — 뜬 뒤에 올라가도 사라지지 않는다.")]
        [Range(0f, 100f)] public float maxStressToOffer = 30f;

        // 첫 스폰의 절대 시각(버튼 누른 시점 기준). 포탈이 열린 뒤에 나와야 하므로 두 지연의 합이다.
        public float FirstSpawnAtSec => portalAppearDelaySec + firstSpawnDelaySec;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (enemyUnit == null)
                Debug.LogError("[BonusWaveData] enemyUnit 미할당 — 보너스 당기기가 아무것도 스폰하지 않는다.", this);

            // 계약 12 의 불변식. 등호도 막는다 — N == enemyCount 면 보너스 웨이브 하나가 정확히
            // 다음 하나를 채워 «일반 적 0마리로 영구 순환» 이 된다.
            if (killThreshold <= enemyCount)
                Debug.LogError(
                    $"[BonusWaveData] killThreshold({killThreshold}) 는 enemyCount({enemyCount}) 보다 커야 한다 — " +
                    "보너스 웨이브가 자기 자신을 재발화한다(README 계약 12).", this);

            // enemy-detection-range unit 1 — 구 `huntsDefenders`(bool)가 `detectionRange` 로
            // 흡수됐다. 보너스 적이 요구하는 것은 **무제한 사냥**(음수)이다 — 유한 반경을
            // 저작하면 반경 밖 방어유닛은 못 찾아 「거점으로 직행」이 부분적으로 되살아난다.
            if (enemyUnit != null && !enemyUnit.HasUnlimitedDetection)
                Debug.LogWarning(
                    $"[BonusWaveData] enemyUnit.detectionRange({enemyUnit.detectionRange}) 가 음수(무제한)가 " +
                    "아니다 — 보너스 적이 방어유닛을 끝까지 사냥하지 않고 거점으로 향할 수 있다.", this);
        }
#endif
    }
}

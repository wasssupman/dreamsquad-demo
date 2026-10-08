using UnityEngine;

namespace Somnia.Battle.Data.BattleView
{
    // battle-core-rebuild unit 5a — 「뜬 높이」의 시각 반응 노브.
    //
    // 옛 브리지 직렬화 필드의 새 주인이다(`liftScalePerHeight`·`liftScaleMax`·
    // `liftShadowFullHeight`·`liftShadowMinAlpha`·`spineDefenderYOffset`·`spawnHeight`).
    // 브리지에 있던 시절 이 값들은 **static 미러**로 뷰에 흘렀고, 그래서 「그 값의 주인은
    // 브리지」라는 잘못된 신호가 됐다. 여기서는 뷰 풀이 이 자산을 들고 자기 뷰에 넘긴다.
    //
    // ⚠ 원근 보상이라 **화면 전역 단일 소유**다. 유닛별로 저작하지 않는다 — 같은 높이의
    // 두 유닛이 다른 크기로 보이면 그 순간 높이가 거리를 말하지 않게 된다.
    [CreateAssetMenu(menuName = "Somnia/Battle/BattleView/Unit Lift Knobs", fileName = "UnitLiftKnobs")]
    public sealed class UnitLiftKnobs : ScriptableObject
    {
        [Header("뜬 높이 → 확대")]
        [Tooltip("단위 높이당 확대 비율. 0.14 = 1 만큼 뜨면 14% 커진다.")]
        [SerializeField, Min(0f)] private float liftScalePerHeight = 0.14f;

        [Tooltip("확대 상한. 1 미만으로 내려가지 않는다 — 오설정이 유닛을 축소시키지 않게.")]
        [SerializeField, Min(1f)] private float liftScaleMax = 1.35f;

        [Header("뜬 높이 → 그림자")]
        [Tooltip("그림자가 가장 옅어지는 높이.")]
        [SerializeField, Min(0.01f)] private float liftShadowFullHeight = 3f;

        [Tooltip("그 높이에서의 그림자 알파 배율.")]
        [SerializeField, Range(0f, 1f)] private float liftShadowMinAlpha = 0.35f;

        [Header("스폰·배치 높이")]
        [Tooltip("Spine 방어유닛 뷰의 Y 보정(월드). 발 피벗이 타일 면과 어긋날 때만 쓴다.")]
        [SerializeField] private float spineDefenderYOffset = 0f;

        [Tooltip("드롭 배치가 시작되는 높이(월드). 뷰 전용 — 코어는 배치 순간을 즉시로 본다.")]
        [SerializeField, Min(0f)] private float spawnHeight = 0.5f;

        public float SpineDefenderYOffset => spineDefenderYOffset;
        public float SpawnHeight => spawnHeight;

        /// <summary>
        /// lift(지면에서 뜬 view 공간 높이) → 시각 반응 세 배율.
        ///
        /// 왜 한 함수인가: 유닛 크기 · 그림자 크기 · 그림자 알파가 **같은 lift 에서 함께
        /// 파생된다**는 것이 계약이다. 뷰마다 따로 계산하면 셋이 다른 lift 를 보고 갈라진다.
        ///
        /// ⚠ `shadowScale` 은 **항상 1** 이다. 그림자 지름은 판정 몸(2r)이라 높이로 흔들면
        /// 「그림자가 링에 닿으면 사거리 안」이 공중에서 거짓이 된다. out 인자로 남겨 둔 것은
        /// 소비처가 셋을 한 자리에서 받게 하기 위해서다(짝이던 축소 노브는 은퇴).
        /// </summary>
        public void Resolve(float lift, out float unitScale, out float shadowScale, out float shadowAlpha)
        {
            // lift <= 0 = 지면 또는 반동으로 내려앉은 구간 — 전부 항등(반응 없음).
            if (lift <= 0f)
            {
                unitScale = 1f;
                shadowScale = 1f;
                shadowAlpha = 1f;
                return;
            }

            unitScale = Mathf.Min(1f + lift * liftScalePerHeight, Mathf.Max(1f, liftScaleMax));
            float r = Mathf.Clamp01(lift / Mathf.Max(0.01f, liftShadowFullHeight));
            shadowScale = 1f;
            shadowAlpha = Mathf.Lerp(1f, liftShadowMinAlpha, r);
        }
    }
}

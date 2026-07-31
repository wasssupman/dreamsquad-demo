using System;
using UnityEngine;

namespace Wassup.Data
{
    // gimmick-recognition-upgrade unit 1 — 기믹 리빌 연출의 수치 소유자.
    // 공통 타이밍 + 기믹당 연출 엔트리. 프리팹/클립은 null 허용이고, 엔트리 자체가
    // 없어도 리빌은 기본 tint 로 성립한다 — 아트가 늦게 와도 기능이 막히지 않는다.
    [CreateAssetMenu(fileName = "GimmickRevealConfig", menuName = "Wassup/Gimmick Reveal Config", order = 26)]
    public class GimmickRevealConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public GimmickData gimmick;
            [Tooltip("화면을 물들이는 색. 기믹의 정체성 색.")]
            public Color tintColor = new Color(1f, 0.82f, 0.35f, 1f);
            [Tooltip("리빌 중 카메라 앞에 띄울 월드 파티클. 없으면 절차적 파티클만.")]
            public GameObject revealVfxPrefab;
            [Tooltip("등장 효과음(unit 2). 없으면 공용 클립 → 무음 순으로 폴백.")]
            public AudioClip sfxClip;
        }

        [Header("타이밍 (unscaled — 드래그 슬로우모/정지 무관)")]
        [Tooltip("① 도장 — 딤 + 틴트 + 아이콘 등장.")]
        public float beatStampSec = 0.6f;
        [Tooltip("② 명명 — 룰 라벨 + 정서 카피 부제.")]
        public float beatNameSec = 0.8f;
        [Tooltip("③ 퇴장 — 한 줄 노출 후 페이드아웃.")]
        public float beatOutSec = 0.6f;
        [Tooltip("한 줄(summary) 을 읽을 시간. ③ 페이드 시작 전 홀드.")]
        public float summaryHoldSec = 0.7f;
        [Tooltip("탭 스킵이 먹기 시작하는 시점. 연출 시작 직후 오탭이 통째로 날리는 걸 막는다.")]
        public float tapSkipGraceSec = 0.25f;

        [Header("모양")]
        [Tooltip("배경 딤 알파. UiOverlay.Dim 보다 옅게 — 맵이 비쳐야 '이 판'이라는 느낌이 산다.")]
        [Range(0f, 1f)] public float dimAlpha = 0.82f;
        // 틴트는 딤 **위에** 평평하게 덧발린다. 값을 올리면 화면 전체가 균일한 색 안개가 되고
        // (딤이 만든 어두운 바탕까지 들어올려) 텍스트 대비가 깎인다. 0.28 로 검증했을 때
        // 숲 (4,29,26) → (35,34,52) 로 오히려 밝아졌다. 딤이 지배하도록 낮게 유지할 것.
        [Tooltip("화면을 물들이는 틴트의 최대 알파. 높이면 텍스트 대비가 깎인다.")]
        [Range(0f, 1f)] public float tintAlpha = 0.12f;
        public float iconSize = 260f;
        [Tooltip("① 도장에서 아이콘이 줄어들며 찍히는 시작 배율.")]
        public float stampFromScale = 2.2f;
        [Tooltip("절차 파티클 개수. 0 이면 생략.")]
        public int particleCount = 14;
        public float particleSize = 26f;
        [Tooltip("절차 파티클이 흩어지는 반경.")]
        public float particleSpread = 420f;

        [Header("엔트리 (미등록 기믹은 아래 기본값으로 돈다)")]
        public Color defaultTint = new Color(1f, 0.82f, 0.35f, 1f);
        public Entry[] entries = Array.Empty<Entry>();

        [Header("사운드 (unit 2)")]
        [Tooltip("엔트리에 sfxClip 이 없을 때 쓰는 공용 등장음. 이것도 없으면 무음.")]
        public AudioClip defaultSfxClip;

        // 미등록이면 null — 호출측이 기본값으로 폴백한다.
        public Entry Find(GimmickData gimmick)
        {
            if (gimmick == null || entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].gimmick == gimmick) return entries[i];
            return null;
        }
    }
}

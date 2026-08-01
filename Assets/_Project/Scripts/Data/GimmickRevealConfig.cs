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
        [Tooltip("③ 퇴장 — 요약 노출 후 페이드아웃.")]
        public float beatOutSec = 0.6f;
        // 요약은 이 연출의 핵심 정보인데 마지막 1/3에만 떠서 읽을 시간이 모자랐다.
        // ② 명명이 끝나기 전에 미리 들어와 텀을 줄인다(0 = 기존처럼 ② 종료 후 시작).
        [Tooltip("요약이 ② 명명 종료보다 얼마나 먼저 들어오는지(초). beatNameSec 로 클램프.")]
        public float summaryLeadSec = 0.35f;
        // 탭 스킵이 있으므로 이 값은 '탭하지 않은 사람에게만 적용되는 하한'이다.
        // 아는 사람은 탭으로 즉시 넘어가므로 넉넉히 잡아도 반복 플레이를 막지 않는다.
        [Tooltip("요약을 읽을 시간. 탭하면 즉시 넘어가므로 넉넉해도 된다.")]
        public float summaryHoldSec = 2f;
        [Tooltip("탭 스킵이 먹기 시작하는 시점. 연출 시작 직후 오탭이 통째로 날리는 걸 막는다.")]
        public float tapSkipGraceSec = 0.25f;

        [Header("모양")]
        [Tooltip("배경 딤 알파. UiOverlay.Dim 보다 옅게 — 맵이 비쳐야 '이 판'이라는 느낌이 산다.")]
        [Range(0f, 1f)] public float dimAlpha = 0.82f;
        // 틴트는 딤 **위에** 평평하게 덧발리므로, 올릴수록 딤이 만든 어두운 바탕을 되들어올려
        // 텍스트 대비를 깎는다. 0.12 는 딤이 지배하게 두는 보수적 기본값 — 실기 확인 후 조정.
        // (MCP 스크린샷은 오버레이 캔버스를 충실히 합성하지 않아 색 판단에 쓸 수 없다.)
        [Tooltip("화면을 물들이는 틴트의 최대 알파. 높이면 텍스트 대비가 깎인다.")]
        [Range(0f, 1f)] public float tintAlpha = 0.12f;
        public float iconSize = 260f;
        [Tooltip("① 도장에서 아이콘이 줄어들며 찍히는 시작 배율.")]
        public float stampFromScale = 2.2f;

        [Header("레이아웃 (1920×1080 기준, 화면 중앙 = 0)")]
        [Tooltip("아이콘 중심 y. 제목보다 위에 둔다.")]
        public float iconOffsetY = 260f;
        [Tooltip("룰 라벨(제목) y. 화면 중앙 근처가 기본.")]
        public float titleOffsetY = 20f;
        [Tooltip("제목 아래 정서 카피까지의 간격.")]
        public float subtitleGap = 76f;
        [Tooltip("요약 y. 두 줄(원인 / 결과)이라 높이를 넉넉히 잡는다.")]
        public float summaryOffsetY = -140f;
        [Tooltip("\"탭하여 계속\" 힌트 y. 빠른 길을 보이게 해서 늘어난 홀드가 아무도 막지 않게 한다.")]
        public float tapHintOffsetY = -290f;
        [Tooltip("② 명명에서 제목 블록이 떠오르는 시작 오프셋(아래로).")]
        public float titleRiseFrom = -64f;
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

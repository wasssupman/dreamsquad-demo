using UnityEngine;

namespace Wassup.Data
{
    // sprite-flipbook-player unit 1 — 플립북 1개의 고유 속성(프레임·fps·루프).
    // 여러 소비자가 같은 애니메이션을 공유하므로 컴포넌트 직렬화가 아니라 에셋이다.
    //
    // TimeDomain 은 여기에 없다 — 도메인은 "이 애니메이션이 무엇인가"가 아니라 "어디에 쓰이는가"라
    // 같은 에셋이 전투 이펙트와 UI 연출에 동시에 쓰일 수 있다. 도메인은 재생기 인스턴스가 소유한다.
    //
    // 프레임은 컷 모드(개별 스프라이트 수동 할당)든 통 모드(시트 슬라이스 → 에디터 유틸이 주입)든
    // 결국 임포트된 에셋 참조다. 런타임에 Sprite 를 생성하지 않는다(수명 관리·leak 이 아예 없다).
    [CreateAssetMenu(menuName = "Wassup/Sprite Flipbook", fileName = "SpriteFlipbook")]
    public class SpriteFlipbookData : ScriptableObject
    {
        [Tooltip("재생 순서대로의 프레임. 통 시트는 인스펙터의 '시트에서 채우기' 로 주입한다.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("초당 프레임.")]
        [SerializeField] private float fps = 24f;

        [Tooltip("체크 시 무한 반복. 해제 시 1회 재생 후 마지막 프레임에서 정지.")]
        [SerializeField] private bool loop;

        public int FrameCount => frames != null ? frames.Length : 0;
        public float Fps => fps;
        public bool Loop => loop;

        // 배열 자체는 내보내지 않는다 — 소비자가 밖에서 배열을 바꿔 SO 상태를 오염시킬 수 없게.
        // 범위 밖 인덱스는 null. FlipbookMath.FrameAt 이 "프레임 없음" 에 -1 을 주므로
        // 그 값이 그대로 흘러들어와도 예외가 나지 않아야 한다.
        public Sprite FrameAt(int index)
        {
            if (frames == null || index < 0 || index >= frames.Length) return null;
            return frames[index];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (fps <= 0f)
                Debug.LogWarning($"SpriteFlipbookData '{name}': fps 가 {fps} — 원샷이면 재생이 시작하자마자 완료로 판정된다.", this);

            if (frames == null) return;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null) continue;
                // 재생기는 빈 슬롯에서 직전 프레임을 유지한다(렌더러가 순간 비는 것보다 낫다).
                // 즉 프레임이 사라지는 게 아니라 앞 프레임이 그만큼 길어진다 — 타이밍 디버깅에서
                // 헷갈리지 않도록 증상을 정확히 적는다.
                Debug.LogWarning($"SpriteFlipbookData '{name}': 프레임 {i} 이 비어 있다 — " +
                                 (i == 0
                                     ? "재생 시작 시 직전 스프라이트(렌더러에 authored 된 것)가 그대로 남는다."
                                     : "재생 시 직전 프레임이 그만큼 길게 유지된다."), this);
            }
        }
#endif
    }
}

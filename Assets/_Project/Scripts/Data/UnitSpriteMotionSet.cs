using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Wassup.Data
{
    // sprite-unit-backend unit 0 — 유닛 하나의 스프라이트 모션 세트. 「모션당 시트 1장」.
    //
    // 슬롯은 Spine 유닛이 실제로 쓰는 모션과 1:1 이다(ISpineUnitVisualData / IDefenderSpineExtras):
    //   idle · walk · attack · death (전 유닛) · deploy · drag (방어유닛) + 대기 컷 풀(unit 6, 아래 참조).
    //
    // 유닛 SO 는 이 세트를 가리키는 **필드 하나**만 갖는다. 비면 Spine, 있으면 스프라이트 —
    // 되돌리기 = 그 필드 비우기(임시 기능 전제). 필드를 7개로 흩뿌리면 되돌리기가 「7개 지우기」가 된다.
    //
    // 빈 슬롯의 폴백은 아래 순수 메서드가 정한다. Spine 의 ResolveAnimation 후보 체인과 같은 정신 —
    // 단 idle 은 폴백이 없다(HasIdle 이 거짓이면 세트 자체가 무효라 풀이 쿼드 폴백으로 보낸다).
    [CreateAssetMenu(menuName = "Wassup/Unit Sprite Motion Set", fileName = "MotionSet")]
    public class UnitSpriteMotionSet : ScriptableObject
    {
        [Header("전 유닛")]
        [Tooltip("대기 루프. 유일한 필수 슬롯 — 비면 이 세트는 무효(Spine/쿼드 폴백).")]
        [SerializeField] private SpriteFlipbookData idle;
        [Tooltip("이동 루프. 비면 이동/정지 구분 없이 idle 단일 루프(타일 방어유닛의 walkAnimation:\"\" 과 같은 규칙).")]
        [SerializeField] private SpriteFlipbookData walk;
        [Tooltip("공격 원샷. 발사 주기보다 길면 압축 재생된다.")]
        [SerializeField] private SpriteFlipbookData attack;
        [Tooltip("사망 원샷. 비면 사망 즉시 파괴.")]
        [SerializeField] private SpriteFlipbookData death;

        [Header("방어유닛")]
        [Tooltip("배치 착지 원샷. 비면 drag → attack → idle 순으로 폴백.")]
        [SerializeField] private SpriteFlipbookData deploy;
        [Tooltip("드래그 중 루프. 비면 idle.")]
        [SerializeField] private SpriteFlipbookData drag;

        // sprite-unit-backend unit 6 → idle-break-shared(2026-09-17) — 대기 컷(idle break). 비어 있으면 idle 단일 루프(현행).
        // 틀: **기본 idle 루프가 항상 돌고**, N초마다 여기 있는 컷 하나를 한 바퀴 끼운 뒤 루프로 돌아온다. 기본 idle 은 풀에 없다.
        // 「한 바퀴」는 뷰가 FlipbookMath.Duration 으로 재서 끝낸다 — 시트의 loop 체크박스를 보지 않는다. 그래서 슬롯별 루프 정책은
        // 상수(idle = 루프)로 남는다. 규칙 자체(타이머·직전 회피)는 IdleBreakCycle(Presentation)이 Spine 과 공유한다.
        [Header("대기 컷 (idle breaks)")]
        [Tooltip("N초마다 한 번 끼워 넣는 대기 모션들. 기본 idle 은 여기 넣지 않는다. loop 설정은 무관(한 바퀴만 튼다).")]
        [SerializeField] private List<SpriteFlipbookData> idleBreaks = new List<SpriteFlipbookData>();
        [Tooltip("컷 사이 간격(초) 범위. (N,N) = 고정 N초. (0,0) = 컷 끝나자마자 다음 컷.")]
        [FormerlySerializedAs("idleRestGap")]
        [SerializeField] private Vector2 idleBreakInterval = new Vector2(1f, 3f);

        [Header("시트 규약")]
        [Tooltip("시트가 오른쪽(+x)을 보고 그려졌으면 체크. 기본 규약은 Spine 리그와 같이 「왼쪽을 본다」 — " +
                 "SkeletonFlipXModifier 의 데이터 대응이다(코드 분기 대신 여기서 정규화).")]
        [SerializeField] private bool sheetFacesRight;

        public SpriteFlipbookData Idle => idle;
        public SpriteFlipbookData Walk => walk;
        public SpriteFlipbookData Attack => attack;
        public SpriteFlipbookData Death => death;
        public SpriteFlipbookData Deploy => deploy;
        public SpriteFlipbookData Drag => drag;
        public bool SheetFacesRight => sheetFacesRight;

        public bool HasIdle => idle != null;

        // 대기 컷이 저작돼 있나. 풀 = idleBreaks 만(기본 idle 은 항상 도는 것이지 끼워 넣는 것이 아니다).
        public bool HasIdleBreaks => idleBreaks != null && idleBreaks.Count > 0;
        public int IdleBreakCount => idleBreaks != null ? idleBreaks.Count : 0;
        public SpriteFlipbookData IdleBreakAt(int index) =>
            idleBreaks != null && index >= 0 && index < idleBreaks.Count ? idleBreaks[index] : null;
        public Vector2 IdleBreakInterval => idleBreakInterval;
        // 다음 컷까지의 간격 하나를 뽑는다. roll 은 0..1 난수(프레젠테이션 난수 — sim 난수와 섞지 않는다).
        public float PickIdleBreakInterval(float roll) =>
            Mathf.Max(0f, Mathf.Lerp(idleBreakInterval.x, idleBreakInterval.y, Mathf.Clamp01(roll)));

        // 이동 중이고 walk 가 있으면 walk, 아니면 idle. Spine ResolveLocomotionAnimation 과 같은 규칙.
        public SpriteFlipbookData ResolveLocomotion(bool moving) =>
            moving && walk != null ? walk : idle;

        // Spine PlayDeploy 의 후보 순서(deploy → drag → attack → idle)를 그대로 옮겼다.
        public SpriteFlipbookData ResolveDeploy()
        {
            if (deploy != null) return deploy;
            if (drag != null) return drag;
            if (attack != null) return attack;
            return idle;
        }

        // 드래그 프리뷰용. Spine 쪽 ResolveAnimation(drag, idle, attack) 에서 attack 은 빼는데,
        // 그쪽은 「트랙이 존재하는 첫 이름」 탐색이라 attack 이 끼었을 뿐이고 손끝에서 공격 모션이
        // 도는 것은 의도가 아니다(그 코드의 주석: 「서 있는 그림 — idle 우선」).
        public SpriteFlipbookData ResolveDrag() => drag != null ? drag : idle;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 슬롯별 루프 정책은 상수다 — idle 은 대기 컷 유무와 무관하게 루프(기본 루프가 항상 돈다).
            WarnLoopPolicy(nameof(idle), idle, wantLoop: true);
            if (idleBreaks != null)
                for (int i = 0; i < idleBreaks.Count; i++)
                    if (idleBreaks[i] == null || idleBreaks[i].FrameCount == 0)
                        Debug.LogError($"UnitSpriteMotionSet '{name}': idleBreaks[{i}] 이 비었거나 프레임이 0 — 뽑히면 건너뛴다.", this);
            if (idleBreakInterval.x < 0f || idleBreakInterval.y < idleBreakInterval.x)
                Debug.LogError($"UnitSpriteMotionSet '{name}': idleBreakInterval 은 0 ≤ min ≤ max 여야 한다 (지금 {idleBreakInterval}).", this);
            WarnLoopPolicy(nameof(walk), walk, wantLoop: true);
            WarnLoopPolicy(nameof(drag), drag, wantLoop: true);
            WarnLoopPolicy(nameof(attack), attack, wantLoop: false);
            WarnLoopPolicy(nameof(death), death, wantLoop: false);
            WarnLoopPolicy(nameof(deploy), deploy, wantLoop: false);
        }

        // 원샷 슬롯에 루프 시트가 들어오면 완료 폴링이 영원히 참이라 유닛이 그 모션에 갇힌다.
        // 원인이 컴포넌트가 아니라 에셋 체크박스라 에셋 이름을 지목한다(FlipbookCharacterView 와 같은 이유).
        private void WarnLoopPolicy(string slot, SpriteFlipbookData data, bool wantLoop)
        {
            if (data == null || data.Loop == wantLoop) return;
            // 원인이 에셋 체크박스라 경고로는 묻힌다 — FlipbookCharacterView 와 같이 에러로(리뷰 Minor 2).
            Debug.LogError(wantLoop
                ? $"UnitSpriteMotionSet '{name}': '{slot}' 은 루프 슬롯인데 '{data.name}' 의 loop 가 꺼져 있다."
                : $"UnitSpriteMotionSet '{name}': '{slot}' 은 원샷 슬롯인데 '{data.name}' 의 loop 가 켜져 있다 — 완료 판정이 안 나 유닛이 갇힌다.",
                this);
        }
#endif
    }
}

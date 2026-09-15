using System.Collections.Generic;
using UnityEngine;

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

        // sprite-unit-backend unit 6 — 대기 컷(idle break, 2026-09-16 사용자 결정). 비어 있으면 idle 단일 루프(현행).
        // 하나라도 있으면 idle 상태의 모양이 바뀐다: idle 의 0 프레임으로 **쉬다가** → 쉬는 시간이 끝나면
        // 풀(idle + 대기 컷들)에서 하나를 뽑아 **한 바퀴** 틀고 → 다시 쉰다.
        // ⚠ 「한 바퀴」는 뷰가 FlipbookMath.Duration 으로 재서 끝낸다 — 시트의 loop 체크박스를 **보지 않는다**.
        //   그래서 idle 은 변형 유무와 무관하게 루프 슬롯이고(폴백 소비자들이 그 루프에 기댄다), 슬롯별 루프 정책은
        //   전부 상수로 남는다(리뷰 M1). Spine 의 SpineIdleVariants(애니 이름 · 루프 이어 붙임)와는 이름부터 갈랐다.
        [Header("대기 컷 (idle breaks)")]
        [Tooltip("추가 대기 모션. 하나라도 있으면 「idle 0프레임으로 쉼 → 풀(idle+대기 컷)에서 하나 한 바퀴 → 쉼」을 반복한다. loop 설정은 무관.")]
        [SerializeField] private List<SpriteFlipbookData> idleBreaks = new List<SpriteFlipbookData>();
        [Tooltip("대기 컷 사이 쉬는 시간(초) 범위. 쉬는 동안은 idle 의 0 프레임으로 선다. (0,0) = 쉼 없이 연속 재생.")]
        [SerializeField] private Vector2 idleRestGap = new Vector2(1f, 3f);

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

        // 대기 컷 모드인가. 풀 = idle(0번) + idleBreaks(1번~). 쉬는 그림은 항상 idle 의 0 프레임.
        public bool HasIdleBreaks => idleBreaks != null && idleBreaks.Count > 0;
        public int IdlePoolCount => idle == null ? 0 : 1 + (idleBreaks != null ? idleBreaks.Count : 0);
        public SpriteFlipbookData IdlePoolAt(int index)
        {
            if (index == 0) return idle;
            int v = index - 1;
            return idleBreaks != null && v >= 0 && v < idleBreaks.Count ? idleBreaks[v] : null;
        }
        public Vector2 IdleRestGap => idleRestGap;
        // 쉬는 시간 하나를 뽑는다. roll 은 0..1 난수(프레젠테이션 난수 — sim 난수와 섞지 않는다).
        public float PickIdleRestGap(float roll) =>
            Mathf.Max(0f, Mathf.Lerp(idleRestGap.x, idleRestGap.y, Mathf.Clamp01(roll)));

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
            // 슬롯별 루프 정책은 상수다 — idle 은 대기 컷 유무와 무관하게 루프(대기 컷의 「한 바퀴」는 뷰가 길이를 잰다).
            WarnLoopPolicy(nameof(idle), idle, wantLoop: true);
            if (idleBreaks != null)
                for (int i = 0; i < idleBreaks.Count; i++)
                    if (idleBreaks[i] == null || idleBreaks[i].FrameCount == 0)
                        Debug.LogError($"UnitSpriteMotionSet '{name}': idleBreaks[{i}] 이 비었거나 프레임이 0 — 뽑히면 건너뛴다.", this);
            if (idleRestGap.x < 0f || idleRestGap.y < idleRestGap.x)
                Debug.LogError($"UnitSpriteMotionSet '{name}': idleRestGap 은 0 ≤ min ≤ max 여야 한다 (지금 {idleRestGap}).", this);
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

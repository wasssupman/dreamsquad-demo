using UnityEngine;
using Wassup.Core.TimeControl;

namespace Wassup.Presentation
{
    // instinct-turret-readout unit 1 — 본능(3×3 공격 거점) 프랍의 포신 조준.
    //
    // **도는 것은 포신뿐이다**(spec 계약 5). 받침과 터렛은 고정 — 리그가 이미
    // base → turret → barrel 3단이라 자식 하나만 돌리면 된다. 포신 참조는 프리팹 변형에서
    // 지정한다(이름 문자열 탐색 금지 — 변형마다 노드 이름이 색을 달고 다닌다:
    // cannon_barrel_yellow / cannon_barrel_red).
    //
    // 이 뷰는 게임 규칙을 하나도 소유하지 않는다. 무엇을 겨눌지는 sim(AttackSystem)이 이미
    // 정했고 브리지가 그 사건을 방향으로 풀어 넘긴다. 여기 있는 것은 «그 방향으로 얼마나 빨리
    // 도는가» 하나뿐이다.
    public sealed class StructureTurretView : MonoBehaviour
    {
        [Tooltip("돌릴 포신. 프리팹 변형에서 cannon_barrel_* 을 지정한다.")]
        [SerializeField] private Transform barrel;
        [Tooltip("초당 회전 각도. 쿨(1.5초)보다 충분히 빨라야 «돌고 나서 쏜다»로 읽힌다.")]
        [Min(1f)] [SerializeField] private float turnDegreesPerSecond = 540f;

        private float _currentYaw;
        private float _targetYaw;

        // 방향은 **보드 평면 위의 월드 벡터**다(브리지가 BoardSpace 로 풀어서 준다).
        // 퇴화 벡터(대상이 내 발밑)는 마지막 조준을 유지한다 — 0 으로 스냅하면 포신이
        // 엉뚱하게 홱 돈다.
        public void AimAt(Vector3 boardDir)
        {
            if (boardDir.x * boardDir.x + boardDir.z * boardDir.z < 1e-6f) return;
            // 총구는 포신의 로컬 +Z 다(오프스크린 렌더로 확인). atan2(x, z) = +Z 기준 yaw.
            _targetYaw = Mathf.Atan2(boardDir.x, boardDir.z) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            if (barrel == null) return;
            // 배틀 도메인 시계 — 슬로모가 걸리면 포신도 같이 느려진다(Time.deltaTime 직독 금지).
            float dt = TimeManager.Instance.DeltaTime(TimeDomain.Battle);
            if (dt <= 0f) return;
            // MoveTowardsAngle 이 최단호를 보장한다 — 반대편 대상으로 바꿔도 한 바퀴 돌지 않는다.
            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, _targetYaw, turnDegreesPerSecond * dt);
            // **월드 회전**으로 쓴다. 프랍은 브리지가 world rotation = identity 로 세우므로
            // (SpawnStructureViews), 부모가 회전해도 포신의 조준이 따라 틀어지지 않는다.
            barrel.rotation = Quaternion.AngleAxis(_currentYaw, Vector3.up);
        }
    }
}

using System.Collections.Generic;

namespace Wassup.Skills
{
    // skill-layer-foundation unit 3 — skillId → concrete.
    //
    // 디스패처가 이걸 들고 있고, 감지측(Burst)은 슬롯에 베이크된 **숫자**만 안다.
    // 그 분리가 계약 12 다 — Burst 코드는 managed 레지스트리를 읽을 수 없다.
    //
    // **fail-closed.** 미등록 skillId 는 조용한 no-op 이 아니라 loud 거절이다.
    // 선례: `DcApplicability` 의 `default → Unclassified` — 배선 누락을 침묵으로
    // 넘기면 「스킬이 안 나가는데 아무도 모르는」 상태가 된다.
    public sealed class SkillRegistry
    {
        public const int LegacyArmId = 0;   // 0 = 아직 이전 안 된 arm 이 처리한다

        private readonly Dictionary<int, ISkill> _byId = new Dictionary<int, ISkill>();

        public void Register(ISkill skill)
        {
            if (skill == null)
                throw new System.ArgumentNullException(nameof(skill));
            if (skill.SkillId == LegacyArmId)
                throw new System.ArgumentException(
                    $"skillId {LegacyArmId} 은 legacy arm 예약값이다 — concrete 가 쓸 수 없다.");
            if (_byId.ContainsKey(skill.SkillId))
                throw new System.ArgumentException(
                    $"skillId {skill.SkillId} 중복 등록: {_byId[skill.SkillId].GetType().Name} vs {skill.GetType().Name}");
            _byId.Add(skill.SkillId, skill);
        }

        // 미등록이면 false. 호출측이 loud 하게 알린다 — 여기서 던지지 않는 이유는
        // 드레인 루프 한복판이라 한 슬롯의 배선 실수가 그 프레임 전체를 죽이면 안 되기 때문이다.
        public bool TryGet(int skillId, out ISkill skill) => _byId.TryGetValue(skillId, out skill);

        public int Count => _byId.Count;

        // 완전성 테스트용 — 등록된 id 전수. 「저작된 스킬 전부가 등록됐나」를
        // 테스트가 물을 수 있어야 배선 누락이 런타임까지 안 간다.
        public IEnumerable<int> RegisteredIds => _byId.Keys;
    }
}

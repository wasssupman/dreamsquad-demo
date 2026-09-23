using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 5a — **정의표 줄 번호 → 저작 에셋** 되찾기 표.
    //
    // 정의표는 plain 이라 프리팹·머티리얼·스켈레톤을 모른다(계약 6). 그런데 뷰는 그것이 있어야
    // 무엇을 그릴지 안다. 사건이 나르는 `DefIndex` 로 되찾으려면 **정의표를 만든 그 순회가
    // 매긴 번호**와 같은 순서의 에셋 목록이 필요하다.
    //
    // ⚠ 이 목록을 뷰 쪽에서 **다시 모으지 않는다.** 두 벌이 되는 순간 「표엔 있는데 아무도
    // 안 가리키는 탄」과 그 반대가 조용히 생기고, 그 어긋남은 탄이 엉뚱한 프리팹으로 날아가는
    // 모습으로만 드러난다. 그래서 번호를 매긴 쪽이 목록도 같이 내놓는다.
    //
    // 선택 인자다 — 넘기지 않으면 빌더는 아무것도 채우지 않고 지금까지와 똑같이 돈다
    // (헤드리스 lane·EditMode 테스트는 뷰가 없어 이 표가 필요 없다).
    public sealed class MatchViewAssets
    {
        private readonly List<ProjectileData> _projectiles = new List<ProjectileData>();
        private readonly List<StructureData> _structures = new List<StructureData>();

        public IReadOnlyList<ProjectileData> Projectiles => _projectiles;
        public IReadOnlyList<StructureData> Structures => _structures;

        public void SetProjectiles(List<ProjectileData> rows)
        {
            _projectiles.Clear();
            if (rows != null) _projectiles.AddRange(rows);
        }

        public void SetStructures(List<StructureData> rows)
        {
            _structures.Clear();
            if (rows != null) _structures.AddRange(rows);
        }

        public ProjectileData Projectile(int defIndex)
            => defIndex >= 0 && defIndex < _projectiles.Count ? _projectiles[defIndex] : null;

        public StructureData Structure(int defIndex)
            => defIndex >= 0 && defIndex < _structures.Count ? _structures[defIndex] : null;
    }
}

using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Core;

namespace Wassup.Presentation
{
    // battle-core-rebuild unit 5b — 새 전투 코어의 판을 **카메라 소유자에게 먹여 주는** 한 줄.
    //
    // `CameraDirector` 는 포즈의 유일한 런타임 쓰기 주체이고, 입력은 **미는 것뿐**이라는 계약을
    // 갖는다(「Director 가 맵이나 브리지에서 당겨오지 않는다 — 그 유혹이 경계 우회의 입구다」).
    // 옛 전투에서 그 push 를 브리지가 했다. 새 층에는 브리지가 없으므로 이 컴포넌트가 한다.
    //
    // 미는 것 셋:
    //   ① **보드 bounds** — 맵마다 크기가 달라(12×10 ~ 20×12) 카메라가 판에 맞춰 물러나야 한다.
    //      ⚠ 5a 의 Play 에서 스테이지가 잘리고 회색 띠가 보였던 원인이 **이것이 없었다**는
    //      것이다: `_hasBoardBounds` 가 거짓이면 Director 는 포즈를 **아예 안 쓴다**(레시피가
    //      있어도). 카메라 값을 새로 지어낼 문제가 아니었다.
    //   ② **스테이지 포스트 볼륨** — 스테이지 프리팹 안에 있어 씬에서 미리 배선할 수 없다.
    //   ③ **페이즈** — 배치/전투 레시피를 고르는 축. 이 씬에는 `GameManager` 가 없다
    //      (매니저를 두지 않는 것이 새 코어의 절대 제약 1).
    //
    // ⚠ 이 파일이 `Scripts/Presentation/` 에 있는 이유: Unity 층 컴파일 검사 lane
    // (`BattleCoreUnity.Check.csproj`)이 **`BattleCoreUnity/**` 만** 컴파일하고 나머지는 옛
    // `Wassup.Runtime.dll` 로 받는다. `CameraDirector.SetPhase` 는 그 dll 에 아직 없으므로,
    // 호출부가 `BattleCoreUnity/` 안에 있으면 그 lane 이 거짓 빨강이 된다. 에디터에서는
    // 둘 다 같은 어셈블리(`Wassup.Runtime`)라 차이가 없다.
    //
    // 규칙은 하나도 없다 — 읽고 민다. 판정이 여기 들어오면 그것이 새 브리지의 첫 줄이다.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]   // Director(-90) 보다 **먼저** 민다(그 프레임에 반영되게)
    public sealed class CoreCameraFeed : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CameraDirector _director;

        private bool _pushed;
        private GamePhase _lastPhase = GamePhase.None;

        private void OnEnable()
        {
            _pushed = false;
            _lastPhase = GamePhase.None;
        }

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;
            var director = EnsureDirector();
            if (director == null) return;

            if (!_pushed) PushBoard(director);
            PushPhase(director);
        }

        // 격자가 선 뒤 **한 번**. 판이 바뀌면(맵 교체) 드라이버가 새 판을 걸므로 그때 다시 민다.
        private void PushBoard(CameraDirector director)
        {
            if (!BoardSpace.IsConfigured) return;
            var grid = _driver.GridSize;
            if (grid.x <= 0 || grid.y <= 0) return;

            var plane = _driver.BoardGrid;
            if (plane == null) return;

            // bounds 는 **플레이 그리드**다. 바닥 렌더러 실측이 아니다 — 그쪽은 주변 데코
            // 지대까지 포함해(20×12 → 35×32) 카메라가 과하게 물러난다.
            var b = new Bounds(plane.CellToWorld(new Vector3Int(0, 0, 0)), Vector3.zero);
            b.Encapsulate(plane.CellToWorld(new Vector3Int(grid.x, 0, 0)));
            b.Encapsulate(plane.CellToWorld(new Vector3Int(0, grid.y, 0)));
            b.Encapsulate(plane.CellToWorld(new Vector3Int(grid.x, grid.y, 0)));
            director.SetBoardBounds(b);

            // 스테이지가 볼륨을 안 들고 있으면 **조용히 지나가지 않는다** — 씬 Post 를
            // 스테이지로 옮기며 참조가 끊겨 비네트가 로그 한 줄 없이 사라진 선례가 있다.
            var volume = _driver.StageRoot != null
                ? _driver.StageRoot.GetComponentInChildren<UnityEngine.Rendering.Volume>(true)
                : null;
            if (volume == null && _driver.StageRoot != null)
                Debug.LogWarning($"[CoreCameraFeed] 스테이지 '{_driver.StageRoot.name}' 에 Volume 이 없다 — "
                    + "스트레스 비네트가 그려지지 않는다.", this);
            director.SetPostVolume(volume);

            _pushed = true;
        }

        // 코어의 국면(배치/전투) → 카메라 페이즈. **판 밖 국면(결과·집계)은 코어에 없다**(G2).
        //
        // unit 5c — 코어가 아는 것은 「끝났다」 하나이고, 그 뒤가 무슨 화면인지는 Unity 층이
        // 정한다. 집계(`Tally`)는 은퇴했으므로(X19) 종료 = 곧 `Result` 다.
        // ⚠ 디렉터는 `Result` 를 전투 상태로 접는다(`ResolveState` — 결과는 전면 UI 라 별도
        // 그림이 필요 없다). 그래서 **오늘 화면은 안 바뀐다** — 그래도 미는 이유는 밀지 않으면
        // 디렉터가 전투 중이라고 믿게 되고, 결과 레시피가 생기는 날 그 거짓이 버그가 되기 때문이다.
        private void PushPhase(CameraDirector director)
        {
            var clock = _driver.Match.Clock;
            var phase = clock.Ended ? GamePhase.Result
                      : clock.Phase == MatchPhase.Placement ? GamePhase.Placement
                      : GamePhase.Battle;
            if (phase == _lastPhase) return;
            _lastPhase = phase;
            director.SetPhase(phase);
        }

        private CameraDirector EnsureDirector()
        {
            if (_director != null) return _director;
            var cam = Camera.main;
            _director = cam != null ? cam.GetComponent<CameraDirector>() : null;
            return _director;
        }
    }
}

using UnityEngine;

namespace Wassup.UI
{
    // outgame-login-gate Unit 3 — dev/QA-only UI group. Same gate rule as
    // StatRefreshButtonView: hidden in non-development builds.
    public class DevOnlyGroup : MonoBehaviour
    {
        private void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
                gameObject.SetActive(false);
        }
    }
}

using UnityEngine;
using SwDreams.Shared.Platform.Domain;

namespace SwDreams.Shared.Platform.Adapter
{
    /// <summary>
    /// 플랫폼 서비스 싱글턴. 다른 매니저들이 PlatformBootstrap.Service 로 접근.
    ///
    /// 셋업: MenuScene 또는 부트 씬에 빈 GameObject + 이 컴포넌트.
    /// DontDestroyOnLoad 로 씬 전환 무관하게 유지.
    ///
    /// Phase A: PlatformType.Local 만 동작 (PlayerPrefs 백엔드).
    /// Phase B/C 에서 StovePlatformService / SteamPlatformService 추가.
    /// </summary>
    public class PlatformBootstrap : MonoBehaviour
    {
        public static PlatformBootstrap Instance { get; private set; }
        public static IPlatformService Service { get; private set; }

        [SerializeField] private PlatformType platformType = PlatformType.Local;

        /// <summary>
        /// 인스펙터 셋업이 누락됐을 때 lazy 자동 생성 (PlatformType.Local 기본).
        /// MetaProgressStore 가 첫 호출 시 호출. SDK 도입 후 명시적 셋업 권장.
        /// </summary>
        public static PlatformBootstrap GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(PlatformBootstrap));
            return go.AddComponent<PlatformBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Service = CreateService(platformType);
            Service.Initialize();
            Debug.Log($"[Platform] Bootstrap initialized: {platformType}");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Service?.Shutdown();
            Service = null;
            Instance = null;
        }

        private static IPlatformService CreateService(PlatformType type)
        {
            switch (type)
            {
                case PlatformType.Local:
                    return new LocalPlatformService();
                case PlatformType.Stove:
                case PlatformType.Steam:
                    Debug.LogWarning($"[Platform] {type} 구현체 미존재 (Phase B/C). Local 로 fallback.");
                    return new LocalPlatformService();
                default:
                    return new LocalPlatformService();
            }
        }
    }
}

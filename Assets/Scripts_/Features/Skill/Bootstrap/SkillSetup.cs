using Features.Skill.Infrastructure;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    /// <summary>
    /// PlayerCharacter 프리팹에 부착.
    /// 로컬 플레이어 → SkillBootstrap에 네트워크 어댑터 + 트랜스폼 연결.
    /// 원격 플레이어 → SkillBootstrap에 콜백 포트 등록 (원격 이펙트 재생용).
    /// </summary>
    public sealed class SkillSetup : MonoBehaviour
    {
        [SerializeField] private SkillNetworkAdapter _networkAdapter;

        private void Start()
        {
            if (_networkAdapter == null)
            {
                Debug.LogError("[SkillSetup] SkillNetworkAdapter is missing.");
                return;
            }

            var bootstrap = FindObjectOfType<SkillBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[SkillSetup] SkillBootstrap not found in scene.");
                return;
            }

            if (_networkAdapter.photonView.IsMine)
                bootstrap.ConnectLocalPlayer(_networkAdapter, transform);
            else
                bootstrap.RegisterRemotePlayer(_networkAdapter);
        }
    }
}

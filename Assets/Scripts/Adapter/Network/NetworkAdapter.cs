using Photon.Pun;
using UnityEngine;

namespace Adapter.Network
{
    public class NetworkAdapter : MonoBehaviourPun
    {
        public void NotifySkillTriggered(int actorNumber, int skillId)
        {
            Debug.Log($"Skill RPC mapped. actor={actorNumber}, skill={skillId}");
        }

        public void NotifyDamageApplied(int targetViewId, float damage)
        {
            Debug.Log($"Damage RPC mapped. target={targetViewId}, damage={damage}");
        }

        [PunRPC]
        public void RPC_NotifySkillTriggered(int actorNumber, int skillId)
        {
            NotifySkillTriggered(actorNumber, skillId);
        }

        [PunRPC]
        public void RPC_NotifyDamageApplied(int targetViewId, float damage)
        {
            NotifyDamageApplied(targetViewId, damage);
        }
    }
}

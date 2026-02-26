using UnityEngine;
using Photon.Pun;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 로컬 플레이어를 부드럽게 추적하는 카메라.
    /// Main Camera에 부착.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private float zOffset = -10f;

        private Transform target;

        private void LateUpdate()
        {
            if (target == null)
            {
                FindLocalPlayer();
                return;
            }

            Vector3 desired = new Vector3(target.position.x, target.position.y, zOffset);
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }

        private void FindLocalPlayer()
        {
            foreach (var pv in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
            {
                if (pv.IsMine && pv.CompareTag("Player"))
                {
                    target = pv.transform;
                    break;
                }
            }
        }
    }
}
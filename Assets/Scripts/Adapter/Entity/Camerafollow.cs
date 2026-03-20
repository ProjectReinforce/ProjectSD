using UnityEngine;
using Photon.Pun;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 로컬 플레이어를 부드럽게 추적하는 카메라.
    /// Main Camera에 부착. Orthographic 전용.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Orthographic 줌")]
        [Tooltip("카메라 반높이 (작을수록 줌인). Inspector에서 조정.")]
        [SerializeField] private float orthoSize = 5f;

        private Transform target;
        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
                cam.orthographicSize = orthoSize;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                FindLocalPlayer();
                return;
            }

            // z는 -10 고정 (스프라이트 뒤에 위치해야 렌더링됨)
            Vector3 desired = new Vector3(target.position.x, target.position.y, -10f);
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
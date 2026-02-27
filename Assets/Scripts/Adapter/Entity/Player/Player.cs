using Photon.Pun;
using UnityEngine;

namespace Adapter.Entity.Player
{
    public class Player : MonoBehaviourPun
    {
        [SerializeField] private float moveSpeed = 5f;

        private Camera mainCamera;

        private void Start()
        {
            if (photonView.IsMine)
            {
                mainCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (!photonView.IsMine)
            {
                return;
            }

            var x = Input.GetAxisRaw("Horizontal");
            var y = Input.GetAxisRaw("Vertical");
            var direction = new Vector2(x, y).normalized;

            transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);

            if (mainCamera != null)
            {
                var camPos = mainCamera.transform.position;
                mainCamera.transform.position = new Vector3(transform.position.x, transform.position.y, camPos.z);
            }
        }
    }
}

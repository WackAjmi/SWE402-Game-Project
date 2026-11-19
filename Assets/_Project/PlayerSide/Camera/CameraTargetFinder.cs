using Hollow.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Points the Cinemachine camera at the Knight automatically (via Services.Player),
    /// so the CameraRig prefab works in any scene without dragging the Player in.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class CameraTargetFinder : MonoBehaviour
    {
        CinemachineCamera cam;

        void Awake() => cam = GetComponent<CinemachineCamera>();

        void LateUpdate()
        {
            if (cam.Follow == null && Services.Player != null)
            {
                cam.Follow = Services.Player;
                cam.PreviousStateIsValid = false;   // snap to him instead of gliding in
            }
        }
    }
}

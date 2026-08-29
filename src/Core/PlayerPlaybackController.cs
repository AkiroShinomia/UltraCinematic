using BepInEx.Logging;
using UltraCinematic.Data;
using UnityEngine;

namespace UltraCinematic.Core
{
    internal sealed class PlayerPlaybackController
    {
        private readonly ManualLogSource log;
        private NewMovement movement;
        private Rigidbody body;
        private Transform playerRoot;
        private bool movementWasEnabled;
        private bool bodyWasKinematic;
        private Vector3 cameraLocalOffset;
        private Quaternion cameraToPlayerRotation;
        private Vector3 cameraLocalPositionInPlayer;
        private UnityEngine.Camera controlledCamera;
        private Vector3 originalPlayerPosition;
        private Quaternion originalPlayerRotation;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private float originalCameraFieldOfView;
        private Vector3 originalVelocity;
        private Vector3 originalAngularVelocity;
        private bool restoreOriginalPose;
        private bool active;

        internal PlayerPlaybackController(ManualLogSource logger) { log = logger; }

        internal bool Begin(UnityEngine.Camera camera, bool restorePose = false)
        {
            if (active) return true;
            movement = Object.FindObjectOfType<NewMovement>();
            if (movement == null) { log.LogError("NewMovement not found; playback was not started."); return false; }
            body = movement.GetComponent<Rigidbody>();
            if (body == null) { log.LogError("Player Rigidbody not found; playback was not started."); return false; }
            playerRoot = movement.transform;
            controlledCamera = camera;
            restoreOriginalPose = restorePose;
            movementWasEnabled = movement.enabled;
            bodyWasKinematic = body.isKinematic;
            originalPlayerPosition = playerRoot.position;
            originalPlayerRotation = playerRoot.rotation;
            originalCameraPosition = camera.transform.position;
            originalCameraRotation = camera.transform.rotation;
            originalCameraFieldOfView = camera.fieldOfView;
            originalVelocity = body.velocity;
            originalAngularVelocity = body.angularVelocity;
            cameraLocalOffset = Quaternion.Inverse(camera.transform.rotation) * (playerRoot.position - camera.transform.position);
            cameraToPlayerRotation = Quaternion.Inverse(camera.transform.rotation) * playerRoot.rotation;
            cameraLocalPositionInPlayer = playerRoot.InverseTransformPoint(camera.transform.position);
            if (!restorePose)
            {
                movement.StopMovement();
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            movement.enabled = false;
            body.isKinematic = true;
            active = true;
            return true;
        }

        internal void Follow(CameraState cameraState)
        {
            if (!active || playerRoot == null) return;
            Vector3 position = cameraState.Position + cameraState.Rotation * cameraLocalOffset;
            Quaternion rotation = cameraState.Rotation * cameraToPlayerRotation;
            playerRoot.SetPositionAndRotation(position, rotation);
            if (body != null) { body.position = position; body.rotation = rotation; }
        }

        internal void FollowUpright(CameraState cameraState)
        {
            if (!active || playerRoot == null) return;
            float yaw = cameraState.Rotation.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 position = cameraState.Position - rotation * cameraLocalPositionInPlayer;
            playerRoot.SetPositionAndRotation(position, rotation);
            if (body != null) { body.position = position; body.rotation = rotation; }
        }

        internal void Restore()
        {
            if (!active) return;
            if (restoreOriginalPose && playerRoot != null)
            {
                playerRoot.SetPositionAndRotation(originalPlayerPosition, originalPlayerRotation);
                if (body != null) { body.position = originalPlayerPosition; body.rotation = originalPlayerRotation; }
            }
            if (body != null)
            {
                body.isKinematic = bodyWasKinematic;
                if (!body.isKinematic)
                {
                    body.velocity = restoreOriginalPose ? originalVelocity : Vector3.zero;
                    body.angularVelocity = restoreOriginalPose ? originalAngularVelocity : Vector3.zero;
                }
            }
            if (restoreOriginalPose && controlledCamera != null)
            {
                controlledCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
                controlledCamera.fieldOfView = originalCameraFieldOfView;
            }
            if (movement != null) movement.enabled = movementWasEnabled;
            active = false;
            restoreOriginalPose = false;
            movement = null;
            body = null;
            playerRoot = null;
            controlledCamera = null;
        }
    }
}

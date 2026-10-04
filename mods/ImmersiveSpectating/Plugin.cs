using BepInEx;
using Game.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveSpectating
{
	[BepInPlugin(ModMetadata.Guid, ModMetadata.Name, ModMetadata.Version)]
	public sealed class Plugin : BaseUnityPlugin
	{
		private GameObject cameraObject;
		private CinemachineCamera eyeCamera;
		private PlayerManager hiddenTarget;
		private bool firstPerson;

		private void Awake()
		{
			Logger.LogInfo($"{ModMetadata.Name} v{ModMetadata.Version} loaded.");
		}

		private void Update()
		{
			PlayerManager target = PlayerSpectatorController.CurrentTarget;
			Transform eyes = target?.PlayerController?.EyeTransform;
			if (eyes == null)
			{
				if (firstPerson)
				{
					SetFirstPerson(false, null, null);
				}
				return;
			}

			Mouse mouse = Mouse.current;
			if (mouse != null && mouse.rightButton.wasPressedThisFrame)
			{
				SetFirstPerson(!firstPerson, target, eyes);
			}
			else if (firstPerson && (hiddenTarget != target || eyeCamera.Follow != eyes))
			{
				SetFirstPerson(true, target, eyes);
			}
		}

		private void SetFirstPerson(bool enabled, PlayerManager target, Transform eyes)
		{
			firstPerson = enabled;
			RestoreTarget();
			if (!enabled)
			{
				cameraObject?.SetActive(false);
				return;
			}

			if (cameraObject == null)
			{
				cameraObject = new GameObject("Immersive Spectating Camera");
				cameraObject.transform.SetParent(transform);
				eyeCamera = cameraObject.AddComponent<CinemachineCamera>();
				cameraObject.AddComponent<CinemachineHardLockToTarget>();
				cameraObject.AddComponent<CinemachineRotateWithFollowTarget>();
				eyeCamera.Priority = int.MaxValue;
			}

			hiddenTarget = target;
			target.PlayerModel?.gameObject.SetActive(false);
			cameraObject.transform.SetPositionAndRotation(eyes.position, eyes.rotation);
			eyeCamera.Follow = eyes;
			if (target.mainCam != null)
			{
				eyeCamera.Lens = LensSettings.FromCamera(target.mainCam);
			}
			cameraObject.SetActive(true);
		}

		private void RestoreTarget()
		{
			if (hiddenTarget?.SleepManager != null && hiddenTarget.SleepManager.IsDreaming)
			{
				hiddenTarget.PlayerModel?.gameObject.SetActive(true);
			}
			hiddenTarget = null;
		}

		private void OnDestroy()
		{
			RestoreTarget();
		}
	}
}

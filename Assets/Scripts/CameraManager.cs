using System.Collections.Generic;
using UnityEngine;

//Creating this to make the transition to dots cinemachine cameras easier!
public class CameraManager : MonoBehaviour
{
    public Camera mainCamera;
    public List<CameraPreset> cameraPresets;

    public static CameraManager Instance { get; private set; }

    private void Awake()
    {
        mainCamera = Camera.main;
        if(Instance == null){
            Instance = this;
        } else {
            Destroy(gameObject);
        }
    }

    public void ChangeCamera(string name)
    {
        bool isPlayback = DemoManager.Instance.demoState == DemoState.Playback;
        switch (name)
        {
            case "MainMenu": 
                CinecastManager.Instance.SetSelectedCamera(string.Empty);
                CinecastManager.Instance.SetSelectedPOI(string.Empty);
                if (!EnableCinemachine(false))
                    ReparentCamera(name);
                break;
            case "Auto": 
                CinecastManager.Instance.SetSelectedCamera(string.Empty);
                CinecastManager.Instance.SetSelectedPOI(string.Empty);
                EnableCinemachine(isPlayback);
                break;
            case "WorldView": 
                CinecastManager.Instance.SetSelectedCamera("world");
                CinecastManager.Instance.SetSelectedPOI(string.Empty);
                if (!EnableCinemachine(isPlayback))
                    ReparentCamera(name);
                break;
            default:
                CinecastManager.Instance.SetSelectedCamera("follow");
                CinecastManager.Instance.SetSelectedPOI(name);
                if (!EnableCinemachine(isPlayback))
                    ReparentCamera(name);
                break;
        }
    }

    void ReparentCamera(string name)
    {
        CameraPreset selectedPreset = cameraPresets.Find(x => x.name == name);
        if(selectedPreset == null)
        {
            Debug.Log("No Camera preset with that name found");
            return;
        }

        mainCamera.transform.SetParent(selectedPreset.cameraTransform);
        mainCamera.transform.localPosition = Vector3.zero;
        mainCamera.transform.localRotation = Quaternion.identity;
    }

    bool EnableCinemachine(bool enable)
    {
#if CINECAST_CINEMATOGRAPHER
        if (Cinecast.CM.Hybrid.CinemachineRoot.Instance != null)
        {
            Cinecast.CM.Hybrid.CinemachineRoot.Instance.Enable(enable);
            var listener = mainCamera.GetComponent<Unity.Cinemachine.Hybrid.CmListener>();
            if (listener == null)
                listener = mainCamera.gameObject.AddComponent<Unity.Cinemachine.Hybrid.CmListener>();
            listener.enabled = enable;
            return enable;
        }
#endif
        return false;
    }

}


[System.Serializable]
public class CameraPreset
{
    public string name;
    public Transform cameraTransform;
}

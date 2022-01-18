using System.Collections;
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
        CameraPreset selectedPreset = cameraPresets.Find(x => x.name == name);
        if(selectedPreset == null)
        {
            Debug.Log("No Camera preset with that name found");
            return;
        }

        mainCamera.transform.SetParent(selectedPreset.cameraTransform);
        mainCamera.transform.localPosition = Vector3.zero;
        mainCamera.transform.localRotation = new Quaternion(0,0,0,0);
    }
}

[System.Serializable]
public class CameraPreset
{
    public string name;
    public Transform cameraTransform;
}

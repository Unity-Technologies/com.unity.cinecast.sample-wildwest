using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine.Core;
using Unity.Cinemachine.Hybrid;
using System;
using Unity.Entities.Hybrid;
using Cinecast.CM.Hybrid;
using Unity.Entities;
using Cinecast.Implementation.Application;

#if UNITY_EDITOR
namespace CinemachineHelpers.Editor
{
    [UnityEditor.CustomPropertyDrawer(typeof(GameStateObserver.CameraTypeTemplate))]
    class CameraTypeTemplatePropertyDrawer : UnityEditor.PropertyDrawer
    {
        const int hSpace = 2;

        public override void OnGUI(Rect rect, UnityEditor.SerializedProperty property, GUIContent label)
        {
            var r = rect; r.width = UnityEditor.EditorGUIUtility.labelWidth;
            UnityEditor.EditorGUI.PropertyField(r, property.FindPropertyRelative("Type"), GUIContent.none);
            r.x += r.width + hSpace; r.width = rect.width - (r.width + hSpace);
            UnityEditor.EditorGUI.PropertyField(r, property.FindPropertyRelative("Camera"), GUIContent.none);
        }
    }
}
#endif

namespace CinemachineHelpers
{
    public class GameStateObserver : MonoBehaviour, CinecastObserver.ICameraService
    {
        public CmClearShotAuthoring m_AutomaticCameras;
        public CmTargetGroupAuthoring m_TargetProxy;

        [Serializable]
        public struct CameraTypeTemplate
        {
            public CMCameraType Type;
            public CmNodeAuthoringBase Camera;
        }
        public List<CameraTypeTemplate> m_ManualCameras;

        void Awake()
        {
            CinecastManager.ListenForStartup(() => CinemachineRoot.OnCinecastStarted(this));
        }

        // GML Hackity hack hack
        public int GetCameraEnumValue(string cameraId)
        {
            switch (cameraId)
            {
                case "world": return (int)CMCameraType.Type1;
                case "follow": return (int)CMCameraType.Type2;
                case "freeRoam": return (int)CMCameraType.Type4;
            }
            Debug.LogError($"Unknown camera type {cameraId}");
            return 0;
        }

        public void OnSelectionChanged(
            IReadOnlyList<int> selectedCameraTypes, IReadOnlyList<StableKey> selectedPOIs)
        {
            // If there is a manual camera, then find an appropriate Movie Time target and
            // add it to the target group proxy
            CmNodeAuthoringBase manualCamera = null;
            if (m_TargetProxy != null && m_TargetProxy.IsSynchronized)
            {
                manualCamera = GetManualCamera(selectedCameraTypes);
                if (manualCamera != null)
                {
                    var target = GetMovieTimeTarget(selectedPOIs);

                    // Special handling for FreeRoam camera: it works with or without a target
                    var freeRoam = GetFreeRoamCamera(manualCamera);
                    if (freeRoam != null)
                    {
                        freeRoam.LookAtTarget.Referent = target.IsValid 
                            ? m_TargetProxy.GetComponent<StableID>().Value : StableKey.Default;
                        freeRoam.ReconvertNow();
                    }
                    else if (!target.IsValid)
                        manualCamera = null; // no valid target, don't engage

                    if (manualCamera != null)
                    {
                        // Add target to the target group
                        var buffer = m_TargetProxy.SynchronizedWorld.EntityManager.GetBuffer<CmTargetGroupBufferElement>(
                            m_TargetProxy.SynchronizedEntity);
                        buffer.Clear();
                        buffer.Add(new CmTargetGroupBufferElement
                        {
                            StableKey = target,
                            Weight = 1
                        });
                    }
                }
            }
            for (int i = 0; i < m_ManualCameras.Count; ++i)
                if (m_ManualCameras[i].Camera != null)
                    m_ManualCameras[i].Camera.gameObject.SetActive(m_ManualCameras[i].Camera == manualCamera);
            if (m_AutomaticCameras != null)
                m_AutomaticCameras.gameObject.SetActive(manualCamera == null);

            // Kick the EntityBehaviours to sync the transforms
            EntityBehaviour.ManualUpdateSystems(Unity.Cinemachine.Core.ClientHooks.DefaultWorld);
        }

        CmNodeAuthoringBase GetManualCamera(IReadOnlyList<int> cameraTypes)
        {
            for (int j = 0; j < cameraTypes.Count; ++j)
                for (int i = 0; i < m_ManualCameras.Count; ++i)
                    if (m_ManualCameras[i].Camera != null && (int)m_ManualCameras[i].Type == cameraTypes[j])
                        return m_ManualCameras[i].Camera;
            return null;
        }

        CmCameraAuthoring GetFreeRoamCamera(CmNodeAuthoringBase node)
        {
            return node.GetComponent<FreeRoamCameraController>() != null ? node as CmCameraAuthoring : null;
        }

        StableKey GetMovieTimeTarget(IReadOnlyList<StableKey> selectedPOIs)
        {
            if (selectedPOIs.Count == 0)
            {
                //Debug.LogError("Manual camera can only be used if a POI is selected");
                return StableKey.Default;
            }
            for (int i = 0; i < selectedPOIs.Count; ++i)
            {
                var go = StableIDGameObjectManager.Resolve(selectedPOIs[i]);
                if (go != null)
                {
                    var target = go.transform.GetComponentInChildren<MovieTimeTarget>();
                    if (target != null)
                        return target.GetComponent<StableID>().Value;
                }
            }
            //Debug.LogError("A MovieTimeTarget component is required on the selected POI in order to use Manual camera");
            return StableKey.Default;
        }
    }
}

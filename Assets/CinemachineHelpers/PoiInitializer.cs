using UnityEngine;
using Cinecast.CM.Hybrid;

namespace CinemachineHelpers
{
    [RequireComponent(typeof(CinemachinePoi))]
    public class PoiInitializer : MonoBehaviour
    {
        void Start()
        {
            var name = transform.parent.name;
            GetComponent<CinemachinePoi>().Initialize(name, name);
        }
    }
}
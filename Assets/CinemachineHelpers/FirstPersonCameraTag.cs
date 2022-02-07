using Unity.Cinemachine.Hybrid;
using Unity.Entities;

public struct FirstPersonCameraData : IComponentData {}

/// <summary>
/// Add this to first-person camera types to signal that this is a first-person camera.
/// This will be checked for and advertised to the game so it can hide geometry.
/// </summary>
public class FirstPersonCameraTag : ComponentAuthoringBase<FirstPersonCameraData> {}

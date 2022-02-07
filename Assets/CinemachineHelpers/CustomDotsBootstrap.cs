using UnityEngine;
using Unity.Entities;

// Note: Use this class in a Unity Classic project, to initialize the DOTS environment.
// UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP must be defined in the player settings.

namespace Cinecast.CM.Hybrid
{
    public class CustomDotsBootstrap : ICustomBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StaticInitialize()
        {
            DefaultWorldInitialization.Initialize("Custom DOTS World", false);
        }
    
        // Returns true if the bootstrap has performed initialization.
        // Returns false if default world initialization should be performed.
        public bool Initialize(string defaultWorldName)
        {
    #if !UNITY_EDITOR && !UNITY_DOTSRUNTIME
            var world = new World(defaultWorldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;

            var systemList = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systemList);

            ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);
            return true;
    #else
            return false;
    #endif
        }
    }
}

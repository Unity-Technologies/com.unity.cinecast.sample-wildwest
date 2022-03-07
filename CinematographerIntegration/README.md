# Cinecast Cinematographer Integration

Cinecast Cinematographer is an experimental suite of packages built using [Unity DOTS](https://unity.com/dots) technology.

---

## Getting Started

**Prerequisite: Unity 2020.3.30f1**

To install *Cinecast Cinematographer* into the WildWest sample:

1. Add the package *Cinecast Cinematographer* to the project manifest `Packages/manifest.json`.

    ```diff
    {
      "dependencies": {
        "com.unity.cinecast": "0.10.0-preview",
    +  "com.unity.cinecast.cinematographer": "0.1.0-preview.1",
        ...
      }
    }
     ```

2. Reopen the project.
3. Import the asset package `AssetsWithCinematographer.unitypackage` to the project.  This will overwrite the `Assets/CinemachineHelpers` folder, installing some prefabs and scripts.

To remove *Cinecast Cinematographer* from the WildWest sample:

1. Delete the `Assets/CinemachineHelpers` folder.
2. Import the asset package `AssetsWithoutCinematographer.unitypackage` to the project. This will create a new `Assets/CinemachineHelpers` folder, installing some placeholder prefabs.
3. (optional) Remove the *Cinecast Cinematographer* dependency.

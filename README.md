# Cinecast WildWest Sample

*Tested with Unity 2020.3.30f1*

This sample showcases all the basic functionalities of Cinecast, it acts as a suggestion of how Cinecast SDK could be implemented into a game.

------

## Getting Started

**1. Clone this repository**

  ```shell
  git clone https://github.cds.internal.unity3d.com/unity/com.unity.cinecast.sample-wildwest
  ```

**2. Configure Cinecast SDK**

- Create a new asset folder `Config`
- Create *Cinecast SDK Config* assets (`Assets > Create > Cinecast > SDK > Config`):
  - `ServerRefAssignment_Ingestion`
  - `ServerRefAssignment_Extraction`
  - `ServerRefAssignment_Api`
  - `PlaybackConfig`
  - `RecordingConfig`
  - `CinecastConfig`, and add reference to `RecordingConfig`, `PlaybackConfig` and `CoreConfig`
- Create *Cinecast SDK Core Config* assets (`Assets > Create > Cinecast > Core > Config`):
  - `CoreNetworkConfig`  
  - `ServerRefAssignments`, and add reference to `ServerRegAssignment_Ingestion`, `ServerRefAssignments_Extraction` and `ServerRefAssignments_Api`
  - `CoreConfig`, and add reference to `CoreNetworkConfig` and `ServerRevAssignments`
- Inspect the object `CinecastManager` from the scene `Demo`, and in `Cinecast Config` add reference to `CinecastConfig`

**3. Set Cinecast Authorization Keys**

Inspect the object `CinecastManager` from the scene `Demo`, and update the properties under `Authorization` with the API Keys from the matching distribution in [Cinecast Dev Portal](https://admin.cinecast.tv/).

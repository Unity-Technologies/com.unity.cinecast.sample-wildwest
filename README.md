# Cinecast-Wildwest-Sample
[View this project in Backstage](https://backstage.corp.unity3d.com/catalog/default/component/cinecast-wildwest-sample) <br/>

This sample showcases all the basic functionalities of Cinecast, it acts as a suggestion of how Cinecast could be implemented into a game.

------

## Instructions - How to get started:

1.) Clone this repository
2.) Install Universal Render Pipeline Package (0.10.7) from the Package Manager

3.) Install Cinecast Core Package Version - 0.3.22

4.) Install Cinecast Package Version - 0.9.0

5.) Fix Unsafe.dll error message:

- In the Unity Editor surch for Unsafe, right click the file and select "Reveal in Finder".
- Move the folder the file lives within into the Packages folder
- Now delete the unsafe.dll and its .meta file 
- Return back to the Unity Editor, it wills ask you to Update API's and should now work after a round of recompiling

6.) Setup Cinecast Configs

- Create a folder for your configs
- Right click within the folder,navigate to *Create -> Cinecast -> Core -> Config* and create the following configs files:
  - CoreConfig 
    (needs references to CoreNetworkConfig and ServerRevAsignments)
  - CoreNetworkConfig  
  - ServerRefAssignments 
    (needs reference to ServerRegAsigments_Ingestion, ServerRefAsignments_Extraction and ServerRefAsignements_Api)
- Navigate to *Create -> Cinecast -> SDK -> Config* and create the following files:
  - ServerRefAsignments_Ingestion (change URL to https://dev-in.cinecast.tv/)
  - ServerRefAsignments_Extraction (change URL to https://dev-out.cinecast.tv/)
  - ServerRefAsignments_Api (change URL to https://dev-api.cinecast.gg/)
  - CinecastConfig 
    (needs reference to RecordingConfig, PlaybackConfig and CoreConfig)
  - PlaybackConfig
  - RecordingConfig

------


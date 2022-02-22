Cinecast Cinematographer is an experimental suite of packages built using DOTS technology.

DOTS is used so that many hundreds of smart shot-evaluating cameras can coexist with minimal performance impact.
Shipped with this sample is a snapshot of still-unreleased versions of DOTS 0.50, including:

	- com.unity.entities
	- com.unity.jobs
	- com.unity.mathematics
	- com.unity.platforms*

Also shipped is a pre-release version of Cinemachine.DOTS and cinecast.cinematographer, including:

	- com.unity.stableid
	- com.unity.cinemachine.dots
	- com.unity.cinecast.cinematographer

These packages are not currently available in the package manager.

When DOTS 0.50 is released in early 2022, it will no longer be necessary to ship snapshots of the DOTS packages,
and when stableid, cinemachine.dots, and cinecast.cinematographer become publicly available as 
experimental packages, they won't have to be shipped this way either.

=========

To install Cinecast Cinematographer into the Wild West Demo:

Prerequisite: a DOTS-stream version of Unity (currently 2020.3.14f1-dots, available here: unityhub://2020.3.14f1-dots/051fb20b3877)

1. Unzip DOTS-0.50-prerelease.zip and CinematographerPackages.zip into the Packages folder.  
2. Reopen the project.
3. Import AssetsWithCinematographer.unitypackage to the project.  It will overwrite the Assets/CinemachineHelpers folder, installing some prefabs and scripts.


To remove Cinecast Cinematographer from the Wild West Demo:

1. Delete the Assets/CinemachineHelpers folder.
2. Import AssetsWithoutCinematographer.unitypackage to the project.  It will create a new Assets/CinemachineHelpers folder, containing some placeholder prefabs.
3. (optional) Delete the above-listed DOTS and cinematograhper packages from the Packages folder.

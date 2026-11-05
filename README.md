# liminal uwu

![Rain Exchange, photographed inside the PC world](Preview/Rain%20Exchange.jpg)

Walk out under the pink **uwu** streetlights and pick a platform. All nine districts connect on foot. The ticket machine marked **Any platform** sends you to one of eight destinations and remembers your last stop so that the next departure takes you somewhere different.

## A walk through the interchange

Take the north passage to **Spiral Line**. The train climbs a suspended helix, slows at the top and runs backward down the same track. Turn east to **Hanging Orchard**, where a procession of torii gates starts upright, turns sideways and ends beneath the roots of suspended trees. The shrine’s field gate opens onto the ceiling of an archive.

To the west, **Event Horizon** has a leaning lattice Eiffel Tower and an open view of the black hole. Its rim lights the edge of a pitch-black disk. The field gate here leads to a walk along the archive wall. South of the station there is a wet motor court, an elevated print gallery and one short fluorescent service wing. A stretched airliner hangs above the eastern terminal.

## Gravity travel

- **Ceiling Ward** offers a ceiling walk, a wall walk and a six-direction ride. Use the lit reader on a terminal to travel.
- The two walks use full-size copies of the archive and its skyline rotated through 180° and 90°. You walk on the room's ceiling or side wall using normal VRChat locomotion with a stable headset horizon. Other players in the same gallery can walk with you.
- Inside either gallery, the two archive terminals go back to the ward. The marked arch takes you out into the shrine garden or tower court. Each arch also works in reverse.
- **Six direction ride** changes local player gravity and carries you up, right, forward, down, left and back through the chamber. Jump to exit early. Finishing or leaving the ride restores your previous gravity.

## Open the project

1. In VRChat Creator Companion, choose **Add Existing Project** and select the folder containing **Assets**, **Packages** and **ProjectSettings**.
2. Resolve the pinned Worlds SDK packages, then open the project with **Unity 2022.3.22f1**.
3. Open **Assets → Stillwater → Scenes → liminal uwu - PC** or **liminal uwu - Quest**. The scenes, meshes, art, animations, Udon programs and audio are already saved.

The PC scene contains nine captured room reflections, ripple detail and the longer train. Quest uses a cheaper floor shader, reduced landmark geometry, lighter rain and smaller environment textures. Both have the same paths and gravity destinations. Keep the scene files paired with their respective build targets.

## GitHub checks and export

Each push runs two hosted checks on the committed scenes. One checks scene assets, soundscapes, compiled interactions, sky pixels and platform differences. The other samples the collision geometry to check walkable passages, headroom and arrivals. Its downloadable artifact includes a map for each platform. [Hosted checks](https://github.com/WeepingAngeIs/VRChat-games-2/actions)

The optional licensed workflow exports Windows and Android world bundles through the VRChat SDK, checks the compressed Quest size, and runs the PC gravity interaction test in ClientSim. The repo needs Unity activation in GitHub Actions secrets before this workflow can run. [Remote build setup](Tools/CI/README.md)

When publishing, upload the PC scene with Windows selected, then use the **same world blueprint ID** in the Quest scene and upload it with Android selected. The saved scenes start with an empty blueprint ID. [VRChat cross-platform publishing guide](https://creators.vrchat.com/platforms/android/cross-platform-setup/)

## Inside the world

| Spiral Line | Hanging Orchard |
| --- | --- |
| ![Reciprocating train on its helix](Preview/Spiral%20Line.jpg) | ![Shrine procession and inverted orchard](Preview/Hanging%20Orchard.jpg) |
| **Event Horizon** | **Long-haul Annex** |
| ![Tilted lattice tower](Preview/Event%20Horizon.jpg) | ![Stretched passenger jet](Preview/Long-haul%20Annex.jpg) |
| **Wall walk** | **Ceiling Ward** |
| ![The archive and its windows rotated sideways](Preview/Wall%20walk.jpg) | ![Reading-room furniture hanging from the ceiling](Preview/Ceiling%20Ward.jpg) |

The supplied ORGANISM recording, screenshots and local world-bundle directory informed the transitions between intimate tiled passages and large open architectural spaces. The models, materials, sounds, artwork and layouts in this project were made for **liminal uwu**.

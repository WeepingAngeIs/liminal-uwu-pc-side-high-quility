# liminal uwu

![Rain Exchange photographed in the PC scene](Preview/Rain%20Exchange.jpg)

The signs under the pink **uwu** streetlights give you the next stop. Pick a passage or take a chance on the **Any platform** ticket machine. The expanded design connects fourteen districts with eighteen walking passages, plus two side wings that grow smaller until you have to turn back. The release world uses local signs and has no full-area map.

This revision adds a second exploration circuit, portable video and audio controls, and a longer train approach. Scene generation and validation run on GitHub. The new scenes, multiplayer controls and Quest performance still need their hosted and in-client checks; the images below show the earlier authored build.

## Catch the Aperture Express

Follow **Spiral Line** north from the exchange. At Platform 01, press **Call train down** if the train is overhead. It finishes the climb and can leave the summit early. When the doors open, walk aboard and interact with a window seat. Three carriages carry up to six seated passengers. The new 320-second circuit climbs through the spiral tunnel, follows a longer approach toward the black hole, waits at the aperture and returns. The summit has a small overlook and a return control. Stepping beyond its open edge enters a local pull that returns you to arrival; seated passengers remain on the train. PC and Quest share the ride timing, seats and collision layout.

| Inside the train | At the aperture |
| --- | --- |
| ![Carriage interior](Preview/Aperture%20Express%20interior.jpg) | ![View through the front of the carriage at summit dwell](Preview/PC%20summit.png) |

Both views were captured in Unity. [On the helix](Preview/On%20the%20helix.jpg) · [The PC photon ring from the observatory](Preview/PC%20photon%20ring.png)

## Around the exchange

**Hanging Orchard** leads through a procession of torii to a five-storey pagoda. A broadcast tower rises behind the shrine. The beds of the **Diminishing Baths** catch light from overhead fixtures. In **Night Reception**, the desks, telephones, doorways and ceiling shrink together. The observatory has a leaning Eiffel Tower. A stretched jet hangs over the air terminal. The train platforms, art hall, service wing and rain courts each have their own quieter soundscape.

| Hanging Orchard | Diminishing Baths |
| --- | --- |
| ![The pagoda and illuminated paper screens](Preview/Pagoda%20court.jpg) | ![Decreasing tiled vaults](Preview/Diminishing%20Baths.jpg) |
| **Night Reception** | **The Embankment broadcast** |
| ![Shrinking waiting room](Preview/Night%20Reception.jpg) | ![Gallery transmission](Preview/Embankment%20broadcast.jpg) |

The exchange kiosk, art gallery and archive windows share a new 24-second observatory film: three slow views of the black hole, followed by two seconds of black before the loop restarts. Its ring and lensing use the world sky's optical textures. The former collapsing-city footage and the synchronized buildings above the streets are removed. Screens sit beside the walking routes.

## Beyond the air terminal

The east exit leads to **Ad Alley**, a broad neon street with a video billboard on the pavement. **Last Concourse** opens into an empty departure hall. Beyond it, **Orbital Causeway** crosses space between enormous walls beside the **Asterion** station: a spherical hull with recessed service arrays, antenna decks and radiator fins. **Quiet Stacks** and **Service Returns** close the circuit through oversized storage and branching service corridors.

District visibility follows the current room and its doorway connections; camera frustum and baked occlusion provide further culling. This keeps distant streets out of view while retaining the visible routes ahead. Gameplay, collisions and portable controls stay active when their scenery is hidden.

## Local controls and billboard video

On desktop, press **↑** to open the panel and **↓** to close it. In VR, use the **MENU** button above your left wrist. Ad Alley also has a sidewalk console. Keyboard input is unavailable while VRChat's own menus or text entry capture it.

Paste an HTTPS video link and choose **Load / Share**. Play, Pause and Stop affect the shared screen; using them takes control, and the panel shows the current owner. **Resync** retries only your player. Video volume affects only you. The separate **Rain** and **Atmosphere** sliders control the world soundscape throughout each room, without needing to stand near an emitter.

The billboard uses VRChat's Unity video player on both platforms. Supported file/video links still depend on VRChat's URL settings and platform decoding; live streams requiring AVPro are outside this player's scope. PC renders the billboard at 1024×576 and Quest at 512×288. [VRChat video player support](https://creators.vrchat.com/worlds/udon/video-players/)

## Gravity travel

The ceiling and wall terminals in **Ceiling Ward** open into full-size versions of the archive rotated through 180° and 90°. Your feet meet the former ceiling or wall while your headset horizon stays level. Other visitors can walk beside you. Four return terminals and two reversible arches connect the archive, shrine garden and tower court.

The **Six direction ride** changes player gravity and carries you up, right, forward, down, left and back. Jump to leave early. Completing or exiting the ride restores your previous gravity.

## Open in Creator Companion

Use Unity **2022.3.22f1** and the pinned Worlds SDK. Choose **Open Project** in Creator Companion. When adding a checkout, select the folder containing **Assets**, **Packages** and **ProjectSettings**, then resolve the project packages. The authored scenes are saved under **Assets → Stillwater → Scenes**. Pick **liminal uwu - PC** for Windows and **liminal uwu - Quest** for Android.

The PC sky bends the star field around an absolutely black circle. Moving gas filaments, bright knots and dark dust lanes surround its thin luminous rim. An offline ray lookup supplies the lensing, with four gas layers and a secondary ring sampled in the shader. Wet floors have sharper puddle reflections between rough stone patches, ripple detail and baked fixture lighting. Gentle blue haze gives the upper spiral more depth. Scanned surfaces carry colour, normals, roughness and occlusion. Quest keeps the same sky silhouette, train seats, timing, gravity destinations and collision geometry with a two-layer sky, simplified meshes, smaller textures and a cheaper floor finish.

## Checks and publishing

The hosted checks inspect serialized scene geometry and controls: all fourteen districts and eighteen connections, floor and doorway seams, the train circuit and doors, six seats, cross-platform network IDs, room audio routing, visibility portals, and billboard/menu references. They also inspect black-hole pixels in a captured room reflection. A passing source check does not replace headset performance or multiplayer video testing. [Hosted checks](https://github.com/WeepingAngeIs/liminal-uwu-pc-side-high-quility/actions)

The licensed export workflow bakes occlusion on the runner, exports Windows and Android bundles, checks the Quest download size and exercises the gravity ride in ClientSim. To publish, use the same VRChat world blueprint ID for both uploads. [Remote build setup](Tools/CI/README.md) · [VRChat cross-platform publishing](https://creators.vrchat.com/platforms/android/cross-platform-setup/)

The earlier [PC optics review](https://github.com/WeepingAngeIs/liminal-uwu-pc-side-high-quility/actions/runs/42266262637) captured 30 images and refreshed eleven reflection maps using hosted software rendering. Those results predate this expansion. New scene authoring, Udon compilation, visual checks and export results must be reviewed before treating this revision as release-ready. Headset frame rate, single-pass stereo, video orientation and late-joining playback need device tests.

## References and materials

The supplied ORGANISM recording and screenshots informed the transition from narrow, tiled passages into large open rooms. British Home informed the decision to spend texture detail at arm's length and capture stationary lighting and reflections during authoring. The cache inspection and the creator's account are documented separately. [British Home measurements and sources](Tools/CI/British-Home-reference.md)

The architecture, train, observatory film and wall prints were authored for **liminal uwu**. Five selected furnishings come from [Kenney's Space Station Kit](https://kenney.nl/assets/space-station-kit), under CC0. The space ambience incorporates a quieter, crossfaded excerpt of [The Surreal Truth by Joth](https://opengameart.org/content/ambience-pack-1-sci-fi-horror), also CC0. The distant nebula uses [Blue Nebulae 1 by TonyS / Space Spheremaps](https://www.spacespheremaps.com/blue-nebulae-spheremaps/); its [custom terms](https://www.spacespheremaps.com/about/#licensing-terms) follow CC BY 4.0 with attribution waived and prohibit AI image training. It is used here as a game sky texture. Source files and notices are retained under `Assets/Stillwater/ThirdParty`.

The PC gas shading adapts equations from [tantaneity's MIT black-hole shader](https://github.com/tantaneity/urp-black-hole) for this Built-in project; the wet finish adapts parts of [Mochie's MIT shaders](https://github.com/MochiesCode/Mochies-Unity-Shaders). Their license notices are retained in `Assets/Stillwater/ThirdParty`.

Scanned surface maps come from Poly Haven under CC0. [Blue Floor Tiles 01](https://polyhaven.com/a/blue_floor_tiles_01) · [Brick Wall 005](https://polyhaven.com/a/brick_wall_005) · [Grey Roof Tiles](https://polyhaven.com/a/grey_roof_tiles) · [Wood Plank Wall](https://polyhaven.com/a/wood_plank_wall) · [Painted Plaster Wall](https://polyhaven.com/a/painted_plaster_wall) · [Granite Tile](https://polyhaven.com/a/granite_tile) · [License](https://polyhaven.com/license)

For future art passes, search suitable free asset libraries and open repositories first. Verify the license, retain attribution, and adapt useful detailed models, textures or shaders before defaulting to primitive-built replacements. Preserve clear walking routes and prepare reduced Quest versions alongside the PC assets.


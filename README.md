# liminal uwu

![Rain Exchange photographed in the PC scene](Preview/Rain%20Exchange.jpg)

Glowing, winking **uwu** doors take you through fifteen liminal districts. Choose one of two initial paths; both rejoin the adventure and lead to the Aperture Express finale. The old tunnel interchanges are shuttered, so they no longer create confusing shortcuts. Shrinking side wings and gravity galleries remain optional detours. There is no full-area map in the world.

This revision adds **The Falling Hours**, a giant roofless waterworks between two walls, and changes the train to a player-started service. PC and Quest share the same route, landings and track collisions; PC retains richer materials, reflections and water detail. Hosted review evidence accompanies the authored scenes; headset performance and multi-client playback still need device testing.

## Catch the Aperture Express

The adventure ends at Platform 01. The train waits there until someone presses **Start train**. There are 36 seconds to board before its first departure; after that it repeats its 320-second round trip continuously. Further button presses cannot recall or restart it. Walk aboard and interact with a window seat: three carriages carry up to six seated passengers. The spiral track also has a continuous walkable bed, with small steps on the steep final approach and no roof collider.

The train approaches the black hole, pauses beside an overlook, then returns. Its shared clock keeps late arrivals on the same service. Stepping beyond the overlook's open edge enters a pull that returns you to spawn; seated passengers remain protected. PC and Quest share the timing, seats and collision layout.

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

The next glowing door leads to **Ad Alley**, a broad neon street with a video billboard on the pavement. **Last Concourse** opens into an empty departure hall. Beyond it, **Orbital Causeway** crosses space between enormous walls beside the **Asterion** station: a spherical hull with recessed service arrays, antenna decks and radiator fins. **Quiet Stacks** and **Service Returns** form quieter chapters of oversized storage and service corridors. **The Falling Hours** follows the space road: twin 86-metre cascades fall beside a dry central walkway under the black hole.

| Ad Alley | Orbital Causeway |
| --- | --- |
| ![The sidewalk billboard and clear street](Preview/Ad%20Alley.png) | ![Asterion beside the space road](Preview/Orbital%20Causeway.png) |

District visibility follows the current room and its doorway connections; camera frustum and baked occlusion provide further culling. The shuttered interchanges no longer reveal neighbouring streets. Walking through a glowing door reveals its destination; the two open shrinking annexes retain doorway visibility. Gameplay, collisions and portable controls stay active when their scenery is hidden.

## Local controls and billboard video

On desktop, press **↑** to open the panel and **↓** to close it. In VR, use the **MENU** button above your left wrist. Ad Alley also has a sidewalk console. Keyboard input is unavailable while VRChat's own menus or text entry capture it.

![Portable video and room-audio controls](Preview/Local%20controls.png)

Paste an HTTPS video link and choose **Load / Share**. Play, Pause and Stop affect the shared screen; using them takes control, and the panel shows the current owner. **Resync** retries only your player. Video volume affects only you. The separate **Rain** and **Atmosphere** sliders control the world soundscape throughout each room, without needing to stand near an emitter.

The billboard uses VRChat's Unity video player on both platforms. Supported file/video links still depend on VRChat's URL settings and platform decoding; live streams requiring AVPro are outside this player's scope. PC renders the billboard at 1024×576 and Quest at 512×288. [VRChat video player support](https://creators.vrchat.com/worlds/udon/video-players/)

## Gravity travel

The ceiling and wall terminals in **Ceiling Ward** open into full-size versions of the archive rotated through 180° and 90°. Your feet meet the former ceiling or wall while your headset horizon stays level. Other visitors can walk beside you. Four return terminals and two reversible arches connect the archive, shrine garden and tower court.

The **Six direction ride** changes player gravity and carries you up, right, forward, down, left and back. Jump to leave early. Completing or exiting the ride restores your previous gravity.

## Open in Creator Companion

Use Unity **2022.3.22f1** and the pinned Worlds SDK. Choose **Open Project** in Creator Companion. When adding a checkout, select the folder containing **Assets**, **Packages** and **ProjectSettings**, then resolve the project packages. The authored scenes are saved under **Assets → Stillwater → Scenes**. Pick **liminal uwu - PC** for Windows and **liminal uwu - Quest** for Android.

The PC sky bends the star field around an absolutely black circle. Moving gas filaments, bright knots and dark dust lanes surround its thin luminous rim. An offline ray lookup supplies the lensing, with four gas layers and a secondary ring sampled in the shader. Wet floors have sharper puddle reflections between rough stone patches, ripple detail and baked fixture lighting. Gentle blue haze gives the upper spiral more depth. Scanned surfaces carry colour, normals, roughness and occlusion. Quest keeps the same sky silhouette, train seats, timing, gravity destinations and collision geometry with a two-layer sky, simplified meshes, smaller textures and a cheaper floor finish.

## Checks and publishing

The hosted checks inspect fifteen districts and fifteen adventure doors, the thirty-six sealed tunnel mouths, floor and doorway seams, the train circuit and six seats, cross-platform network IDs, room audio routing, visibility, and billboard/menu references. They also inspect black-hole pixels in a captured room reflection. A passing source check does not replace headset performance or multiplayer video testing. [Hosted checks](https://github.com/WeepingAngeIs/liminal-uwu-pc-side-high-quility/actions)

The licensed export workflow bakes occlusion on the runner, exports Windows and Android bundles, checks the Quest download size and exercises the gravity ride in ClientSim. To publish, use the same VRChat world blueprint ID for both uploads. [Remote build setup](Tools/CI/README.md) · [VRChat cross-platform publishing](https://creators.vrchat.com/platforms/android/cross-platform-setup/)

The [expansion review](https://github.com/WeepingAngeIs/liminal-uwu-pc-side-high-quility/actions/runs/42334991175) captured 32 images, refreshed sixteen PC reflection maps and compiled the world interactions. Both sky variants passed black-center and continuous-rim checks. ClientSim exercised room audio, all three sliders, the portable menu, visibility with persistent collisions, approach growth, seat immunity, pull movement, cancellation and return to arrival. [Visual report](Tools/CI/Reviews/Expansion.json) � [Runtime results](Tools/CI/Reviews/Expansion-runtime.txt). These checks use hosted software rendering and desktop simulation. Headset frame rate, single-pass stereo, video orientation and late-joining playback need device tests.

The [adventure review](https://github.com/WeepingAngeIs/liminal-uwu-pc-side-high-quility/actions/runs/42344837478) checks the actual door and landing clearances, walkable track, waterfall, platform LEDs and carriage audio in both editions. Thirty ClientSim checks cover the compiled interactions, including both opening branches, portal cooldown, the parked train, one-time start, boarding wait, repeating timetable, and seated or standing carriage ambience through the Atmosphere slider. [Adventure report](Tools/CI/Reviews/Adventure.json) | [Runtime results](Tools/CI/Reviews/Adventure-runtime.txt).

## References and materials

The supplied ORGANISM recording and screenshots informed the transition from narrow, tiled passages into large open rooms. British Home informed the decision to spend texture detail at arm's length and capture stationary lighting and reflections during authoring. The cache inspection and the creator's account are documented separately. [British Home measurements and sources](Tools/CI/British-Home-reference.md)

The architecture, train, observatory film and wall prints were authored for **liminal uwu**. Five selected furnishings come from [Kenney's Space Station Kit](https://kenney.nl/assets/space-station-kit), under CC0. The space ambience incorporates a quieter, crossfaded excerpt of [The Surreal Truth by Joth](https://opengameart.org/content/ambience-pack-1-sci-fi-horror), also CC0. The distant nebula uses [Blue Nebulae 1 by TonyS / Space Spheremaps](https://www.spacespheremaps.com/blue-nebulae-spheremaps/); its [custom terms](https://www.spacespheremaps.com/about/#licensing-terms) follow CC BY 4.0 with attribution waived and prohibit AI image training. It is used here as a game sky texture. Source files and notices are retained under `Assets/Stillwater/ThirdParty`.

The PC gas shading adapts equations from [tantaneity's MIT black-hole shader](https://github.com/tantaneity/urp-black-hole) for this Built-in project; the wet finish adapts parts of [Mochie's MIT shaders](https://github.com/MochiesCode/Mochies-Unity-Shaders). Their license notices are retained in `Assets/Stillwater/ThirdParty`.

Scanned surface maps come from Poly Haven under CC0. [Blue Floor Tiles 01](https://polyhaven.com/a/blue_floor_tiles_01) · [Brick Wall 005](https://polyhaven.com/a/brick_wall_005) · [Grey Roof Tiles](https://polyhaven.com/a/grey_roof_tiles) · [Wood Plank Wall](https://polyhaven.com/a/wood_plank_wall) · [Painted Plaster Wall](https://polyhaven.com/a/painted_plaster_wall) · [Granite Tile](https://polyhaven.com/a/granite_tile) · [License](https://polyhaven.com/license)

For future art passes, search suitable free asset libraries and open repositories first. Verify the license, retain attribution, and adapt useful detailed models, textures or shaders before defaulting to primitive-built replacements. Preserve clear walking routes and prepare reduced Quest versions alongside the PC assets.


The waterfall uses Cass Bunting's [MIT-0 water palette](https://github.com/cbunt/unity-voronoi-water), with the original notice retained in `Assets/Stillwater/Media/Water`. Its flowing curtain shader is authored for this Built-in world; the source repository's Shader Graph is not imported.

## Owner-licensed additions

The platform uses Peach_Boba's Hello Kitty LEDs under the Personal license, and the carriages use a quiet Drop Sohila train-interior loop controlled by the Atmosphere slider. Raw models, textures and audio stay outside public source: GitHub builds restore an authenticated encrypted asset pack using the owner's repository secret. The original packages and license notices are retained privately.

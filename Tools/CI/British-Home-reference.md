# British Home optimization reference

**British Home** is Tioaoa's VRChat world `wrld_44ef91fd-d2d3-4e47-80c9-13bdbcfc28b9`. The public listing reported revision 128 at inspection. [Official world listing](https://vrchat.com/home/world/wrld_44ef91fd-d2d3-4e47-80c9-13bdbcfc28b9)

## Local measurements

The matching client file, relative to the VRChat data folder under `AppData/LocalLow/VRChat/VRChat`, was `Cache-WindowsPlayer/FD0BDC6622D15E8A/f567e26a000000000000000078000000/__data`.

| Observation | Evidence |
| --- | --- |
| Windows download | The VRChat client logged a 52.0 MB transfer and named the world ID when unpacking it. |
| Cached representation | The Windows UnityFS cache entry updated in the same second. Its 105,745,308-byte file indexes a 105,516,256-byte shared asset stream and a 424,944-byte scene stream named `BuildPlayer-VRChat_Build_V2`. These sizes describe stored data. GPU memory is a separate measurement. |
| Performance on this PC | Three one-player client reports during visits averaged 89.3, 90.0 and 68.2 FPS over roughly 93, 60 and 146 seconds. The reports include client work and transitions around the visits. They provide no isolated world GPU cost or Quest measurement. |

The cache exposes the bundle directory. Its protected object data leaves the current mesh, material, lightmap, LOD and draw-call counts unverified. The local inspection used read-only metadata and existing logs.

## How the creator built the lighting

Tioaoa described using Unity's built-in lightmapper, laying out lightmap UVs by hand and using 2K lightmaps for surfaces players can approach closely. The creator also described building diffuse, normal and roughness textures and checking each asset under the baked lighting. These are accounts of the world's development. [Lighting bake](https://www.reddit.com/r/VRchat/comments/siz2f1/comment/hvbzchn/) · [Lightmap resolution](https://www.reddit.com/r/VRchat/comments/siz2f1/comment/hvckg3w/) · [Asset workflow](https://www.reddit.com/r/VRchat/comments/siz2f1/hiya_i_wanted_to_share_my_world_it_has_been_in/)

For **liminal uwu**, the train doorway, armrests and wet platform are the places to spend close-up surface detail. Capturing a stationary room's reflection during authoring avoids a reflection camera rendering another view each frame. The Quest scene can share the same seat positions and collision surfaces while using cheaper reflections. The project uses its own authored scenes and separately sourced surface materials. The British Home cache contributed measurements only.

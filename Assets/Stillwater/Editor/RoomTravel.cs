using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UdonSharpEditor;

internal static class RoomTravel
{
    internal static void Install(Transform root, Transform spawn, bool quest)
    {
        var journey = root.GetComponentInChildren<AdventureJourney>();
        var rooms = DistrictSystems.SectorRoots(root);
        var belts = new List<Transform>();
        journey.roomTracker = root.GetComponentInChildren<DistrictVisibility>();
        journey.roomCheckpoints = new Transform[rooms.Count];
        journey.rememberEntry = new bool[rooms.Count];
        string path = ObservatoryPrototype.Folder + "/Walkway " + (quest ? "Quest" : "PC") + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) {
            mat = new Material(Shader.Find("Liminal uwu/Moving walkway"));
            AssetDatabase.CreateAsset(mat,path);
        }
        Physics.SyncTransforms();
        for (int i = 0; i < rooms.Count; i++) {
            Transform room = rooms[i];
            if (room.name.StartsWith("Passage ")) continue;
            var arrival = room == root.Find("hub") ? spawn : journey.destinations.FirstOrDefault(t => t.parent == room);
            journey.roomCheckpoints[i] = arrival;
            journey.rememberEntry[i] = !arrival;
            var exit = journey.entrances.FirstOrDefault(t => t.parent == room);
            if (!arrival || !exit) continue;
            // Find clear, continuously supported stretches alongside the route.
            // Leave the arrival and the portal landing free for stopping and turning.
            Vector3 start = arrival.position, end = exit.position;
            Vector3 direction = Vector3.ProjectOnPlane(end-start, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            foreach (int lane in new[]{-1,1}) {
                int first = -1, last = Mathf.FloorToInt(Vector3.Dot(end-start,direction)) - 3;
                for (int m = 3; m <= last + 1; m++) {
                    Vector3 at = start + direction*m + side*(lane*2.6f);
                    bool clear = m <= last;
                    foreach (float offset in new[]{-.76f,0,.76f}) {
                        if (!clear) break;
                        Vector3 sample = at + side*offset;
                        clear = Physics.Raycast(sample+Vector3.up*.55f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore)
                            && Mathf.Abs(hit.point.y-room.position.y)<.04f && hit.normal.y>.96f;
                    }
                    at.y = room.position.y;
                    if (clear) clear = !Physics.CheckBox(at+Vector3.up*1.17f,new Vector3(.85f,1.05f,.51f),
                        Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore);
                    if (clear) { if (first<0) first=m; continue; }
                    if (first>=0 && m-first>=5) {
                        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        strip.name = lane>0 ? "Express walkway" : "Return walkway";
                        strip.transform.SetParent(room,true);
                        var center=start+direction*((first+m-1)*.5f)+side*(lane*2.6f);
                        center.y=room.position.y+.035f;
                        strip.transform.position=center;
                        strip.transform.rotation=Quaternion.LookRotation(direction*lane);
                        strip.transform.localScale=new Vector3(1.6f,.07f,m-first);
                        strip.GetComponent<MeshRenderer>().sharedMaterial=mat;
                        belts.Add(strip.transform);
                        Physics.SyncTransforms();
                    }
                    first=-1;
                }
            }
        }
        journey.belts=belts.ToArray();
        if (belts.Count==0) throw new System.InvalidOperationException("No clear moving walkways found");
        UdonSharpEditorUtility.CopyProxyToUdon(journey);
    }
}

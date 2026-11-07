using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Only rendering is gated. Colliders, trains, networking and portable controls stay alive.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DistrictVisibility : UdonSharpBehaviour
{
    public Vector3[] zoneMin, zoneMax;
    public Renderer[] renderers;
    public int[] rendererSector;
    public int[] portalA, portalB;
    public Vector3[] portalCenter;
    public float[] portalRadius;
    private bool[] visible;
    private bool[] lastVisible;
    private int[] depth;
    private int[] queue;
    public int currentSector = -1;

    private void Start()
    {
        visible = new bool[zoneMin.Length];
        lastVisible = new bool[zoneMin.Length];
        depth = new int[zoneMin.Length];
        queue = new int[zoneMin.Length];
        for(int i=0;i<lastVisible.Length;i++) lastVisible[i]=true;
        ReviewVisibility();
    }
    public void ReviewVisibility()
    {
        var player=Networking.LocalPlayer;
        if(Utilities.IsValid(player)) {
            Vector3 p=player.GetPosition()+Vector3.up*.1f;
            var head=player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 eye=head.position, forward=head.rotation*Vector3.forward;
            currentSector=-1;
            for(int i=0;i<zoneMin.Length;i++) {
                Vector3 a=zoneMin[i],b=zoneMax[i];
                if(p.x>=a.x && p.x<=b.x && p.y>=a.y && p.y<=b.y && p.z>=a.z && p.z<=b.z) {currentSector=i;break;}
            }
            for(int i=0;i<visible.Length;i++) {visible[i]=false;depth[i]=0;}
            // Outside authored bounds (flight, respawn or debug): fail open.
            if(currentSector<0) {
                for(int i=0;i<visible.Length;i++)visible[i]=true;
            } else {
                int read=0,write=1;queue[0]=currentSector;visible[currentSector]=true;
                while(read<write) {
                    int from=queue[read++];
                    if(depth[from]>=5)continue;
                    for(int link=0;link<portalA.Length;link++) {
                        int to=-1;
                        if(portalA[link]==from)to=portalB[link];
                        else if(portalB[link]==from)to=portalA[link];
                        if(to<0 || visible[to])continue;
                        Vector3 ray=portalCenter[link]-eye;
                        float distance=ray.magnitude;
                        // Wide angular margin avoids pop-in at the edge of VR lenses.
                        // Unity's baked occlusion and per-camera frustum finish the cull.
                        if(distance>portalRadius[link]+2 && Vector3.Dot(forward,ray/distance)<-.35f-portalRadius[link]/distance)continue;
                        // Beyond the immediate neighbour, require an unobstructed doorway.
                        // Layers 0/11 are architecture/environment; triggers are ignored.
                        if(depth[from]>0 && distance>portalRadius[link]+2 && Physics.Linecast(eye,portalCenter[link],(1<<0)|(1<<11),QueryTriggerInteraction.Ignore))continue;
                        visible[to]=true;depth[to]=depth[from]+1;queue[write++]=to;
                    }
                }
            }
            for(int i=0;i<renderers.Length;i++) {
                int sector=rendererSector[i];
                if(visible[sector]!=lastVisible[sector] && Utilities.IsValid(renderers[i]))renderers[i].enabled=visible[sector];
            }
            for(int i=0;i<visible.Length;i++)lastVisible[i]=visible[i];
        }
        SendCustomEventDelayedSeconds(nameof(ReviewVisibility), .15f);
    }
    private void OnDisable()
    {
        if(renderers==null)return;
        for(int i=0;i<renderers.Length;i++)if(Utilities.IsValid(renderers[i]))renderers[i].enabled=true;
    }
}

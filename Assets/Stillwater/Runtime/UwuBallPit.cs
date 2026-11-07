using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Deliberately local toys: no pickups, ownership churn or per-ball Udon updates.
// Analytic contacts move the balls away from player capsules; players are never
// pushed, teleported or given a different locomotion speed by this room.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UwuBallPit : UdonSharpBehaviour
{
    public Transform room;
    public Transform[] balls;
    public float[] radii;
    public Vector3[] starts;
    public Vector3[] obstacleCenters, obstacleHalfSizes;
    public Vector3 halfExtent = new Vector3(28, 20, 30);
    [HideInInspector] public Vector3[] positions, velocities;
    [HideInInspector] public int playerBounces, boundaryBounces, pairBounces, simulationSteps;
    [HideInInspector] public bool simulationActive;
    private Vector3[] previous;
    private VRCPlayerApi[] players = new VRCPlayerApi[80];
    private Vector3[] feet = new Vector3[80], playerVelocity = new Vector3[80];
    private float[] heights = new float[80];
    private int occupants, playerCount;
    private float clock, nextRoster;
    private const float Step = .05f;

    private void Start() { ResetBalls(); }
    public void ResetBalls()
    {
        if (balls == null || starts == null || radii == null || balls.Length != starts.Length || balls.Length != radii.Length) return;
        positions = new Vector3[balls.Length]; previous = new Vector3[balls.Length]; velocities = new Vector3[balls.Length];
        for (int i = 0; i < balls.Length; i++) {
            positions[i] = starts[i]; previous[i] = starts[i];
            velocities[i] = new Vector3(((i * 7) % 9 - 4) * .53f, 1.1f + (i % 4) * .4f, ((i * 5) % 11 - 5) * .43f);
            if (Utilities.IsValid(balls[i])) balls[i].localPosition = starts[i];
        }
        clock = 0; nextRoster = 0;
    }
    private void Update()
    {
        if (!Utilities.IsValid(room) || positions == null) return;
        var local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local)) return;
        Vector3 p = room.InverseTransformPoint(local.GetPosition());
        simulationActive = Mathf.Abs(p.x) < 36 && p.y > -5 && p.y < 36 && Mathf.Abs(p.z) < 40;
        if (!simulationActive) { clock = 0; return; }
        clock += Mathf.Min(Time.deltaTime, Step);
        // At most one fixed step per frame: never spend a hitch catching up an
        // invisible physics backlog. Rendering interpolates the 20 Hz positions.
        if (clock >= Step) { clock -= Step; SimulateStep(); }
        float blend = Mathf.Clamp01(clock / Step);
        for (int i = 0; i < balls.Length; i++) {
            if (!Utilities.IsValid(balls[i])) continue;
            balls[i].localPosition = Vector3.Lerp(previous[i], positions[i], blend);
            balls[i].Rotate(new Vector3(velocities[i].z, .35f, -velocities[i].x) * (Time.deltaTime * 12f), Space.World);
        }
    }
    private void SamplePlayers()
    {
        if (Time.time >= nextRoster) {
            playerCount = Mathf.Min(players.Length, VRCPlayerApi.GetPlayerCount());
            VRCPlayerApi.GetPlayers(players); nextRoster = Time.time + .5f;
        }
        occupants = 0;
        for (int i = 0; i < playerCount; i++) {
            var player = players[i];
            if (!Utilities.IsValid(player)) continue;
            Vector3 p = room.InverseTransformPoint(player.GetPosition());
            if (Mathf.Abs(p.x) > 30 || p.y < -3 || p.y > 25 || Mathf.Abs(p.z) > 32) continue;
            feet[occupants] = p;
            playerVelocity[occupants] = Vector3.ClampMagnitude(room.InverseTransformDirection(player.GetVelocity()), 8);
            // Eye-height querying is available for local and remote players. It
            // handles tiny/tall avatars without needing a humanoid bone mapping.
            heights[occupants] = Mathf.Clamp(player.GetAvatarEyeHeightAsMeters(), .4f, 4f);
            occupants++;
        }
    }
    public void SimulateStep()
    {
        if (!Utilities.IsValid(room) || positions == null) return;
        SamplePlayers(); simulationSteps++;
        for (int i = 0; i < positions.Length; i++) {
            previous[i] = positions[i];
            Vector3 v = velocities[i]; v.y -= 5.2f * Step;
            v.x *= .998f; v.z *= .998f;
            velocities[i] = Vector3.ClampMagnitude(v, 8);
            positions[i] += velocities[i] * Step;
            Contain(i);
            CollideFurniture(i);
            CollidePlayers(i);
        }
        for (int i = 0; i < positions.Length; i++) for (int j = i + 1; j < positions.Length; j++) {
            Vector3 difference = positions[i] - positions[j];
            float combined = radii[i] + radii[j], d2 = difference.sqrMagnitude;
            if (d2 >= combined * combined) continue;
            float d = Mathf.Sqrt(Mathf.Max(d2, .00001f));
            Vector3 normal = d2 < .00001f ? Vector3.right : difference / d;
            Vector3 push = normal * ((combined - d) * .5f + .002f);
            positions[i] += push; positions[j] -= push;
            float closing = Vector3.Dot(velocities[i] - velocities[j], normal);
            if (closing < 0) {
                Vector3 impulse = normal * (closing * .9f);
                velocities[i] -= impulse; velocities[j] += impulse; pairBounces++;
            }
        }
        // Pair correction cannot push a toy through the room boundary.
        for (int i = 0; i < positions.Length; i++) Contain(i);
    }
    private void Contain(int i)
    {
        Vector3 p = positions[i], v = velocities[i]; float r = radii[i];
        if (p.y < r) { p.y = r; v.y = Mathf.Max(3.2f, Mathf.Abs(v.y) * .87f); boundaryBounces++; }
        if (p.y > halfExtent.y - r) { p.y = halfExtent.y - r; v.y = -Mathf.Abs(v.y) * .88f; boundaryBounces++; }
        if (Mathf.Abs(p.x) > halfExtent.x - r) { p.x = Mathf.Sign(p.x) * (halfExtent.x - r); if (v.x * p.x > 0) v.x *= -.92f; boundaryBounces++; }
        if (Mathf.Abs(p.z) > halfExtent.z - r) { p.z = Mathf.Sign(p.z) * (halfExtent.z - r); if (v.z * p.z > 0) v.z *= -.92f; boundaryBounces++; }
        positions[i] = p; velocities[i] = Vector3.ClampMagnitude(v, 8);
    }
    private void CollidePlayers(int i)
    {
        for (int n = 0; n < occupants; n++) {
            Vector3 p = positions[i], basePoint = feet[n];
            Vector3 nearest = basePoint;
            nearest.y = Mathf.Clamp(p.y, basePoint.y + .22f, basePoint.y + Mathf.Max(.24f, heights[n] - .12f));
            Vector3 delta = p - nearest;
            float combined = radii[i] + .27f, d2 = delta.sqrMagnitude;
            if (d2 >= combined * combined) continue;
            float d = Mathf.Sqrt(Mathf.Max(d2, .00001f));
            Vector3 normal = d2 < .00001f ? Vector3.right : delta / d;
            positions[i] = nearest + normal * (combined + .015f);
            Vector3 relative = velocities[i] - playerVelocity[n];
            float closing = Vector3.Dot(relative, normal);
            if (closing < 0) relative -= normal * (closing * 1.85f);
            // A small outward separation also resolves a player walking into a
            // sleeping or almost tangential ball without repeated visual overlap.
            velocities[i] = Vector3.ClampMagnitude(relative + playerVelocity[n] + normal * .35f, 8);
            playerBounces++;
        }
    }
    private void CollideFurniture(int i)
    {
        if (obstacleCenters == null || obstacleHalfSizes == null) return;
        for (int j = 0; j < obstacleCenters.Length; j++) {
            Vector3 p = positions[i], c = obstacleCenters[j], h = obstacleHalfSizes[j];
            Vector3 relative = p - c;
            Vector3 nearest = new Vector3(Mathf.Clamp(relative.x, -h.x, h.x), Mathf.Clamp(relative.y, -h.y, h.y), Mathf.Clamp(relative.z, -h.z, h.z));
            Vector3 delta = relative - nearest; float d2 = delta.sqrMagnitude, r = radii[i];
            if (d2 >= r * r) continue;
            Vector3 normal;
            float penetration;
            if (d2 > .00001f) {
                float d = Mathf.Sqrt(d2); normal = delta / d; penetration = r - d;
            } else {
                Vector3 depths = h - new Vector3(Mathf.Abs(relative.x), Mathf.Abs(relative.y), Mathf.Abs(relative.z));
                if (depths.x < depths.y && depths.x < depths.z) { normal = relative.x < 0 ? Vector3.left : Vector3.right; penetration = r + depths.x; }
                else if (depths.y < depths.z) { normal = relative.y < 0 ? Vector3.down : Vector3.up; penetration = r + depths.y; }
                else { normal = relative.z < 0 ? Vector3.back : Vector3.forward; penetration = r + depths.z; }
            }
            positions[i] += normal * (penetration + .005f);
            float closing = Vector3.Dot(velocities[i], normal);
            if (closing < 0) velocities[i] -= normal * (closing * 1.85f);
        }
    }
}

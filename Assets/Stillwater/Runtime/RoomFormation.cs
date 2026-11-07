using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Visual architecture only. The walkable foundation and perimeter never move.
// Each visitor sees their own slow, reversible reconstruction around them.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class RoomFormation : UdonSharpBehaviour
{
    public Transform space;
    public Transform[] pieces;
    public Vector3[] assembledPositions, scatteredPositions;
    public Quaternion[] assembledRotations, scatteredRotations;
    public Vector3[] influencePoints;
    public float nearDistance = 18f, farDistance = 24f;
    public float assembleSeconds = 5.5f, scatterSeconds = 7f;
    [HideInInspector] public float[] amounts;
    [HideInInspector] public bool[] forming;
    [HideInInspector] public bool initialized;
    [HideInInspector] public int movingPieces;
    private float lastTick;

    private void Start()
    {
        InitializeFormation();
        TickFormation();
    }

    public void InitializeFormation()
    {
        if (initialized || pieces == null || !Utilities.IsValid(space)) return;
        int count = pieces.Length;
        if (assembledPositions == null || scatteredPositions == null || assembledRotations == null ||
            scatteredRotations == null || influencePoints == null || assembledPositions.Length != count ||
            scatteredPositions.Length != count || assembledRotations.Length != count ||
            scatteredRotations.Length != count || influencePoints.Length != count) return;
        amounts = new float[count];
        forming = new bool[count];
        for (int i = 0; i < count; i++)
            if (Utilities.IsValid(pieces[i])) {
                pieces[i].localPosition = scatteredPositions[i];
                pieces[i].localRotation = scatteredRotations[i];
            }
        lastTick = Time.time;
        initialized = true;
    }

    // Also callable by the hosted review, without starting a second timer loop.
    public void ReviewFormation()
    {
        if (!initialized) InitializeFormation();
        if (!initialized) return;
        var player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        Vector3 local = space.InverseTransformPoint(player.GetPosition());
        float delta = Mathf.Clamp(Time.time - lastTick, 0f, .25f);
        lastTick = Time.time;
        movingPieces = 0;
        for (int i = 0; i < pieces.Length; i++) {
            if (!Utilities.IsValid(pieces[i])) continue;
            // The projected anchor makes a high roof rib react to walking below it.
            Vector3 difference = local - influencePoints[i];
            float distance = difference.magnitude;
            if (distance <= nearDistance) forming[i] = true;
            else if (distance >= farDistance) forming[i] = false;
            float target = forming[i] ? 1f : 0f;
            float old = amounts[i];
            float duration = forming[i] ? assembleSeconds : scatterSeconds;
            amounts[i] = Mathf.MoveTowards(old, target, delta / Mathf.Max(1f, duration));
            if (Mathf.Abs(amounts[i] - old) < .00001f) continue;
            movingPieces++;
            float eased = Mathf.SmoothStep(0f, 1f, amounts[i]);
            pieces[i].localPosition = Vector3.Lerp(scatteredPositions[i], assembledPositions[i], eased);
            pieces[i].localRotation = Quaternion.Slerp(scatteredRotations[i], assembledRotations[i], eased);
        }
    }

    public void TickFormation()
    {
        ReviewFormation();
        // Nearby interpolation is smooth; an idle room wakes four times a second.
        SendCustomEventDelayedSeconds(nameof(TickFormation), movingPieces > 0 ? .05f : .25f);
    }
}

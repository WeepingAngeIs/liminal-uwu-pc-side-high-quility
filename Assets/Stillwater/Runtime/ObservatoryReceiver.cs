using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Persistence;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ObservatoryReceiver : UdonSharpBehaviour
{
    public Text receiverDisplay, clockDisplay;
    public Transform penNeedle, feedWheel, clockHand, serviceHand, pendulum;
    public GameObject secondSpool;

    private const string SaveKey = "liminal.observatory.receiver.v1";
    private const int SpoolCost = 6;
    private const int MaximumReadings = 100000000;
    private bool initialized, gotRestore, ready, hasSpool, hasClock;
    private int lifetime;
    private float baseSeconds, spoolSeconds, lastSample, lastSave, nextUpdate, messageUntil, strokeStart;
    private int strokeSteps;
    private bool nearDesk, nearClock;
    private string message = "";
    private Quaternion penRest, feedRest, clockRest, serviceRest, pendulumRest;
    private Canvas deskCanvas, clockCanvas;

    private void Start() { Initialize(); }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (penNeedle) penRest = penNeedle.localRotation;
        if (feedWheel) feedRest = feedWheel.localRotation;
        if (clockHand) clockRest = clockHand.localRotation;
        if (serviceHand) serviceRest = serviceHand.localRotation;
        if (pendulum) pendulumRest = pendulum.localRotation;
        if (receiverDisplay) deskCanvas = receiverDisplay.GetComponentInParent<Canvas>();
        if (clockDisplay) clockCanvas = clockDisplay.GetComponentInParent<Canvas>();
        ShowMechanisms();
        SetDisplay("Tuning receiver...", "Tuning receiver...");
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal || gotRestore) return;
        Initialize();
        gotRestore = true;
        if (PlayerData.HasKey(player, SaveKey)) {
            string record;
            if (!PlayerData.TryGetString(player, SaveKey, out record) || !ReadRecord(record)) {
                SetDisplay("Receiver paused\nUnreadable record", "Receiver paused\nUnreadable record");
                return;
            }
        }
        ready = true;
        lastSample = Time.realtimeSinceStartup;
        lastSave = lastSample;
        ShowMechanisms();
        RefreshDisplay();
    }

    private bool ReadRecord(string record)
    {
        // Version 1 stores lifetime readings, the two partial seconds and the two flags.
        // Spendable readings are derived because this receiver has one permanent purchase.
        if (string.IsNullOrEmpty(record) || record.Length > 50) return false;
        string[] fields = record.Split(new char[] { '|' });
        if (fields.Length != 5) return false;
        int total, basePhase, spoolPhase;
        if (!int.TryParse(fields[0], out total) || !int.TryParse(fields[1], out basePhase) ||
            !int.TryParse(fields[2], out spoolPhase)) return false;
        if (total < 0 || total > MaximumReadings || basePhase < 0 || basePhase >= 30 ||
            spoolPhase < 0 || spoolPhase >= 60) return false;
        if ((fields[3] != "0" && fields[3] != "1") || (fields[4] != "0" && fields[4] != "1"))
            return false;
        if ((fields[3] == "1" && total < SpoolCost) || (fields[3] == "0" && spoolPhase != 0))
            return false;
        lifetime = total;
        baseSeconds = basePhase;
        spoolSeconds = spoolPhase;
        hasSpool = fields[3] == "1";
        hasClock = fields[4] == "1";
        return true;
    }

    private void Update()
    {
        if (!initialized) return;
        float now = Time.realtimeSinceStartup;
        if (now >= nextUpdate) {
            nextUpdate = now + .5f;
            var local = Networking.LocalPlayer;
            if (Utilities.IsValid(local)) {
                Vector3 position = local.GetPosition();
                nearDesk = penNeedle && (position - penNeedle.position).sqrMagnitude < 400f;
                nearClock = serviceHand && (position - serviceHand.position).sqrMagnitude < 625f;
                if (deskCanvas && deskCanvas.enabled != nearDesk) deskCanvas.enabled = nearDesk;
                if (clockCanvas && clockCanvas.enabled != nearClock) clockCanvas.enabled = nearClock;
            }
            if (ready) {
                bool earned = Accrue(now);
                if (earned || now - lastSave >= 15f) Save(now);
                if (message != "" && now >= messageUntil) { message = ""; RefreshDisplay(); }
                else if (earned) RefreshDisplay();
            }
        }
        if (!ready) return;
        if (nearDesk) {
            float stroke = Mathf.Clamp01((now - strokeStart) / 1.2f);
            float pen = 3f * Mathf.Sin(now * 1.4f);
            if (strokeSteps > 0) pen -= 16f * Mathf.Sin(stroke * Mathf.PI);
            penNeedle.localRotation = penRest * Quaternion.Euler(0f, 0f, pen);
            if (feedWheel) {
                float steps = lifetime % 16 - strokeSteps * (1f - Mathf.SmoothStep(0f, 1f, stroke));
                feedWheel.localRotation = feedRest * Quaternion.Euler(0f, 0f, -22.5f * steps);
            }
            if (hasClock && pendulum)
                pendulum.localRotation = pendulumRest * Quaternion.Euler(0f, 0f, 18f * Mathf.Sin(now * Mathf.PI));
        }
        if (nearClock) {
            // The last tick is silent: both dials hold their penultimate mark, then jump together.
            int tick = Mathf.Min(10, Mathf.FloorToInt(now % 12f));
            Quaternion angle = Quaternion.Euler(0f, 0f, -30f * tick);
            if (clockHand) clockHand.localRotation = clockRest * angle;
            serviceHand.localRotation = serviceRest * angle;
        }
    }

    private bool Accrue(float now)
    {
        float elapsed = Mathf.Max(0f, now - lastSample);
        lastSample = now;
        baseSeconds += elapsed;
        int regular = Mathf.FloorToInt(baseSeconds / 30f);
        baseSeconds -= 30f * regular;
        int extra = 0;
        if (hasSpool) {
            spoolSeconds += elapsed;
            extra = Mathf.FloorToInt(spoolSeconds / 60f);
            spoolSeconds -= 60f * extra;
        }
        int newTotal = Mathf.Min(MaximumReadings, lifetime + regular + extra);
        if (newTotal == lifetime) return false;
        strokeStart = now;
        strokeSteps = Mathf.Min(2, newTotal - lifetime);
        lifetime = newTotal;
        return true;
    }

    private void Save(float now)
    {
        string record = lifetime + "|" + Mathf.FloorToInt(baseSeconds) + "|" + Mathf.FloorToInt(spoolSeconds) +
                        "|" + (hasSpool ? "1" : "0") + "|" + (hasClock ? "1" : "0");
        PlayerData.SetString(SaveKey, record);
        lastSave = now;
    }

    public void _FitSpool()
    {
        if (!ready || NetworkCalling.InNetworkCall) return;
        float now = Time.realtimeSinceStartup;
        bool earned = Accrue(now);
        if (hasSpool) Notify("Spool already fitted", now);
        else if (lifetime < SpoolCost) Notify("Need " + (SpoolCost - lifetime) + " more", now);
        else {
            hasSpool = true;
            ShowMechanisms();
            Save(now);
            Notify("Second spool fitted", now);
            return;
        }
        if (earned) Save(now);
    }

    public void _RecordClock()
    {
        if (!ready || NetworkCalling.InNetworkCall) return;
        if (hasClock) return;
        float now = Time.realtimeSinceStartup;
        Accrue(now);
        hasClock = true;
        ShowMechanisms();
        Save(now);
        Notify("Pendulum installed", now);
    }

    private void Notify(string value, float now)
    {
        message = value;
        messageUntil = now + 5f;
        RefreshDisplay();
    }

    private void ShowMechanisms()
    {
        if (secondSpool) secondSpool.SetActive(hasSpool);
        if (pendulum) pendulum.gameObject.SetActive(hasClock);
    }

    private void SetDisplay(string desk, string clock)
    {
        if (receiverDisplay) receiverDisplay.text = desk;
        if (clockDisplay) clockDisplay.text = clock;
    }

    private void RefreshDisplay()
    {
        int balance = lifetime - (hasSpool ? SpoolCost : 0);
        string desk = balance + (balance == 1 ? " reading\n" : " readings\n") + (hasSpool ? "3" : "2") + " / minute\n" +
                      (message != "" ? message : (hasSpool ? "Spool fitted" : "Fit spool for 6"));
        SetDisplay(desk, hasClock ? "Clock recorded\nPendulum installed" : "One beat missing\nRecord the clock");
    }
}

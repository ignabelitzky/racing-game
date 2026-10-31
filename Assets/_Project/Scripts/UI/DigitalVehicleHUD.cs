using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact vehicle instruments, driven by the controller's speed and engine RPM.</summary>
public sealed class DigitalVehicleHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI rpmText;
    [SerializeField] private TextMeshProUGUI gearText;
    [SerializeField] private TextMeshProUGUI driveText;
    [SerializeField] private Image[] rpmSegments;
    [Header("Auxiliary systems")]
    [SerializeField] private RectTransform boostFill;
    [SerializeField] private Image boostFillImage;
    [SerializeField] private TextMeshProUGUI boostPercentText, boostStateText;
    [SerializeField] private VehicleStatusIcon lightIcon, handbrakeIcon, stealthIcon;
    [SerializeField] private TextMeshProUGUI lightStateText, handbrakeStateText, stealthStateText;
    [SerializeField] private Color inactiveColor = new Color(.16f, .19f, .20f, 1);
    private readonly Color green = new Color(.36f, .81f, .55f);
    private readonly Color yellow = new Color(.91f, .81f, .35f);
    private readonly Color orange = new Color(.95f, .54f, .23f);
    private readonly Color red = new Color(.95f, .25f, .25f);
    private Rect safeArea;
    private Vector2 screenSize;
    private Canvas canvas;

    private void Awake() => canvas = GetComponentInParent<Canvas>();

    public void RefreshSystems(float reserve, bool boosting, bool recharging, bool airborne,
        TumblerSystems.LightMode lights, bool handbrake, bool stealth)
    {
        if (boostFill == null) return;
        boostFill.anchorMax = new Vector2(Mathf.Clamp01(reserve), 1);
        boostFillImage.color = boosting ? new Color(1f,.67f,.27f) :
            recharging ? (airborne ? new Color(.32f,.85f,.90f) : new Color(.42f,.75f,.65f)) : new Color(.69f,.57f,.37f);
        boostPercentText.SetText("{0:0}%", Mathf.Clamp01(reserve) * 100);
        boostStateText.text = recharging ? "RECHARGE" : "BOOST";
        lightIcon.SetSymbol(lights == TumblerSystems.LightMode.High ? VehicleStatusIcon.Symbol.HighBeam :
            lights == TumblerSystems.LightMode.Low ? VehicleStatusIcon.Symbol.LowBeam : VehicleStatusIcon.Symbol.LightsOff);
        Color dim = new Color(.39f,.45f,.46f);
        lightIcon.color = lights == TumblerSystems.LightMode.High ? new Color(.36f,.72f,1f) :
            lights == TumblerSystems.LightMode.Low ? new Color(.47f,.86f,.61f) : dim;
        lightStateText.text = lights == TumblerSystems.LightMode.High ? "HIGH" : lights == TumblerSystems.LightMode.Low ? "LOW" : "OFF";
        lightStateText.color = lightIcon.color;
        handbrakeIcon.SetSymbol(VehicleStatusIcon.Symbol.Handbrake);
        handbrakeIcon.color = handbrake ? new Color(1,.39f,.20f) : dim;
        handbrakeStateText.text = handbrake ? "ON" : "OFF"; handbrakeStateText.color = handbrakeIcon.color;
        stealthIcon.SetSymbol(VehicleStatusIcon.Symbol.Bat);
        stealthIcon.color = stealth ? new Color(.96f,.76f,.37f) : dim;
        stealthStateText.text = stealth ? "ON" : "OFF"; stealthStateText.color = stealthIcon.color;
    }

    public Color RPMColor(float normalized)
    {
        if (normalized < .55f) return Color.Lerp(green, yellow, normalized / .55f);
        if (normalized < .8f) return Color.Lerp(yellow, orange, (normalized - .55f) / .25f);
        return Color.Lerp(orange, red, (normalized - .8f) / .2f);
    }

    public void Refresh(float speed, float rpm, float redline, int gear, bool reverse, TumblerController.driveType drive)
    {
        speedText.SetText("{0:0}", Mathf.Max(0, speed));
        rpmText.SetText("{0:0}", Mathf.Max(0, rpm));
        gearText.text = reverse ? "R" : gear.ToString();
        driveText.text = drive == TumblerController.driveType.FrontWheelDrive ? "FWD" :
            drive == TumblerController.driveType.RearWheelDrive ? "RWD" : "AWD";
        float normalized = Mathf.Clamp01(rpm / Mathf.Max(1, redline));
        rpmText.color = RPMColor(normalized);
        int active = Mathf.CeilToInt(normalized * rpmSegments.Length);
        for (int i = 0; i < rpmSegments.Length; i++)
            rpmSegments[i].color = i < active ? RPMColor((float)i / Mathf.Max(1, rpmSegments.Length - 1)) : inactiveColor;
        UpdateSafeArea();
    }

    private void UpdateSafeArea()
    {
        Vector2 size = new Vector2(Screen.width, Screen.height);
        if (safeArea == Screen.safeArea && screenSize == size) return;
        safeArea = Screen.safeArea; screenSize = size;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
        ((RectTransform)transform).anchoredPosition = new Vector2(-24 - (Screen.width - safeArea.xMax) / scale, 24 + safeArea.yMin / scale);
    }
}

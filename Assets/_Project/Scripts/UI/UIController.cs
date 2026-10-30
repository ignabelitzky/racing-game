using UnityEngine;

public class UIController : MonoBehaviour
{
    [SerializeField] private TumblerController tumblerController;
    [SerializeField] private DigitalVehicleHUD hud;

    private void Awake()
    {
        if (tumblerController == null) tumblerController = FindFirstObjectByType<TumblerController>();
        if (hud == null) hud = FindFirstObjectByType<DigitalVehicleHUD>();
    }

    private void LateUpdate() => Refresh();
    public void UpdateDriveMode() => Refresh();
    public void UpdateGearNum() => Refresh();

    private void Refresh()
    {
        if (tumblerController == null || hud == null) return;
        hud.Refresh(tumblerController.GetSpeed(), tumblerController.GetEngineRPM(), tumblerController.GetRedlineRPM(),
            tumblerController.currentGear, tumblerController.IsReversing, tumblerController.currentDriveMode);
    }
}

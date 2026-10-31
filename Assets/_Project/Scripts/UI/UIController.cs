using UnityEngine;

public class UIController : MonoBehaviour
{
    [SerializeField] private TumblerController tumblerController;
    [SerializeField] private DigitalVehicleHUD hud;
    private TumblerSystems systems;
    private Boost boost;

    private void Awake()
    {
        if (tumblerController == null) tumblerController = FindFirstObjectByType<TumblerController>();
        if (hud == null) hud = FindFirstObjectByType<DigitalVehicleHUD>();
        if (tumblerController != null) { systems = tumblerController.GetComponent<TumblerSystems>(); boost = tumblerController.GetComponent<Boost>(); }
    }

    private void LateUpdate() => Refresh();
    public void UpdateDriveMode() => Refresh();
    public void UpdateGearNum() => Refresh();

    private void Refresh()
    {
        if (tumblerController == null || hud == null) return;
        hud.Refresh(tumblerController.GetSpeed(), tumblerController.GetEngineRPM(), tumblerController.GetRedlineRPM(),
            tumblerController.currentGear, tumblerController.IsReversing, tumblerController.currentDriveMode);
        if (systems != null && boost != null)
            hud.RefreshSystems(boost.Reserve01, boost.IsBoostActive, boost.IsRecharging, !tumblerController.IsGrounded(),
                systems.EffectiveLights, tumblerController.IsHandbraking(), systems.IsStealth);
    }
}

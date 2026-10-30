using UnityEngine;
using TMPro;

public class UIController : MonoBehaviour
{
    [HideInInspector] private TumblerController tumblerController;
    [HideInInspector] private TextMeshProUGUI driveModeText;
    [HideInInspector] private TextMeshProUGUI gearNumText;

    private void Awake()
    {
        tumblerController = FindFirstObjectByType<TumblerController>();
        driveModeText = GameObject.Find("DriveModeText").GetComponent<TextMeshProUGUI>();
        gearNumText = GameObject.Find("GearNumText").GetComponent<TextMeshProUGUI>();
        if(!driveModeText)
        {
            Debug.LogError("DriveModeText not found in the scene. Please assign the DriveModeText GameObject in the Inspector.");
        }
        if(!gearNumText)
        {
            Debug.LogError("GearNumText not found in the scene. Please assign the GearNumText GameObject in the Inspector.");
        }
        if(!tumblerController)
        {
            Debug.LogError("TumblerController not found in the scene. Please assign the TumblerController GameObject in the Inspector.");
        }
    }

    private void Start()
    {
        UpdateDriveMode();
        UpdateGearNum();
    }
    public void UpdateDriveMode()
    {
        switch(tumblerController.currentDriveMode)
        {
            case TumblerController.driveType.AllWheelDrive:
                driveModeText.text = "AWD";
                break;
            case TumblerController.driveType.FrontWheelDrive:
                driveModeText.text = "FWD";
                break;
            case TumblerController.driveType.RearWheelDrive:
                driveModeText.text = "RWD";
                break;
        }
    }

    public void UpdateGearNum()
    {
        gearNumText.text = tumblerController.currentGear.ToString();
    }
}
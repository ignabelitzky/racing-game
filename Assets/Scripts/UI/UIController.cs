using UnityEngine;
using TMPro;

public class UIController : MonoBehaviour
{
    TumblerController tumblerController;
    [SerializeField] private TextMeshProUGUI driveModeText;
    [SerializeField] private TextMeshProUGUI gearText;

    private void Start()
    {
        tumblerController = FindObjectOfType<TumblerController>();
        if (tumblerController == null)
        {
            Debug.LogError("TumblerController not found in the scene. Please assign the TumblerController GameObject in the Inspector.");
        }
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

    public void UpdateGear()
    {
        gearText.text = tumblerController.currentGear.ToString();
    }
}
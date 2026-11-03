using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class ButtonTrigger : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "GameScene";

    private readonly List<InputDevice> inputDevices = new List<InputDevice>();
    private static bool isLoading;
    private bool wasAnyButtonPressed;

    private void Awake()
    {
        isLoading = false;

        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }
    }

    private void Start()
    {
        wasAnyButtonPressed = IsAnyControllerButtonPressed();
    }

    private void Update()
    {
        if (isLoading)
        {
            return;
        }

        bool isAnyButtonPressed = IsAnyControllerButtonPressed();
        if (isAnyButtonPressed && !wasAnyButtonPressed)
        {
            isLoading = true;
            SceneManager.LoadScene(targetSceneName);
        }

        wasAnyButtonPressed = isAnyButtonPressed;
    }

    private bool IsAnyControllerButtonPressed()
    {
        inputDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, inputDevices);

        for (int i = 0; i < inputDevices.Count; i++)
        {
            InputDevice device = inputDevices[i];
            if (IsPressed(device, CommonUsages.primaryButton)
                || IsPressed(device, CommonUsages.secondaryButton)
                || IsPressed(device, CommonUsages.triggerButton)
                || IsPressed(device, CommonUsages.gripButton)
                || IsPressed(device, CommonUsages.primary2DAxisClick)
                || IsPressed(device, CommonUsages.secondary2DAxisClick)
                || IsPressed(device, CommonUsages.menuButton))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPressed(InputDevice device, InputFeatureUsage<bool> button)
    {
        return device.TryGetFeatureValue(button, out bool pressed) && pressed;
    }
}

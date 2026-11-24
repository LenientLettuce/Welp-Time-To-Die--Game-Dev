using System;
using UnityEngine;
using Unity.Services.Core;
using UnityEngine.UnityConsent;

public class UGS_Analytics : MonoBehaviour
{
    private async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (this != null && isActiveAndEnabled) GiveConsent();
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Unity Analytics initialization failed: {error.Message}");
        }
    }


    public void GiveConsent()
    {
        // Call if consent has been given by the user
        EndUserConsent.SetConsentState(new ConsentState
        {
            AnalyticsIntent = ConsentStatus.Granted,
        });
        Debug.Log($"Consent has been provided. The SDK is now collecting data!");
    }
}

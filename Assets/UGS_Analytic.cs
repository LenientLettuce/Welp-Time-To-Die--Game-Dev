using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Analytics;
using UnityEngine.UnityConsent;




public class UGS_Analytics : MonoBehaviour
{
    async void Start()
    {
            await UnityServices.InitializeAsync();
            GiveConsent(); //Get user consent according to various legislations
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
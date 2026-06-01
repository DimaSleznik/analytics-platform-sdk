using System.Collections.Generic;
using AnalyticsPlatform.Unity;
using UnityEngine;

public sealed class BasicAnalyticsSample : MonoBehaviour
{
    private void Start()
    {
        Analytics.Track("sample_start", new Dictionary<string, object?>
        {
            ["scene"] = gameObject.scene.name,
        });
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Script that allows autopopulation of quality settings on a dropdown and selection
public class QualitySettingsDropdown : MonoBehaviour
{
    Dropdown dropdown;

    void Start()
    {
        dropdown = GetComponent<Dropdown>();
        dropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (string name in QualitySettings.names)
        {
            options.Add(name);
        }
        dropdown.AddOptions(options);
        dropdown.value = QualitySettings.GetQualityLevel();
        dropdown.onValueChanged.AddListener(delegate { ChangeQuality(dropdown.value); });
    }

    public void ChangeQuality(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
    }
}
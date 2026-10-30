using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;

// Language selection manager on the settings screen to display and change languages
public class LanguageSelectionHandler : MonoBehaviour
{
    public Dropdown languageDropdown;
    private SecondaryLocalisationManager localisationManager;

    private void Start()
    {
        localisationManager = SecondaryLocalisationManager.Instance;

        // Populate the dropdown with the languages
        PopulateLanguageDropdown();

        // Add listener for when the language changes
        languageDropdown.onValueChanged.AddListener(delegate {
            LanguageDropdownIndexChanged(languageDropdown);
        });
        UpdateLocalisationFromDropdown();
    }

    // Populating the dropdown with available languages
    void PopulateLanguageDropdown()
    {
        languageDropdown.options.Clear();
        var languageFullNames = new Dictionary<string, string> {
            {"en", "English"},
            {"es", "Español"},
            {"fr", "Français"},
            {"it", "Italiano"}
        };

        languageDropdown.AddOptions(localisationManager.localisationData
            .Select(locData => new Dropdown.OptionData(languageFullNames[locData.languageCode]))
            .ToList());
    }

    // On language change trigger translations
    void LanguageDropdownIndexChanged(Dropdown dropdown)
    {
        string selectedLanguageCode = localisationManager.localisationData[dropdown.value].languageCode;
        localisationManager.UpdateUILocalisation(selectedLanguageCode);
    }

    // Initialise with default english
    void UpdateLocalisationFromDropdown()
    {
        languageDropdown.value = 0;
        languageDropdown.RefreshShownValue();
    }
}
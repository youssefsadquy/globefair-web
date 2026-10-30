using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SecondaryLocalisationData
{
    public string languageCode;
    public List<string> keys;
    public List<string> values;
}

// Script tot ranslate selected fields using localisation for 4 languages
public class SecondaryLocalisationManager : MonoBehaviour
{
    public List<SecondaryLocalisationData> localisationData;
    private Dictionary<string, Dictionary<string, string>> translations;
    public static SecondaryLocalisationManager Instance;

    // Landing Translation
    public Text landingMainTitle;
    public Text landingBusinessAccessTitle;
    public Text landingPrivateAccessTitle;
    public Text landingBusinessAccessButton;
    public Text landingPrivateAccessButton;
    public Text accessTypeTitle;
    public Text accessTypeDescription;
    public Text gotItButton;

    // Settings screen
    public Text settingsTitle;
    public Text settingsLanguageTitle;
    public Text settingsQualityTitle;
    public Text settingsMusicTitle;
    public Text settingsFullscreenTitle;
    public Text settingsClose;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
        BuildDictionaries();
    }

    // Method to add languages and translations
    void BuildDictionaries()
    {
        translations = new Dictionary<string, Dictionary<string, string>>();
        foreach (var locData in localisationData)
        {
            Dictionary<string, string> languageDict = new Dictionary<string, string>();
            for (int i = 0; i < locData.keys.Count; i++)
            {
                if (i < locData.values.Count)
                {
                    languageDict.Add(locData.keys[i], locData.values[i]);
                }
            }
            translations.Add(locData.languageCode, languageDict);
        }
    }

    // Method to swap text with translations
    public string GetLocalisedValue(string key, string languageCode)
    {
        if (translations.TryGetValue(languageCode, out var languageDict))
        {
            if (languageDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return key;
    }

    public void UpdateUILocalisation(string languageCode)
    {
        // Landing Page
        landingMainTitle.text = GetLocalisedValue("landingMainTitle", languageCode);
        landingBusinessAccessTitle.text = GetLocalisedValue("landingBusinessAccessTitle", languageCode);
        landingPrivateAccessTitle.text = GetLocalisedValue("landingPrivateAccessTitle", languageCode);
        landingBusinessAccessButton.text = GetLocalisedValue("landingBusinessAccessButton", languageCode);
        landingPrivateAccessButton.text = GetLocalisedValue("landingPrivateAccessButton", languageCode);

        // Tutorial Landing
        accessTypeTitle.text = GetLocalisedValue("accessTypeTitle", languageCode);
        accessTypeDescription.text = GetLocalisedValue("accessTypeDescription", languageCode);
        gotItButton.text = GetLocalisedValue("gotItButton", languageCode);

        // Settings
        settingsTitle.text = GetLocalisedValue("settingsTitle", languageCode);
        settingsLanguageTitle.text = GetLocalisedValue("settingsLanguageTitle", languageCode);
        settingsQualityTitle.text = GetLocalisedValue("settingsQualityTitle", languageCode);
        settingsMusicTitle.text = GetLocalisedValue("settingsMusicTitle", languageCode);
        settingsFullscreenTitle.text = GetLocalisedValue("settingsFullscreenTitle", languageCode);
        settingsClose.text = GetLocalisedValue("settingsClose", languageCode);
    }
}
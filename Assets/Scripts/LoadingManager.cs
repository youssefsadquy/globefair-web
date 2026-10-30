using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// Method that allows managing the loading screen appearance from any interaction
public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [System.Serializable]
    public class LoadingData
    {
        public GameObject container;
        public Text text;
        public GameObject animation;
    }

    public LoadingData fullscreenLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void EnableLoading(string text)
    {
        fullscreenLoading.text.text = text;
        fullscreenLoading.container.SetActive(true);
        fullscreenLoading.animation.SetActive(true);
        fullscreenLoading.animation.GetComponent<LoadingAnimation>().enabled = true;
    }

    public void DisableLoading()
    {
        fullscreenLoading.container.SetActive(false);
        fullscreenLoading.animation.SetActive(false);
        fullscreenLoading.animation.GetComponent<LoadingAnimation>().enabled = false;
    }
}

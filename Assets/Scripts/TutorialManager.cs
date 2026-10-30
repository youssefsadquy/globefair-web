using UnityEngine;

// Script that allows management of Tutorial panels to activate/deactivate 1 a time with
// their corresponding animation
public class TutorialManager : MonoBehaviour
{
    private GameObject currentTutorialPanel;

    // Activate panel with animation
    public void ToggleTutorialPanel(GameObject tutorialPanel)
    {
        bool isCurrentlyActive = tutorialPanel.activeSelf;

        if (isCurrentlyActive)
        {
            tutorialPanel.SetActive(false);
            if (tutorialPanel == currentTutorialPanel)
            {
                currentTutorialPanel = null;
            }
        }
        else
        {
            CloseCurrentTutorialPanel();

            currentTutorialPanel = tutorialPanel;
            currentTutorialPanel.SetActive(true);

            Animator animator = currentTutorialPanel.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play(currentTutorialPanel.name);
            }
        }
    }

    // Deactivating panel
    public void CloseCurrentTutorialPanel()
    {
        if (currentTutorialPanel != null)
        {
            currentTutorialPanel.SetActive(false);
            currentTutorialPanel = null;
        }
    }
}

using UnityEngine;

// Quit application comand
public class ExitGame : MonoBehaviour
{
    public void Quit()
    {
        Application.Quit();
        Debug.Log("Application Closed");
    }
}
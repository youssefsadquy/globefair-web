using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.UI;

// Using the UMA methods to allow gender selection
public class GenderSwitcher : MonoBehaviour
{
    public DynamicCharacterAvatar avatar;
    public Button maleButton;
    public Button femaleButton;

    void Start()
    {
        // Assigning gender buttons
        maleButton.onClick.AddListener(() => SetGender("HumanMale"));
        femaleButton.onClick.AddListener(() => SetGender("HumanFemale"));
    }

    // Calling UMA methods
    void SetGender(string race)
    {
        if (avatar != null)
        {
            avatar.ChangeRace(race);
            avatar.BuildCharacter();
        }
    }
}
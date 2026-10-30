using UnityEngine;
using System.Collections;

// Animation script assigned to circle sprite to change their size
// to create an animation "pulse"
public class LoadingAnimation : MonoBehaviour
{
    [SerializeField] private Transform[] circles;
    [SerializeField] private float minScaleFactor = 0.1f;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private float delayBetweenCircles = 0.1f;

    private void OnEnable()
    {
        foreach (var circle in circles)
        {
            circle.localScale = Vector3.one * minScaleFactor;
        }
        StartCoroutine(AnimateCircles());
    }

    private IEnumerator AnimateCircles()
    {
        while (true)
        {
            foreach (var circle in circles)
            {
                StartCoroutine(TransformExtensions.LerpScale(circle, Vector3.one, animationDuration));
                yield return new WaitForSeconds(animationDuration + delayBetweenCircles);

                StartCoroutine(TransformExtensions.LerpScale(circle, Vector3.one * minScaleFactor, animationDuration));
                yield return new WaitForSeconds(delayBetweenCircles);
            }
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}

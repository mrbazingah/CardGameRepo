using System.Collections;
using UnityEngine;

public class ChanceNotice : MonoBehaviour
{
    [SerializeField] Vector2 pointA, pointB;
    [SerializeField] float timeToPauseOnPoint, timeFromAToB;

    Vector2 localPointA, localPointB;
    bool towardsPointA;

    void Start()
    {
        localPointA = (Vector2)transform.position + pointA;
        localPointB = (Vector2)transform.position + pointB;

        StartCoroutine(UpAndDownRoutine());
    }

    IEnumerator UpAndDownRoutine()
    {
        while (true)
        {
            towardsPointA = !towardsPointA;
            float progress = 0;

            while (progress < 1)
            {
                float smoothProgress = Mathf.Pow(progress, 2) * (3f - 2f * progress);

                if (towardsPointA)
                {
                    transform.position = Vector2.Lerp(localPointB, localPointA, smoothProgress);
                }
                else
                {
                    transform.position = Vector2.Lerp(localPointA, localPointB, smoothProgress);
                }

                progress += Time.deltaTime / timeFromAToB;
                yield return new WaitForEndOfFrame();
            }

            yield return new WaitForSeconds(timeToPauseOnPoint);
        }
    }
}

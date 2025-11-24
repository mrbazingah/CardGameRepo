using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler : MonoBehaviour
{
    void Start()
    {
        // Find existing NetworkRunner from lobby (it should persist with DontDestroyOnLoad)
        NetworkRunner existingRunner = FindFirstObjectByType<NetworkRunner>();
        
        if (existingRunner != null)
        {
            Debug.Log("Found existing NetworkRunner, using it for game scene.");
            // Ensure the runner has a scene manager
            if (existingRunner.gameObject.GetComponent<NetworkSceneManagerDefault>() == null)
            {
                existingRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }
        }
        else
        {
            Debug.LogError("No existing NetworkRunner found! Make sure you're coming from the lobby scene.");
        }
    }
}

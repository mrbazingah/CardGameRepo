using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler : MonoBehaviour
{
    [SerializeField] NetworkRunner runnerPrefab;

    void Start()
    {
        var runner = Instantiate(runnerPrefab);
        runner.ProvideInput = true;
        var sceneManager = runner.gameObject.GetComponent<NetworkSceneManagerDefault>()
                            ?? runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "FusionCardGame",
            SceneManager = sceneManager,
            //Scene = SceneManager.GetActiveScene().buildIndex
        });
    }
}
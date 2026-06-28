using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class NormalModeScpEntitySwitchApplier : MonoBehaviour
{
    private const string NormalSceneName = "Normal_Mode";
    private const string ChaosSceneName = "Chaos_Mode";
    private const string AiEntityName = "AIEntity";
    private const string ScpSwitchName = "SCP_Switch";

    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != NormalSceneName && sceneName != ChaosSceneName)
            return;

        ApplySwitch(sceneName == ChaosSceneName);
    }

    private void ApplySwitch(bool isChaosScene)
    {
        GameObject aiEntity = FindSceneObject(AiEntityName);
        GameObject scpSwitch = FindSceneObject(ScpSwitchName);
        bool useScpSwitch = EasterEggSettings.ScpEntitySwitchEnabled;

        if (aiEntity != null)
            aiEntity.SetActive(!useScpSwitch);

        if (scpSwitch == null)
            return;

        scpSwitch.SetActive(useScpSwitch);
        if (!useScpSwitch)
            return;

        EnsureScpSwitchSetup(scpSwitch, isChaosScene);
    }

    private static void EnsureScpSwitchSetup(GameObject scpSwitch, bool enableChaosPropPushing)
    {
        NavMeshAgent agent = scpSwitch.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = scpSwitch.AddComponent<NavMeshAgent>();

        agent.speed = Mathf.Max(agent.speed, 2.25f);
        agent.acceleration = Mathf.Max(agent.acceleration, 16f);
        agent.angularSpeed = Mathf.Max(agent.angularSpeed, 360f);
        agent.stoppingDistance = 0.25f;
        agent.autoBraking = true;

        if (scpSwitch.GetComponent<SCP096MansionEnemy>() == null)
            scpSwitch.AddComponent<SCP096MansionEnemy>();

        if (enableChaosPropPushing && scpSwitch.GetComponent<SCP096PropPusher>() == null)
            scpSwitch.AddComponent<SCP096PropPusher>();
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform.name == objectName)
                return transform.gameObject;
        }

        return null;
    }
}

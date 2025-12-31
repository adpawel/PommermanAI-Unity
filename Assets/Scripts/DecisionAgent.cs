using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DecisionAgent : Agent
{
    public int chosenAction { get; private set; }

    int steps;

    public override void Initialize()
    {
        Debug.Log("DecisionAgent Initialize()");
    }

    void Update()
    {
        Debug.Log("DecisionAgent Update()");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        chosenAction = actions.DiscreteActions[0];
        AddReward(-0.01f);

        steps++;
        if (steps > 1000)
        {
            AddReward(-0.2f); // kara za przeciąganie
            EndEpisode();
            steps = 0;
        }
    }

    public override void OnEpisodeBegin()
    {
        steps = 0;
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Minimalne, STABILNE obserwacje
        sensor.AddObservation(IsEnemyVisible());
        sensor.AddObservation(IsCrateNearby());
        sensor.AddObservation(IsBombThreat());
        sensor.AddObservation(Random.value); // zapobiega deadlockom
    }

    bool IsEnemyVisible()
    {
        return FindObjectsOfType<PommermanAgent>().Length > 1;
    }

    bool IsCrateNearby()
    {
        return Physics.OverlapSphere(transform.position, 1.1f, LayerMask.GetMask("WallBreakable")).Length > 0;
    }

    bool IsBombThreat()
    {
        foreach (Bomb b in FindObjectsOfType<Bomb>())
            if (b.TimeToExplode < 1.2f)
                return true;
        return false;
    }
}

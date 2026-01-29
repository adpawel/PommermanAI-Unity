using System.Collections.Generic;
using UnityEngine;

public class ArenaManager2v2 : MonoBehaviour
{
    public int size = 7;
    public GameObject floorPrefab;
    public GameObject wallSolidPrefab;
    public GameObject wallBreakablePrefab;
    public GameObject trainablePlayerPrefab;
    public GameObject opponentPrefab;
    public Transform playersParent;

    private int breakableCount = 0;
    public int playerCount = 4;
    private Vector3[] agentStartPositions;

    private List<Vector3> safeStartAreas;

    private int agentsAlive;

    private bool episodeInProgress = false;
    bool team0Alive = false;
    bool team1Alive = false;

    void Start()
    {
        if (trainablePlayerPrefab == null || opponentPrefab == null)
        {
            Debug.LogError("FATAL ERROR: Player prefabs are not assigned in the Inspector on object: " + gameObject.name);
            return;
        }

        InitializePositions();
        Generate();
        InstantiatePlayers();

        agentsAlive = agentStartPositions.Length;
        episodeInProgress = true;
    }


    void InitializePositions()
    {
        int num = size / 2;
        agentStartPositions = new Vector3[playerCount];
        safeStartAreas = new List<Vector3>();
        Vector3[] array = new Vector3[]
        {
            new Vector3(1f, 0.5f, 1f),
            new Vector3((size - 2), 0.5f, (size - 2)),
            new Vector3(1f, 0.5f, (size - 2)),
            new Vector3((size - 2), 0.5f, 1f)
        };
        for (int i = 0; i < this.playerCount; i++)
        {
            Vector3 vector = array[i % array.Length];
            Vector3 vector2 = new Vector3(vector.x - num, vector.y, vector.z - num);
            agentStartPositions[i] = vector2;
            for (int j = -1; j <= 1; j++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    safeStartAreas.Add(vector2 + new Vector3(j, 0f, k));
                }
            }
        }
    }

    void Generate()
    {
        int num = size / 2;
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                Vector3 vector = new Vector3((float)(i - num), 0f, (float)(j - num));
                Object.Instantiate<GameObject>(floorPrefab, vector, Quaternion.identity, transform);
                if (i == 0 || j == 0 || i == size - 1 || j == size - 1 || (i % 2 == 0 && j % 2 == 0))
                {
                    Object.Instantiate<GameObject>(wallSolidPrefab, vector + Vector3.up * 0.5f, Quaternion.identity, transform);
                }
                else
                {
                    Vector3 vector2 = vector + Vector3.up * 0.5f;
                    if (!this.IsPlayerStartArea(vector2) && Random.value < 0.25f)
                    {
                        Instantiate(wallBreakablePrefab, vector2, Quaternion.identity, transform);
                        breakableCount++;
                    }
                }
            }
        }
    }

    void InstantiatePlayers()
    {
        for (int i = 0; i < playerCount; i++)
        {
            GameObject prefab = (i < 2) ? trainablePlayerPrefab : opponentPrefab;
            GameObject go = Instantiate(prefab, agentStartPositions[i], Quaternion.identity, playersParent);

            PommermanAgent2v2 agent = go.GetComponent<PommermanAgent2v2>();
            agent.SetArenaManager(this);
            agent.SetAgentIndex(i);
            agent.SetStartPosition(agentStartPositions[i]);

            agent.SetTeam(i < 2 ? 0 : 1);
        }

        PommermanAgent2v2[] array = Object.FindObjectsByType<PommermanAgent2v2>(FindObjectsSortMode.None);

        for (int i = 0; i < array.Length; i++)
        {
            array[i].RefreshEnemies();
        }
        this.agentsAlive = this.agentStartPositions.Length;
    }


    /// <summary>
    /// Sprawdza, czy dana pozycja (środek pola) znajduje się w zdefiniowanej strefie startowej,
    /// gdzie nie mogą pojawić się niszczalne przeszkody.
    /// </summary>
    bool IsPlayerStartArea(Vector3 checkPos)
    {
        foreach (Vector3 safePos in safeStartAreas) // Używamy teraz safeStartAreas
        {
            // Porównujemy tylko X i Z, ignorując Y
            if (Mathf.Abs(checkPos.x - safePos.x) < 0.1f &&
                Mathf.Abs(checkPos.z - safePos.z) < 0.1f)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Wywoływana przez agenta, gdy zginie (np. przez bombę).
    /// </summary>
    public void OnAgentDied(PommermanAgent2v2 agent)
    {
        if (!this.episodeInProgress)
        {
            return;
        }
        agentsAlive--;
        Destroy(agent.gameObject);

        team0Alive = false;
        team1Alive = false;
        foreach (var a in FindObjectsByType<PommermanAgent2v2>(FindObjectsSortMode.None))
        {
            if (!a.isAlive) continue;

            if (a.teamId == 0) team0Alive = true;
            if (a.teamId == 1) team1Alive = true;
        }

        if (!team0Alive || !team1Alive)
        {
            EndRound();
        }
    }

    public void EndRound()
    {
        this.episodeInProgress = false;
        int winningTeam = team0Alive ? 0 : 1;
        foreach (PommermanAgent2v2 a in FindObjectsByType<PommermanAgent2v2>(FindObjectsSortMode.None))
        {
            if (a.teamId == winningTeam)
                a.AddReward(2.0f);
            else
                a.AddReward(-1.0f);

            a.EndEpisode();
        }
        base.Invoke("ResetEnvironment", 0.05f);
    }

    public void OnBreakableDestroyed(PommermanAgent2v2 owner)
    {
        // zabezpieczenie
        if (breakableCount <= 0)
        {
            return;
        }

        breakableCount--;

        // Debug
        //Debug.Log($"Breakable destroyed. Remaining: {breakableCount}");

        if (breakableCount <= 0)
        {
            //Debug.Log("All breakables destroyed! Awarding win...");

            //if (owner != null)
            //{
            //    owner.EndEpisode();
            //}

            //ResetEnvironment();
        }
    }


    private void ResetEnvironment()
    {
        Bomb2v2[] array = Object.FindObjectsByType<Bomb2v2>(FindObjectsSortMode.None);
        for (int i = 0; i < array.Length; i++)
        {
            Object.Destroy(array[i].gameObject);
        }
        PommermanAgent2v2[] array2 = Object.FindObjectsByType<PommermanAgent2v2>(FindObjectsSortMode.None);
        for (int i = 0; i < array2.Length; i++)
        {
            Object.Destroy(array2[i].gameObject);
        }
        foreach (object obj in base.transform)
        {
            Object.Destroy(((Transform)obj).gameObject);
        }
        this.breakableCount = 0;
        this.Generate();
        this.InstantiatePlayers();
        this.episodeInProgress = true;
    }
}
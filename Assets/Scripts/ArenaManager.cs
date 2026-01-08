using System.Collections.Generic;
using UnityEngine;

public class ArenaManager : MonoBehaviour
{
    public int size = 7;
    public GameObject floorPrefab;
    public GameObject wallSolidPrefab;
    public GameObject wallBreakablePrefab;
    public GameObject trainablePlayerPrefab;
    public GameObject opponentPrefab;
    public Transform playersParent;

    private int breakableCount = 0;
    public int playerCount = 2;
    private Vector3[] agentStartPositions;

    private List<Vector3> safeStartAreas;

    private int agentsAlive;

    private bool episodeInProgress = false;

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
        GameObject gameObject = Object.Instantiate<GameObject>(this.trainablePlayerPrefab, this.agentStartPositions[0], Quaternion.identity, this.playersParent);
        gameObject.name = "PommermanAgent_0_Trainable";
        PommermanAgent component = gameObject.GetComponent<PommermanAgent>();
        component.SetArenaManager(this);
        component.SetAgentIndex(0);
        component.SetStartPosition(this.agentStartPositions[0]);
        GameObject gameObject2 = Object.Instantiate<GameObject>(opponentPrefab, agentStartPositions[1], Quaternion.identity, playersParent);
        gameObject2.name = "PommermanAgent_1_Opponent";

        PommermanAgent component2 = gameObject2.GetComponent<PommermanAgent>();

        component2.SetArenaManager(this);
        component2.SetAgentIndex(1);
        component2.SetStartPosition(this.agentStartPositions[1]);

        PommermanAgent[] array = Object.FindObjectsByType<PommermanAgent>(FindObjectsSortMode.None);

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
    public void OnAgentDied(PommermanAgent agent)
    {
        if (!this.episodeInProgress)
        {
            return;
        }
        this.agentsAlive--;
        if (this.agentsAlive <= 1)
        {
            Debug.Log(agent.name + " zginął");
            this.EndRound();
        }
    }

    public void EndRound()
    {
        this.episodeInProgress = false;
        foreach (PommermanAgent pommermanAgent in Object.FindObjectsByType<PommermanAgent>(FindObjectsSortMode.None))
        {
            //if (pommermanAgent.isAlive && agentsAlive == 2)
            //{
            //    pommermanAgent.AddReward(-0.5f);
            //}
            pommermanAgent.EndEpisode();
        }
        base.Invoke("ResetEnvironment", 0.05f);
    }

    /// uwaga
    public void OnBreakableDestroyed(PommermanAgent owner)
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
        Bomb[] array = Object.FindObjectsByType<Bomb>(FindObjectsSortMode.None);
        for (int i = 0; i < array.Length; i++)
        {
            Object.Destroy(array[i].gameObject);
        }
        PommermanAgent[] array2 = Object.FindObjectsByType<PommermanAgent>(FindObjectsSortMode.None);
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

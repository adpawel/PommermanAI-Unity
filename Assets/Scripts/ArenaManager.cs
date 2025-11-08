using System.Collections.Generic; // Dodajemy dla zarządzania agentami

using UnityEngine;

using System.Collections;



public class ArenaManager : MonoBehaviour
{
    public int size = 11;
    public GameObject floorPrefab;
    public GameObject wallSolidPrefab;
    public GameObject wallBreakablePrefab;
    public GameObject playerPrefab;
    public Transform playersParent;

    // Pozycje startowe dla 2 agentów (używane do instancjonowania)
    private Vector3[] agentStartPositions;

    // Lista pól w strefie bezpieczeństwa (używana do wykluczania murów)
    private List<Vector3> safeStartAreas;

    // --- Logika dla resetu i kontroli stanu gry ---
    private int agentsAlive;

    private bool episodeInProgress = false;

    void Start()
    {
        InitializePositions();
        Generate();
        InstantiatePlayers();

        agentsAlive = agentStartPositions.Length;
        episodeInProgress = true;
    }


    void InitializePositions()
    {
        int offset = size / 2;
        // === Krok 1: Definicja DOKŁADNYCH 2 pozycji startowych (1v1) ===
        agentStartPositions = new Vector3[]
        {
            // Agent 1 (Lewy dół: x=1, z=1)
            new Vector3(1 - offset, 0.5f, 1 - offset), 

            // Agent 2 (Prawy góra: x=size-2, z=size-2)
            new Vector3(size - 2 - offset, 0.5f, size - 2 - offset)
        };

        // === Krok 2: Definicja SZEROKICH stref bezpieczeństwa (3x3 wokół startów) ===
        safeStartAreas = new List<Vector3>();

        // Pętla tworząca strefę dla Agent 1
        for (int x = 1; x <= 3; x++)
        {
            for (int z = 1; z <= 2; z++)
            {
                safeStartAreas.Add(new Vector3(x - offset, 0.5f, z - offset));
            }
        }

        // Pętla tworząca strefę 3x3 dla Agent 2

        for (int x = size - 3; x <= size - 1; x++)
        {
            for (int z = size - 3; z <= size - 2; z++)
            {
                safeStartAreas.Add(new Vector3(x - offset, 0.5f, z - offset));
            }
        }
    }


    void Generate()
    {
        // Zniszcz stare obiekty przed generacją nowej planszy (dla przyszłego ResetEnvironment)
        // Możesz użyć tego na początku Generate lub w oddzielnej funkcji ClearBoard()
        // W tej chwili zakładamy, że jest to wywołane tylko w Start()
        //ResetEnvironment();
        int halfSize = size / 2;

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                Vector3 pos = new Vector3(x - halfSize, 0, z - halfSize);

                // 1) Floor na każdym polu
                Instantiate(floorPrefab, pos, Quaternion.identity, transform);

                // 2) Graniczne i "skrzyżowane" solid walls
                if (x == 0 || z == 0 || x == size - 1 || z == size - 1 || (x % 2 == 0 && z % 2 == 0))
                {
                    Instantiate(wallSolidPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
                }
                else
                {
                    Vector3 currentWallPos = pos + Vector3.up * 0.5f;

                    // === Krok 2: Sprawdzenie, czy pole jest strefą bezpieczeństwa ===
                    if (!IsPlayerStartArea(currentWallPos))
                    {
                        // 3) Losowo niszczalne mury
                        if (Random.value < 0.5f)
                        {
                            Instantiate(wallBreakablePrefab, currentWallPos, Quaternion.identity, transform);
                        }
                    }
                }
            }
        }
    }

    void InstantiatePlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player Prefab not assigned to Arena Manager!");
            return;
        }

        if(agentsAlive <= 1)
        {
            for (int i = 0; i < agentStartPositions.Length; i++)
            {
                Vector3 startPos = agentStartPositions[i];

                // Stwórz gracza na zdefiniowanej pozycji
                GameObject newPlayer = Instantiate(playerPrefab, startPos, Quaternion.identity, playersParent);

                newPlayer.name = "PommermanAgent_" + (i + 1);

                PommermanAgent agentScript = newPlayer.GetComponent<PommermanAgent>();
                if (agentScript != null)
                {
                    agentScript.SetArenaManager(this);
                }
            }
        }
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
    public void OnAgentDied(MonoBehaviour agent)
    {
        if (!episodeInProgress) return;
        agentsAlive--;

        // Sprawdzamy, czy pozostał tylko jeden (zwycięzca) lub nikt (remis/samobójstwo)
        if (agentsAlive <= 1)
        {
            // Opcjonalnie: Nagradzanie ostatniego żywego agenta
            if (agentsAlive == 1)
            {
                // TODO: Znajdź i nagródź ostatniego żywego agenta
            }
            ResetEnvironment();
        } 
        else
        {
            DestroyImmediate(agent.gameObject);
        }
    }
    
    void ResetEnvironment()
    {
        // === 1. Zabezpieczenie przed ponownym resetem ===
        if (!episodeInProgress) return;
        episodeInProgress = false;

        var bombs = FindObjectsByType<Bomb>(FindObjectsSortMode.None);
        foreach (var bomb in bombs)
            Destroy(bomb.gameObject);

        PommermanAgent[] agents = FindObjectsByType<PommermanAgent>(FindObjectsSortMode.None);
        foreach (PommermanAgent agent in agents)
        {
            if (agent != null)
            {
                DestroyImmediate(agent.gameObject);
            }
        }

        var transformChildren = transform;
        foreach (Transform child in transformChildren)
            if (child != transform)
                Destroy(child.gameObject);

        Generate();
        InstantiatePlayers();
        agentsAlive = agentStartPositions.Length;
        episodeInProgress = true;
    }
}


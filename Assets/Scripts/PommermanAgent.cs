using UnityEngine;
using Unity.MLAgents; // KLUCZOWE: Dla klasy Agent
using Unity.MLAgents.Actuators; // KLUCZOWE: Dla akcji (decyzji AI)
using Unity.MLAgents.Sensors; // KLUCZOWE: Dla zbierania obserwacji

public class PommermanAgent : Agent
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    private Vector3 targetPosition;
    private bool isMoving = false;
    public Vector3 startPosition;
    public int agentIndexPublic = 0;

    [Header("Gameplay Settings")]
    public GameObject bombPrefab;
    public bool isAlive = true;
    private bool canPlaceBomb = true;

    [Header("References")]
    private ArenaManager arenaManager;
    private Rigidbody rb;
    private int agentIndex; // 0 = pierwszy, 1 = drugi

    // Sta³e do tagów i nagród
    private const string SOLID_OBJECT_TAG = "WallSolid";
    private const string BREAKABLE_OBJECT_TAG = "WallBreakable";
    private const string BOMB_OBJECT_TAG = "Bomb";

    private const float MOVE_REWARD = 0.001f;
    private const float WALL_HIT_PENALTY = -0.005f;

    private const int MAX_EPISODE_STEPS = 1000;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Upewnij siê, ¿e zaczynamy na zaokr¹glonej pozycji siatki
        targetPosition = new Vector3(Mathf.Round(transform.position.x), transform.position.y, Mathf.Round(transform.position.z));
        transform.position = targetPosition;

        // Uzyskaj referencjê do ArenaManager
        arenaManager = FindAnyObjectByType<ArenaManager>();
        if (arenaManager == null)
        {
            Debug.LogError("ArenaManager not found in scene!");
        }
    }

    // === ZAST¥PIENIE Update() DLA P£YNNEGO RUCHU ===
    // U¿ywamy FixedUpdate do ruchu, poniewa¿ ML-Agents domyœlnie dzia³a w sta³ych krokach.
    void FixedUpdate()
    {
        if (isMoving)
        {
            // P³ynne poruszanie siê do docelowej pozycji
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.fixedDeltaTime);

            // Jeœli agent jest blisko, zatrzaœnij go na pozycji i zatrzymaj ruch
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
        // W ka¿dym kroku (nawet jeœli stoi): nagroda za przetrwanie.
        AddReward(MOVE_REWARD);
        if (StepCount >= MAX_EPISODE_STEPS)
        {
            // Neutralna nagroda (0.0f), poniewa¿ nie wygra³ ani nie przegra³, po prostu skoñczy³ czas.
            //AddReward(-2.0f);
            EndEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 3 obserwacje: normalizowana pozycja X, normalizowana pozycja Z, flaga isMoving
        float obsX = 0f;
        float obsZ = 0f;

        // Normalizujemy pozycjê wzglêdem po³owy rozmiaru planszy (zakres przybli¿ony do [-1, 1])
        if (arenaManager != null)
        {
            float half = Mathf.Max(1f, arenaManager.size / 2f); // zabezpieczenie przed dzieleniem przez 0
            obsX = Mathf.Clamp(transform.localPosition.x / half, -1f, 1f);
            obsZ = Mathf.Clamp(transform.localPosition.z / half, -1f, 1f);
        }
        else
        {
            // Fallback — je¿eli arenaManager nie jest ustawiony, u¿ywamy niewielkiej normalizacji
            obsX = Mathf.Clamp(transform.localPosition.x / 10f, -1f, 1f);
            obsZ = Mathf.Clamp(transform.localPosition.z / 10f, -1f, 1f);
        }

        sensor.AddObservation(obsX);                 // 1: normalizowana pozycja X
        sensor.AddObservation(obsZ);                 // 2: normalizowana pozycja Z
        sensor.AddObservation(isMoving ? 1f : 0f);   // 3: czy agent siê porusza (0/1)
        sensor.AddObservation(canPlaceBomb ? 1f : 0f);
        sensor.AddObservation(agentIndex / 1f);
    }


    // === 2. ODBIÓR AKCJI OD AI (DECYZJI) ===
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Odbieramy akcjê z pierwszej (i jedynej) ga³êzi dyskretnej
        int action = actions.DiscreteActions[0];

        if (!isMoving)
        {
            Vector3 direction = Vector3.zero;
            bool shouldMove = false;

            // Konwersja akcji dyskretnych (0-5) na kierunek/dzia³anie:
            if (action == 1) { direction = Vector3.forward; shouldMove = true; } // Pó³noc (Z+)
            else if (action == 2) { direction = Vector3.right; shouldMove = true; }  // Wschód (X+)
            else if (action == 3) { direction = Vector3.back; shouldMove = true; }   // Po³udnie (Z-)
            else if (action == 4) { direction = Vector3.left; shouldMove = true; }  // Zachód (X-)
            else if (action == 5) // Stawianie bomby
            {
                PlaceBomb();
            }

            if (shouldMove)
            {
                Vector3 potentialPosition = transform.position + direction;
                Vector3 newTargetPosition = new Vector3(
                  Mathf.Round(potentialPosition.x),
                  transform.position.y,
                  Mathf.Round(potentialPosition.z)
                );

                if (CanMoveTo(newTargetPosition))
                {
                    targetPosition = newTargetPosition;
                    isMoving = true;
                    // Nagradzaj za pomyœlny ruch
                    AddReward(MOVE_REWARD);
                }
                else
                {
                    // Kara za próbê wejœcia w zablokowane pole
                    AddReward(WALL_HIT_PENALTY);
                }
            }
        }
    }

    // === 3. RESET EPIZODU ===
    public override void OnEpisodeBegin()
    {
        // Reset podstawowych flag
        //isAlive = true;
        //canPlaceBomb = true;
        //isMoving = false;

        //// Jeœli ArenaManager potrafi zwróciæ pozycjê startow¹, u¿yj jej, 
        //// w przeciwnym razie u¿yj startPosition ustawionego przez ArenaManager przy tworzeniu.
        //if (arenaManager != null)
        //{
        //    // jeœli masz metodê w ArenaManager: GetSpawnPositionForAgent(agentIndex) -> u¿yj jej
        //    // otherwise use startPosition field
        //    Vector3 spawn = startPosition;
        //    transform.position = new Vector3(Mathf.Round(spawn.x), transform.position.y, Mathf.Round(spawn.z));
        //}
        //else
        //{
        //    // fallback: round current position
        //    transform.position = new Vector3(Mathf.Round(transform.position.x), transform.position.y, Mathf.Round(transform.position.z));
        //    transform.position = new Vector3(Mathf.Round(transform.position.x), transform.position.y, Mathf.Round(transform.position.z));
        //}
        //targetPosition = transform.position;
    }


    // Metoda Die() jest teraz kluczowa dla RL
    public void Die()
    {
        //Debug.Log(gameObject.name + " zgin¹³! Epizod dla tego agenta zakoñczony.");
        AddReward(-1.0f);
        // Zakoñcz epizod DLA TEGO AGENTA
        EndEpisode();

        if (arenaManager != null)
        {
            // Poinformuj Managera o œmierci (linia 139)
            arenaManager.OnAgentDied(this);
        }
        else
        {
            Debug.LogError("Arena Manager is null! Cannot report death.");
        }
        // ==========================================

    }

    // Funkcja pomocnicza do stawiania bomby
    private void PlaceBomb()
    {
        if (!canPlaceBomb) return;

        if (bombPrefab != null)
        {
            canPlaceBomb = false;

            GameObject newBomb = Instantiate(bombPrefab, new Vector3(Mathf.Round(transform.position.x), 0.5f, Mathf.Round(transform.position.z)), Quaternion.identity);
            Bomb bombScript = newBomb.GetComponent<Bomb>();
            if (bombScript != null)
            {
                // Przekazujemy referencjê do agenta, aby bomba mog³a go powiadomiæ
                bombScript.SetOwner(this);

                bool targetFound = CheckForTargetWalls(transform.position, bombScript.blastRadius);
                if (targetFound)
                {
                    AddReward(0.1f); // Œrednia nagroda za DOBRE umieszczenie bomby
                }
                //else
                //{
                //    AddReward(0.02f); // Ma³a nagroda za samo postawienie bomby
                //}
            }
            else
            {
                // Jeœli nie znaleziono skryptu bomby, przyznaj minimaln¹ nagrodê (unikamy crashu)
                AddReward(0.005f);
            }
        }
    }


    private bool CheckForTargetWalls(Vector3 bombCenter, int radius)
    {
        int mask = LayerMask.GetMask("WallBreakable");

        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= radius; i++)
            {
                Vector3 checkPos = bombCenter + dir * i;
                Collider[] colliders = Physics.OverlapBox(
                    new Vector3(Mathf.Round(checkPos.x), 0.5f, Mathf.Round(checkPos.z)),
                    new Vector3(0.45f, 0.45f, 0.45f),
                    Quaternion.identity,
                    mask
                );

                if (colliders.Length > 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void NotifyBombExploded()
    {
        // Odblokuj mo¿liwoœæ postawienia nowej bomby
        canPlaceBomb = true;
    }

    // Metoda CanMoveTo() pozostaje bez zmian
    bool CanMoveTo(Vector3 position)
    {
        Vector3 checkPoint = position;
        checkPoint.y = 0.5f;

        Collider[] colliders = Physics.OverlapBox(checkPoint, new Vector3(0.45f, 0.45f, 0.45f), Quaternion.identity);

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Floor") || col.CompareTag("Player") || col.isTrigger)
            {
                continue;
            }

            if (col.CompareTag(BOMB_OBJECT_TAG))
            {
                if (Vector3.Distance(transform.position, position) > 0.1f)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }

            if (col.CompareTag(SOLID_OBJECT_TAG) || col.CompareTag(BREAKABLE_OBJECT_TAG))
            {
                return false;
            }
        }
        return true;
    }

    public void SetArenaManager(ArenaManager manager)
    {
        this.arenaManager = manager;
    }

    public void SetAgentIndex(int idx)
    {
        agentIndex = idx;
        agentIndexPublic = idx;
    }

    public void SetStartPosition(Vector3 pos)
    {
        startPosition = pos;
    }

}
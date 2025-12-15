using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

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

    private const float MOVE_REWARD = 0;
    private const float WALL_HIT_PENALTY = -0.005f;

    private const int MAX_EPISODE_STEPS = 5000;

    public int bombObservationCount = 2;
    public float bombObserveRadius = 6f;

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
        //AddReward(-0.0005f);
        if (StepCount >= MAX_EPISODE_STEPS)
        {
            // lekka kara za zbyt d³ugie przeci¹ganie epizodu
            //AddReward(-0.2f);
            //EndEpisode();
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

        Collider[] nearby = Physics.OverlapSphere(transform.position, bombObserveRadius);
        // lista bomb
        List<Bomb> bombs = new List<Bomb>();
        foreach (var c in nearby)
        {
            if (c.CompareTag("Bomb"))
            {
                Bomb b = c.GetComponent<Bomb>();
                if (b != null) bombs.Add(b);
            }
        }

        // posortuj po odleg³oœci rosn¹co
        bombs.Sort((a, b) => {
            float da = Vector3.Distance(transform.position, a.transform.position);
            float db = Vector3.Distance(transform.position, b.transform.position);
            return da.CompareTo(db);
        });

        // Dodaj obserwacje dla K najbli¿szych bomb (distance norm, fuse norm, owner flag)
        for (int i = 0; i < bombObservationCount; i++)
        {
            if (i < bombs.Count)
            {
                Bomb b = bombs[i];
                float dist = Vector3.Distance(transform.position, b.transform.position);
                float distNorm = Mathf.Clamp01(dist / bombObserveRadius); // 0..1

                float fuseNorm = b.GetRemainingFuseNormalized(); // 0..1
                                                                 // odwróæ, jeœli chcesz: closer to 0 => about to explode, ale trzymamy 0..1

                // czy moja bomba?
                bool isOwner = false;
                PommermanAgent owner = null;
                // jeœli bomb ma metodê GetOwner() lub public ownerAgent
                owner = b.GetComponent<Bomb>().GetOwner();
                if (owner == this) isOwner = true;

                sensor.AddObservation(distNorm);
                sensor.AddObservation(fuseNorm);
                sensor.AddObservation(isOwner ? 1f : 0f);
            }
            else
            {
                // padding gdy brak bomb
                sensor.AddObservation(1f); // distMax
                sensor.AddObservation(0f); // fuse 0
                sensor.AddObservation(0f); // not owner
            }
        }
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
                    arenaManager.cumulativeReward += MOVE_REWARD;
                }
                else
                {
                    // Kara za próbê wejœcia w zablokowane pole
                    AddReward(WALL_HIT_PENALTY);
                    arenaManager.cumulativeReward += WALL_HIT_PENALTY;
                }
            }
        }
    }

    // === 3. RESET EPIZODU ===
    public override void OnEpisodeBegin()
    {
    }


    // Metoda Die() jest teraz kluczowa dla RL
    public void Die()
    {
        //Debug.Log(gameObject.name + " zgin¹³! Epizod dla tego agenta zakoñczony.");
        //AddReward(-1.0f);
        // Zakoñcz epizod DLA TEGO AGENTA
        print("Reward this round: " + GetCumulativeReward());
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
                    arenaManager.cumulativeReward += 0.1f;
                    AddReward(0.1f); // Œrednia nagroda za DOBRE umieszczenie bomby
                    print("Poprawne postawienie bomby.");
                }
                else
                {
                    //AddReward(-0.1f);
                    //print("kara za z³e postawienie bomby.");
                }
            }
        }
    }

    private bool CheckForTargetWalls(Vector3 bombCenter, int radius)
    {
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        for (int dirI = 0; dirI < directions.Length; dirI++)
        {
            Vector3 dir = directions[dirI];
            for (int i = 1; i <= radius; i++)
            {
                Vector3 checkPos = bombCenter + dir * i;
                Vector3 center = new Vector3(Mathf.Round(checkPos.x), 0.5f, Mathf.Round(checkPos.z));
                Vector3 halfExtents = new Vector3(0.45f, 0.45f, 0.45f);

                // 1) SprawdŸ bez maski — zobacz co zwraca
                Collider[] collidersAll = Physics.OverlapBox(center, halfExtents, Quaternion.identity);
                if (collidersAll.Length > 0)
                {
                    foreach (var c in collidersAll)
                    {
                        if (c.CompareTag("WallBreakable"))
                        {
                            return true;
                        }
                    }
                }

                int mask = LayerMask.GetMask("Destructible");
                if (mask != 0)
                {
                    Collider[] collidersLayer = Physics.OverlapBox(center, halfExtents, Quaternion.identity, mask);
                    if (collidersLayer.Length > 0)
                    {
                        Debug.Log($"OverlapBox with MASK hit at {center}: {collidersLayer.Length}");
                        return true;
                    }
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
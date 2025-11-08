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
    }

    // === 1. ZBIERANIE OBSERWACJI (INPUT DLA AI) ===
    public override void CollectObservations(VectorSensor sensor)
    {
        //Debug.Log($"CollectObservations called on {gameObject.name}");
        // Na pocz¹tek dodajemy minimalne, wektorowe obserwacje:
        // 1. Pozycja X i Z (2)
        // 2. Czy agent siê aktualnie porusza (1)
        // Razem: 3 floaty
        if (transform != null) // Zabezpieczenie przed usuniêtym obiektem
        {
            sensor.AddObservation(transform.localPosition.x);
            sensor.AddObservation(transform.localPosition.z);
            sensor.AddObservation(isMoving);
        }

        // TODO: W przysz³oœci dodaj tutaj 'widzenie planszy' (Grid Sensor lub Ray Perception)
        // np. odleg³oœæ do najbli¿szego przeciwnika/muru.
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
                    AddReward(0.002f);
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
    }


    // Metoda Die() jest teraz kluczowa dla RL
    public void Die()
    {
        // Du¿a kara za œmieræ
        SetReward(-1.0f);
        //Debug.Log(gameObject.name + " zgin¹³! Epizod dla tego agenta zakoñczony.");

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
        if (!canPlaceBomb)
        {
            return;
        }


        if (bombPrefab != null)
        {
            canPlaceBomb = false;

            GameObject newBomb = Instantiate(bombPrefab, new Vector3(Mathf.Round(transform.position.x), 0.5f, Mathf.Round(transform.position.z)), Quaternion.identity);
            Bomb bombScript = newBomb.GetComponent<Bomb>();
            if (bombScript != null)
            {
                // Przekazujemy referencjê do agenta, aby bomba mog³a go powiadomiæ
                bombScript.SetOwner(this);
            }

            AddReward(0.01f); // Nagroda za podjêcie ryzykownej akcji
        }
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
}
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
    public int maxBombs = 2;
    public bool isDummy;
    public bool isOpponent;

    private int moveTicks = 0;
    private int lastMoveTicks = 0;


    [Header("References")]
    private ArenaManager arenaManager;
    private Rigidbody rb;
    private int agentIndex; // 0 = pierwszy, 1 = drugi

    // Stałe do tagów i nagród
    private const string SOLID_OBJECT_TAG = "WallSolid";
    private const string BREAKABLE_OBJECT_TAG = "WallBreakable";
    private const string BOMB_OBJECT_TAG = "Bomb";

    private const float WALL_HIT_PENALTY = -0.005f;

    private const int MAX_EPISODE_STEPS = 5000;

    public int bombObservationCount = 2;
    public float bombObserveRadius = 6f;

    public int enemyObservationCount = 1;
    private float prevDistToEnemy = float.MaxValue;
    private Bomb lastPlacedBomb;
    private bool bombPlacedByMe;
    private float prevDistanceFromBomb;
    public int stepCount = 0;
    private List<PommermanAgent> enemies = new List<PommermanAgent>();
    private HashSet<Vector2Int> visitedCells;

    private static readonly Vector2Int[] DIRS = new Vector2Int[]
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Upewnij się, że zaczynamy na zaokrąglonej pozycji siatki
        targetPosition = new Vector3(Mathf.Round(transform.position.x), transform.position.y, Mathf.Round(transform.position.z));
        transform.position = targetPosition;

        // Uzyskaj referencję do ArenaManager
        arenaManager = FindAnyObjectByType<ArenaManager>();
        if (arenaManager == null)
        {
            Debug.LogError("ArenaManager not found in scene!");
        }

        foreach (PommermanAgent pommermanAgent in Object.FindObjectsByType<PommermanAgent>(FindObjectsSortMode.None))
        {
            if (pommermanAgent != this)
            {
                this.enemies.Add(pommermanAgent);
            }
        }
    }

    public void RefreshEnemies()
    {
        this.enemies.Clear();
        foreach (PommermanAgent pommermanAgent in Object.FindObjectsByType<PommermanAgent>(FindObjectsSortMode.None))
        {
            if (pommermanAgent != this && pommermanAgent.isAlive)
            {
                this.enemies.Add(pommermanAgent);
            }
        }
    }

    // === ZASTĄPIENIE Update() DLA PŁYNNEGO RUCHU ===
    // Używamy FixedUpdate do ruchu, ponieważ ML-Agents domyślnie działa w stałych krokach.
    void FixedUpdate()
    {
        if (isMoving)
        {
            moveTicks++;
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.fixedDeltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
                lastMoveTicks = moveTicks;
                moveTicks = 0;

                //Debug.Log($"MOVE TICKS = {lastMoveTicks}");
            }
        }

        stepCount++;

        if (stepCount % 500 == 0)
        {
            visitedCells.Clear();
        }

        if (stepCount > 3500)
        {
            arenaManager.EndRound();
        }

        //if (lastPlacedBomb != null)
        //{
        //    float d = Vector3.Distance(transform.position, lastPlacedBomb.transform.position);

        //    if (lastPlacedBomb.GetRemainingFuseNormalized() < 0.6f && d < lastPlacedBomb.blastRadius + 0.2f)
        //    {
        //        if (d > prevDistanceFromBomb)
        //            AddReward(0.003f);
        //        else
        //            AddReward(-0.003f);

        //        prevDistanceFromBomb = d;
        //    }
        //}

        Collider[] nearby = Physics.OverlapSphere(transform.position, bombObserveRadius - 2);

        foreach (var c in nearby)
        {
            if (!c.CompareTag(BOMB_OBJECT_TAG))
                continue;

            Bomb b = c.GetComponent<Bomb>();
            if (b == null)
                continue;

            if (IsInBombBlast(b) && b.GetRemainingFuseNormalized() < 0.7f)
            {
                AddReward(-0.01f);
            }
        }
    }

    bool IsInBombBlast(Bomb bomb)
    {
        Vector3 bombPos = bomb.transform.position;
        Vector3 agentPos = transform.position;

        bombPos.y = 0f;
        agentPos.y = 0f;

        Vector3 delta = agentPos - bombPos;

        // tylko linie proste
        if (Mathf.Abs(delta.x) > 0.1f && Mathf.Abs(delta.z) > 0.1f)
            return false;

        float dist = Mathf.Abs(delta.x) > Mathf.Abs(delta.z)
            ? Mathf.Abs(delta.x)
            : Mathf.Abs(delta.z);

        if (dist > bomb.blastRadius)
            return false;

        // sprawdzamy czy coś nie blokuje wybuchu (solid wall)
        Vector3 dir = delta.normalized;
        for (int i = 1; i < dist; i++)
        {
            Vector3 p = bombPos + dir * i;
            Collider[] cols = Physics.OverlapBox(
                new Vector3(Mathf.Round(p.x), 0.5f, Mathf.Round(p.z)),
                new Vector3(0.4f, 0.4f, 0.4f)
            );

            foreach (var c in cols)
            {
                if (c.CompareTag("WallSolid"))
                    return false;
            }
        }

        return true;
    }


    public void OnBombExploded(Bomb bomb)
    {
        if (bomb == this.lastPlacedBomb)
        {
            this.bombPlacedByMe = false;
            this.lastPlacedBomb = null;
            this.prevDistanceFromBomb = 0f;
        }
    }

    Vector2Int MyGridPos()
    {
        return new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.z)
        );
    }

    bool IsWalkable(Vector2Int cell)
    {
        Vector3 pos = new Vector3(cell.x, 0.5f, cell.y);
        return CanMoveTo(pos);
    }

    bool IsCellInAnyBombBlast(Vector2Int cell)
    {
        Vector3 pos = new Vector3(cell.x, transform.position.y, cell.y);

        Collider[] cols = Physics.OverlapSphere(pos, 0.2f);
        foreach (var c in cols)
        {
            if (!c.CompareTag(BOMB_OBJECT_TAG)) continue;
            Bomb b = c.GetComponent<Bomb>();
            if (b != null && IsInBombBlast(b))
                return true;
        }

        return false;
    }

    bool HasEscapePath(int maxDepth = 6)
    {
        Vector2Int start = MyGridPos();

        Queue<(Vector2Int pos, int depth)> q = new();
        HashSet<Vector2Int> visited = new();

        q.Enqueue((start, 0));
        visited.Add(start);

        while (q.Count > 0)
        {
            var (pos, depth) = q.Dequeue();

            if (!IsCellInAnyBombBlast(pos))
                return true;

            if (depth >= maxDepth)
                continue;

            foreach (var d in DIRS)
            {
                Vector2Int next = pos + d;
                if (visited.Contains(next)) continue;
                if (!IsWalkable(next)) continue;

                visited.Add(next);
                q.Enqueue((next, depth + 1));
            }
        }
        return false;
    }

    float DistToSafeTile(int maxDepth = 8)
    {
        Vector2Int start = MyGridPos();

        Queue<(Vector2Int pos, int depth)> q = new();
        HashSet<Vector2Int> visited = new();

        q.Enqueue((start, 0));
        visited.Add(start);

        while (q.Count > 0)
        {
            var (pos, depth) = q.Dequeue();

            if (!IsCellInAnyBombBlast(pos))
                return depth;

            if (depth >= maxDepth)
                continue;

            foreach (var d in DIRS)
            {
                Vector2Int next = pos + d;
                if (visited.Contains(next)) continue;
                if (!IsWalkable(next)) continue;

                visited.Add(next);
                q.Enqueue((next, depth + 1));
            }
        }

        return maxDepth;
    }

    int CountFreeTiles(int radius)
    {
        Vector2Int c = MyGridPos();
        int count = 0;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                Vector2Int p = c + new Vector2Int(dx, dz);
                if (IsWalkable(p))
                    count++;
            }
        }
        return count;
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // ======================
        // 1. STATUS AGENTA
        // ======================
        float obsX = 0f;
        float obsZ = 0f;

        if (arenaManager != null)
        {
            float half = Mathf.Max(1f, arenaManager.size / 2f);
            obsX = Mathf.Clamp(transform.localPosition.x / half, -1f, 1f);
            obsZ = Mathf.Clamp(transform.localPosition.z / half, -1f, 1f);
        }

        sensor.AddObservation(obsX);
        sensor.AddObservation(obsZ);
        sensor.AddObservation(isMoving ? 1f : 0f);
        sensor.AddObservation(canPlaceBomb ? 1f : 0f);
        sensor.AddObservation(agentIndex / 1f);

        // ======================
        // 2. SIATKA 5x5 ŚCIAN
        // ======================
        for (int dz = 2; dz >= -2; dz--)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                Vector3 pos = transform.position + new Vector3(dx, 0, dz);
                pos.x = Mathf.Round(pos.x);
                pos.z = Mathf.Round(pos.z);
                pos.y = 0.5f;

                float val = 0f;

                Collider[] cols = Physics.OverlapBox(
                    pos,
                    new Vector3(0.45f, 0.45f, 0.45f),
                    Quaternion.identity
                );

                foreach (var c in cols)
                {
                    if (c.CompareTag(SOLID_OBJECT_TAG)) val = -1f;
                    else if (c.CompareTag(BREAKABLE_OBJECT_TAG)) val = 1.0f;
                    else if (c.CompareTag(BOMB_OBJECT_TAG)) val = 0.5f;
                }

                sensor.AddObservation(val);
            }
        }

        // ======================
        // 3. BOMBY (NAJBLIŻSZE)
        // ======================
        Collider[] nearby = Physics.OverlapSphere(transform.position, bombObserveRadius);
        List<Bomb> bombs = new List<Bomb>();

        foreach (var c in nearby)
        {
            if (c.CompareTag(BOMB_OBJECT_TAG))
            {
                Bomb b = c.GetComponent<Bomb>();
                if (b != null) bombs.Add(b);
            }
        }

        bombs.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position))
        );

        for (int i = 0; i < bombObservationCount; i++)
        {
            if (i < bombs.Count)
            {
                Bomb b = bombs[i];
                float dist = Vector3.Distance(transform.position, b.transform.position);
                float distNorm = Mathf.Clamp01(dist / bombObserveRadius);
                float fuseNorm = b.GetRemainingFuseNormalized();

                bool isOwner = (b.GetOwner() == this);

                // czy bomba zagraża (linia + promień)
                bool threatening = IsBombThreatening(b);

                sensor.AddObservation(distNorm);
                sensor.AddObservation(fuseNorm);
                sensor.AddObservation(isOwner ? 1f : 0f);
                sensor.AddObservation(threatening ? 1f : 0f);
            }
            else
            {
                // padding
                sensor.AddObservation(1f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }

        // ======================
        // 4. PRZECIWNICY
        // ======================
        RefreshEnemies();
        enemies.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position))
        );

        for (int i = 0; i < enemyObservationCount; i++)
        {
            if (i < enemies.Count)
            {
                Vector3 delta = enemies[i].transform.position - transform.position;
                float half = arenaManager != null ? arenaManager.size / 2f : 10f;

                sensor.AddObservation(Mathf.Clamp(delta.x / half, -1f, 1f));
                sensor.AddObservation(Mathf.Clamp(delta.z / half, -1f, 1f));
                sensor.AddObservation(
                    Mathf.Clamp01(Vector3.Distance(transform.position, enemies[i].transform.position) / half)
                );
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
            }
        }

        sensor.AddObservation(HasEscapePath() ? 1f : 0f);
        sensor.AddObservation(DistToSafeTile() / 7f);   // normalizacja
        sensor.AddObservation(CountFreeTiles(2) / 25f); // (2r+1)^2
        sensor.AddObservation(CountFreeTiles(3) / 49f);

        for (int i = 0; i < 16; i++)
        {
            sensor.AddObservation(0f);
        }
    }

    private bool IsBombThreatening(Bomb bomb)
    {
        Vector3 dir = bomb.transform.position - transform.position;
        dir.y = 0f;

        // tylko linie proste
        if (Mathf.Abs(dir.x) > 0.1f && Mathf.Abs(dir.z) > 0.1f)
            return false;

        float dist = dir.magnitude;
        if (dist > bomb.blastRadius)
            return false;

        return true;
    }


    // === 2. ODBIÓR AKCJI OD AI (DECYZJI) ===
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (this.isMoving)
        {
            return;
        }
        int num;
        if (this.isDummy)
        {
            float value = Random.value;
            if (value < 0.15f)
            {
                num = 5;
            }
            else if (value < 0.2f)
            {
                num = 0;
            }
            else
            {
                num = Random.Range(1, 5);
            }
        }
        else
        {
            num = actions.DiscreteActions[0];
        }
        Vector3 b = Vector3.zero;
        bool flag = false;
        if (num == 1)
        {
            b = Vector3.forward;
            flag = true;
        }
        else if (num == 2)
        {
            b = Vector3.right;
            flag = true;
        }
        else if (num == 3)
        {
            b = Vector3.back;
            flag = true;
        }
        else if (num == 4)
        {
            b = Vector3.left;
            flag = true;
        }
        else if (num == 5)
        {
            this.PlaceBomb();
        }


        if (flag)
        {
            Vector3 vector = base.transform.position + b;
            Vector3 position = new Vector3(Mathf.Round(vector.x), base.transform.position.y, Mathf.Round(vector.z));
            if (this.CanMoveTo(position))
            {
                this.targetPosition = position;
                this.isMoving = true;

                Vector2Int cell = new Vector2Int(
                    Mathf.RoundToInt(targetPosition.x),
                    Mathf.RoundToInt(targetPosition.z)
                );

                if (!visitedCells.Contains(cell))
                {
                    visitedCells.Add(cell);
                    AddReward(0.005f);
                }

                return;
            }
            if (!this.isDummy)
            {
                base.AddReward(WALL_HIT_PENALTY);
            }
        }

        //AddReward(-0.0005f);
    }

    // === 3. RESET EPIZODU ===
    public override void OnEpisodeBegin()
    {
        this.prevDistToEnemy = float.MaxValue;
        visitedCells = new HashSet<Vector2Int>();

    }


    // Metoda Die() jest teraz kluczowa dla RL
    public void Die()
    {
        //Debug.Log(gameObject.name + " zginął! Epizod dla tego agenta zakończony.");
        //AddReward(-1.0f);
        //if (!this.isAlive)
        //{
        //    return;
        //}
        //this.isAlive = false;
        //base.AddReward(-2.0f);

        EndEpisode();

        if (arenaManager != null)
        {
            // Poinformuj Managera o śmierci (linia 139)
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
        //if (!canPlaceBomb || isDummy) return;
        if (!canPlaceBomb) return;

        Vector3 vector = new Vector3(Mathf.Round(base.transform.position.x), 0.5f, Mathf.Round(base.transform.position.z));
        Collider[] array = Physics.OverlapSphere(vector, 0.4f);
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].CompareTag("Bomb"))
            {
                base.AddReward(-0.02f);
                return;
            }
        }

        if (bombPrefab != null)
        {
            this.maxBombs--;
            this.canPlaceBomb = (this.maxBombs > 0);

            GameObject newBomb = Instantiate(bombPrefab, new Vector3(Mathf.Round(transform.position.x), 0.5f, Mathf.Round(transform.position.z)), Quaternion.identity);
            Bomb bombScript = newBomb.GetComponent<Bomb>();
            if (bombScript != null)
            {
                // Przekazujemy referencję do agenta, aby bomba mogła go powiadomić
                bombScript.SetOwner(this);

                bool targetFound = CheckForTargetWalls(transform.position, bombScript.blastRadius);
                if (targetFound)
                {
                    AddReward(0.02f);
                }

                bool enemyFound = CheckForEnemyTargets(transform.position, bombScript.blastRadius);
                if (enemyFound)
                {
                    AddReward(0.02f);
                }
                if (!targetFound && !enemyFound)
                {
                    //AddReward(-0.01f);
                }

                this.lastPlacedBomb = bombScript;
                this.bombPlacedByMe = true;
            }
        }
    }

    private bool CheckForEnemyTargets(Vector3 bombCenter, int radius)
    {
        foreach (Vector3 a in new Vector3[]
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left
        })
        {
            for (int j = 1; j <= radius; j++)
            {
                Vector3 vector = bombCenter + a * (float)j;
                Vector3 center = new Vector3(Mathf.Round(vector.x), 0.5f, Mathf.Round(vector.z));
                Vector3 halfExtents = new Vector3(0.45f, 0.45f, 0.45f);
                foreach (Collider collider in Physics.OverlapBox(center, halfExtents, Quaternion.identity))
                {
                    if (collider.CompareTag("Player"))
                    {
                        PommermanAgent component = collider.GetComponent<PommermanAgent>();
                        if (component != null && component != this)
                        {
                            //Debug.Log("Wykryto przeciwnika " + component.name + " w zasięgu bomby!");
                            return true;
                        }
                    }
                    if (collider.CompareTag("WallSolid"))
                    {
                        goto IL_11E;
                    }
                }
            }
        IL_11E:;
        }
        return false;
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

                // 1) Sprawdź bez maski — zobacz co zwraca
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
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public void NotifyBombExploded()
    {
        // Odblokuj możliwość postawienia nowej bomby
        canPlaceBomb = true;
        maxBombs++;
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

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            discreteActions[0] = 1;
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            discreteActions[0] = 2;
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            discreteActions[0] = 3;
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActions[0] = 4;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            discreteActions[0] = 5;
        }
    }
}
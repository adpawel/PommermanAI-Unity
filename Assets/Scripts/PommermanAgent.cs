using UnityEngine;

public class PommermanAgent : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Gameplay Settings")]
    public GameObject bombPrefab;

    [Header("State")]
    public bool isAlive = true;

    // movement internals
    Vector3 targetPosition;
    bool isMoving;

    // bomb internals
    bool canPlaceBomb = true;

    // references
    ArenaManager arenaManager;

    // AI brain
    RuleBased brain;

    void Start()
    {
        // snap to grid at start
        targetPosition = RoundToGrid(transform.position);
        transform.position = targetPosition;

        brain = new RuleBased(this);
    }

    void FixedUpdate()
    {
        if (!isAlive) return;

        // decide only when not in the middle of moving
        if (!isMoving)
            brain.Tick();

        MoveStep();
    }

    void MoveStep()
    {
        if (!isMoving) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.fixedDeltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }

    // ===== API for RuleBasedBrain =====

    public void MoveTo(Vector3 pos)
    {
        if (!isAlive) return;
        if (isMoving) return;

        Vector3 rounded = RoundToGrid(pos);
        if (!CanMoveTo(rounded)) return;

        targetPosition = rounded;
        isMoving = true;
    }

    public bool CanMoveTo(Vector3 pos)
    {
        Vector3 check = RoundToGrid(pos);
        check.y = 0.5f;

        Collider[] hits = Physics.OverlapBox(check, Vector3.one * 0.45f);

        foreach (Collider c in hits)
        {
            if (c == null) continue;

            // allow own collider
            if (c.gameObject == gameObject) continue;

            // allow triggers
            if (c.isTrigger) continue;

            // block by walls and bombs
            if (c.CompareTag("WallSolid") ||
                c.CompareTag("WallBreakable") ||
                c.CompareTag("Bomb"))
            {
                return false;
            }
        }
        return true;
    }

    public void PlaceBomb()
    {
        if (!isAlive) return;
        if (!canPlaceBomb) return;
        if (bombPrefab == null) return;

        // Don't place bomb if already a bomb on tile
        Vector3 bombPos = new Vector3(
            Mathf.Round(transform.position.x),
            0.5f,
            Mathf.Round(transform.position.z)
        );

        Collider[] hits = Physics.OverlapBox(bombPos, Vector3.one * 0.45f);
        foreach (Collider c in hits)
            if (c != null && c.CompareTag("Bomb"))
                return;

        canPlaceBomb = false;

        GameObject bomb = Instantiate(bombPrefab, bombPos, Quaternion.identity);

        Bomb b = bomb.GetComponent<Bomb>();
        if (b != null)
            b.SetOwner(this);
    }

    public void NotifyBombExploded()
    {
        canPlaceBomb = true;
    }

    // ===== Death handling =====

    public void Die()
    {
        if (!isAlive) return;

        isAlive = false;
        isMoving = false;

        // Report to arena manager (your existing flow)
        if (arenaManager != null)
            arenaManager.OnAgentDied(this);
        else
            Debug.LogWarning($"{name}: arenaManager is null, cannot report death.");
    }

    // ArenaManager calls this after instantiate
    public void SetArenaManager(ArenaManager manager)
    {
        arenaManager = manager;
    }

    Vector3 RoundToGrid(Vector3 v)
    {
        return new Vector3(
            Mathf.Round(v.x),
            v.y,
            Mathf.Round(v.z)
        );
    }
}

using UnityEngine;
using System.Collections;

public class Bomb2v2 : MonoBehaviour
{
    // Konfigurowalne parametry
    public float fuseTime = 2.0f;
    public int blastRadius = 2;
    public GameObject explosionPrefab;
    public LayerMask explosionMask;
    private PommermanAgent2v2 ownerAgent;
    private float fuseTimer;

    private const float BLOCK_DESTROYED_REWARD = 0.2f;
    private const float SUICIDE_PENALTY = -2.5f;
    private const float KILL_OPPONENT_REWARD = 2.0f;
    private const float WIN_REWARD = 3.0f;
    private ArenaManager2v2 arenaManager;
    public bool damageOwner = true;


    void Start()
    {
        // Rozpocznij odliczanie
        arenaManager = FindAnyObjectByType<ArenaManager2v2>();
        explosionMask = LayerMask.GetMask("Player", "WallBreakable", "WallSolid", "Destructible");
        fuseTimer = fuseTime;
        StartCoroutine(DetonateAfterDelay());
    }

    IEnumerator DetonateAfterDelay()
    {
        // Poczekaj na czas tykniêcia
        yield return new WaitForSeconds(fuseTime);

        // Wywo³aj funkcjê wybuchu
        Explode();

        // Usuñ obiekt bomby
        Destroy(gameObject);
    }

    void Explode()
    {
        Vector3 center = transform.position;
        center.y = 0.5f; // Upewnij siê, ¿e promieñ jest na poziomie obiektów

        CheckSinglePoint(center);
        CheckDirection(center, Vector3.forward);
        CheckDirection(center, Vector3.back);
        CheckDirection(center, Vector3.right);
        CheckDirection(center, Vector3.left);

        if (ownerAgent != null)
        {
            ownerAgent.NotifyBombExploded();
            this.ownerAgent.OnBombExploded(this);
        }
    }

    private void CheckSinglePoint(Vector3 checkPos)
    {
        if (this.explosionPrefab != null)
        {
            Object.Instantiate<GameObject>(this.explosionPrefab, checkPos, Quaternion.identity);
        }
        foreach (Collider collider in Physics.OverlapBox(checkPos, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, this.explosionMask))
        {
            if (collider.CompareTag("Player"))
            {
                PommermanAgent2v2 component = collider.GetComponent<PommermanAgent2v2>();
                if (component != null)
                {
                    if (!this.damageOwner && component == this.ownerAgent)
                    {
                        this.ownerAgent.AddReward(-1f);
                        this.ownerAgent.EndEpisode();
                    }
                    else
                    {
                        if (component.teamId != ownerAgent.teamId)
                        {
                            ownerAgent.AddReward(KILL_OPPONENT_REWARD);
                        }
                        else
                        {
                            ownerAgent.AddReward(-2.0f);
                        }
                        component.Die();
                    }
                }
            }
        }
    }

    private void CheckDirection(Vector3 startPos, Vector3 direction)
    {
        for (int i = 1; i <= this.blastRadius; i++)
        {
            Vector3 vector = startPos + direction * (float)i;
            vector.x = Mathf.Round(vector.x);
            vector.z = Mathf.Round(vector.z);
            if (this.explosionPrefab != null)
            {
                Object.Instantiate<GameObject>(this.explosionPrefab, vector, Quaternion.identity);
            }
            Collider[] array = Physics.OverlapBox(vector, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, this.explosionMask);
            bool flag = false;
            foreach (Collider collider in array)
            {
                if (collider.CompareTag("WallSolid"))
                {
                    flag = true;
                    break;
                }
                if (collider.CompareTag("WallBreakable"))
                {
                    Object.Destroy(collider.gameObject);
                    if (this.ownerAgent != null)
                    {
                        this.ownerAgent.AddReward(BLOCK_DESTROYED_REWARD);
                    }
                    if (this.arenaManager != null)
                    {
                        this.arenaManager.OnBreakableDestroyed(this.ownerAgent);
                    }
                    else
                    {
                        Debug.LogWarning("ArenaManager not found when notifying breakable destruction.");
                    }
                    flag = true;
                    break;
                }
                if (collider.CompareTag("Player"))
                {
                    PommermanAgent2v2 component = collider.GetComponent<PommermanAgent2v2>();
                    if (component != null)
                    {
                        if (!this.damageOwner && component == this.ownerAgent)
                        {
                            this.ownerAgent.AddReward(-1f);
                            this.ownerAgent.EndEpisode();
                        }
                        else
                        {
                            if (component.teamId != ownerAgent.teamId)
                            {
                                ownerAgent.AddReward(KILL_OPPONENT_REWARD);
                            }
                            else
                            {
                                ownerAgent.AddReward(-2.0f);
                            }
                            component.Die();
                        }
                    }
                }
            }
            if (flag)
            {
                break;
            }
        }
    }
    public void SetOwner(PommermanAgent2v2 agent)
    {
        ownerAgent = agent;
    }

    public PommermanAgent2v2 GetOwner()
    {
        return ownerAgent;
    }

    void Update()
    {
        fuseTimer = Mathf.Max(0f, fuseTimer - Time.deltaTime);
    }

    public float GetRemainingFuseNormalized()
    {
        if (fuseTime <= 0.0001f) return 0f;
        return Mathf.Clamp01(fuseTimer / fuseTime);
    }
}
using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour
{
    // Konfigurowalne parametry
    public float fuseTime = 3.0f;
    public int blastRadius = 2;
    public GameObject explosionPrefab;
    public LayerMask explosionMask;
    private PommermanAgent ownerAgent;
    private float fuseTimer;

    private const float BLOCK_DESTROYED_REWARD = 0.6f;
    private const float SUICIDE_PENALTY = -1.5f;
    private const float KILL_OPPONENT_REWARD = 1.2f;
    private const float WIN_REWARD = 3.0f;
    private ArenaManager arenaManager;

    void Start()
    {
        // Rozpocznij odliczanie
        arenaManager = FindAnyObjectByType<ArenaManager>();
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
        }
    }

    void CheckSinglePoint(Vector3 checkPos)
    {
        // Tworzenie wizualizacji eksplozji w danym punkcie
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, checkPos, Quaternion.identity);
        }

        // Sprawdzanie kolizji na polu
        Collider[] colliders = Physics.OverlapBox(checkPos, new Vector3(0.40f, 0.40f, 0.40f), Quaternion.identity, explosionMask);

        foreach (Collider col in colliders)
        {
            // Na centralnym polu NIE ma WallSolid ani WallBreakable (bo je ju¿ rozbi³eœ lub ich tam nie by³o, 
            // jeœli gracze nie ruszali siê poza granice)
            if (col.CompareTag("Player"))
            {
                PommermanAgent player = col.GetComponent<PommermanAgent>();
                if (player != null)
                {
                    if (player != ownerAgent)
                    {
                        // === NAGRODA ZA ZABÓJSTWO ===
                        if (ownerAgent != null)
                        {
                            //ownerAgent.AddReward(KILL_OPPONENT_REWARD);
                        }
                    }
                    else
                    {
                        // KARA ZA SAMOBÓJSTWO (opcjonalna, ale dobra)
                        if (ownerAgent != null)
                        {
                            ownerAgent.AddReward(SUICIDE_PENALTY);
                        }
                    }

                    player.Die();
                }
            }
        }
    }

    void CheckDirection(Vector3 startPos, Vector3 direction)
    {
        for (int i = 1; i <= blastRadius; i++)
        {
            Vector3 checkPos = startPos + direction * i;
            // Zaokr¹glenie, aby celowaæ dok³adnie w œrodek siatki
            checkPos.x = Mathf.Round(checkPos.x);
            checkPos.z = Mathf.Round(checkPos.z);

            // Tworzenie wizualizacji eksplozji w danym punkcie
            if (explosionPrefab != null)
            {
                // Instancja wybuchu, która po chwili sama siê niszczy
                Instantiate(explosionPrefab, checkPos, Quaternion.identity);
            }

            // U¿ycie Raycast lub OverlapBox, ale dla siatki najlepsze jest
            // BoxCast lub OverlapBox na precyzyjnym punkcie
            Collider[] colliders = Physics.OverlapBox(checkPos, new Vector3(0.40f, 0.40f, 0.40f), Quaternion.identity, explosionMask);

            bool hitSolid = false;
            foreach (Collider col in colliders)
            {
                if (col.CompareTag("WallSolid"))
                {
                    hitSolid = true; // Zatrzymuje eksplozjê
                    break;
                }
                else if (col.CompareTag("WallBreakable"))
                {
                    Destroy(col.gameObject); // Niszczy niszczalny mur
                    
                    if (ownerAgent != null)
                    {
                        ownerAgent.AddReward(BLOCK_DESTROYED_REWARD); // NAGRODA ZA ZNISZCZENIE MURU
                    }

                    if (arenaManager != null)
                    {
                        arenaManager.OnBreakableDestroyed(ownerAgent);
                    }
                    else
                    {
                        // Alternatywnie: spróbuj znaleŸæ inn¹ drog¹ (FindObjectOfType). Loguj dla debugu.
                        Debug.LogWarning("ArenaManager not found when notifying breakable destruction.");
                    }

                    hitSolid = true; // Zatrzymuje eksplozjê
                    break;
                }
                else if (col.CompareTag("Player"))
                {
                    PommermanAgent player = col.GetComponent<PommermanAgent>();
                    if (player != null)
                    {
                        if (player != ownerAgent)
                        {
                            // === NAGRODA ZA ZABÓJSTWO ===
                            if (ownerAgent != null)
                            {
                                //ownerAgent.AddReward(KILL_OPPONENT_REWARD);
                            }
                        }
                        else
                        {
                            // KARA ZA SAMOBÓJSTWO (opcjonalna, ale dobra)
                            if (ownerAgent != null)
                            {
                                ownerAgent.AddReward(SUICIDE_PENALTY);
                            }
                        }

                        player.Die();
                    }
                }
            }

            if (hitSolid)
            {
                break;
            }
        }
    }
    public void SetOwner(PommermanAgent agent)
    {
        ownerAgent = agent;
    }

    public PommermanAgent GetOwner()
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
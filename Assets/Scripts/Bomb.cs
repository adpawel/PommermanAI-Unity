using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour
{
    public float TimeToExplode { get; private set; }
    // Konfigurowalne parametry
    public float fuseTime = 3.0f; // Czas do wybuchu
    public int blastRadius = 2; // Zasig eksplozji (liczba pl)
    public GameObject explosionPrefab; // Wizualny efekt wybuchu (stwrz go jako nowy Prefab)
    public LayerMask explosionMask;
    private PommermanAgent ownerAgent;

    void Start()
    {
        // Rozpocznij odliczanie
        explosionMask = LayerMask.GetMask("Player", "WallDestroyable", "WallSolid", "Destructible");

        TimeToExplode = fuseTime;
        StartCoroutine(DetonateAfterDelay());
    }

    void Update()
    {
        TimeToExplode -= Time.deltaTime;
    }


    IEnumerator DetonateAfterDelay()
    {
        // Poczekaj na czas tyknicia
        yield return new WaitForSeconds(fuseTime);

        // Wywoaj funkcj wybuchu
        Explode();

        // Usu obiekt bomby
        Destroy(gameObject);
    }

    void Explode()
    {
        Vector3 center = transform.position;
        center.y = 0.6f; // Upewnij si, e promie jest na poziomie obiektw

        CheckSinglePoint(center);
        // Sprawdzenie w 4 kierunkach (North, South, East, West)
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
            // Na centralnym polu NIE ma WallSolid ani WallBreakable (bo je ju rozbie lub ich tam nie byo,
            // jeli gracze nie ruszali si poza granice)
            if (col.CompareTag("Player"))
            {
                PommermanAgent player = col.GetComponent<PommermanAgent>();
                if (player != null)
                {
                    player.Die(); // Zabija gracza, jeli stoi na bombie
                }
            }
            // Moesz te doda logik niszczenia bonusw, jeli ju je zaimplementowae.
        }
        // Usu sam bomb, ktra jest wanie na tym polu.
        // Uwaga: Destroy(gameObject); na kocu DetonateAfterDelay() ju to robi.
    }

    void CheckDirection(Vector3 startPos, Vector3 direction)
    {
        for (int i = 1; i <= blastRadius; i++)
        {
            Vector3 checkPos = startPos + direction * i;
            // Zaokrglenie, aby celowa dokadnie w rodek siatki
            checkPos.x = Mathf.Round(checkPos.x);
            checkPos.z = Mathf.Round(checkPos.z);

            // Tworzenie wizualizacji eksplozji w danym punkcie
            if (explosionPrefab != null)
            {
                // Instancja wybuchu, ktra po chwili sama si niszczy
                Instantiate(explosionPrefab, checkPos, Quaternion.identity);
            }

            // Uycie Raycast lub OverlapBox, ale dla siatki najlepsze jest
            // BoxCast lub OverlapBox na precyzyjnym punkcie
            Collider[] colliders = Physics.OverlapBox(checkPos, new Vector3(0.40f, 0.40f, 0.40f), Quaternion.identity, explosionMask);

            bool hitSolid = false;
            foreach (Collider col in colliders)
            {
                if (col.CompareTag("WallSolid"))
                {
                    hitSolid = true; // Zatrzymuje eksplozj
                    break;
                }
                else if (col.CompareTag("WallBreakable"))
                {
                    Destroy(col.gameObject); // Niszczy niszczalny mur
                    hitSolid = true; // Zatrzymuje eksplozj
                    break;
                }
                else if (col.CompareTag("Player"))
                {
                    PommermanAgent player = col.GetComponent<PommermanAgent>();
                    if (player != null)
                    {
                        player.Die();
                    }
                }
            }

            if (hitSolid)
            {
                break; // Zatrzymuje promie po napotkaniu muru
            }
        }
    }
    public void SetOwner(PommermanAgent agent)
    {
        ownerAgent = agent;
    }
}
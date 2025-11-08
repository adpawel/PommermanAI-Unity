using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour
{
    // Konfigurowalne parametry
    public float fuseTime = 3.0f; // Czas do wybuchu
    public int blastRadius = 2;   // Zasiêg eksplozji (liczba pól)
    public GameObject explosionPrefab; // Wizualny efekt wybuchu (stwórz go jako nowy Prefab)
    public LayerMask explosionMask;
    private PommermanAgent ownerAgent;

    void Start()
    {
        // Rozpocznij odliczanie
        explosionMask = LayerMask.GetMask("Player", "WallDestroyable", "WallSolid", "Destructible");
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
        center.y = 0.6f; // Upewnij siê, ¿e promieñ jest na poziomie obiektów

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
            // Na centralnym polu NIE ma WallSolid ani WallBreakable (bo je ju¿ rozbi³eœ lub ich tam nie by³o, 
            // jeœli gracze nie ruszali siê poza granice)
            if (col.CompareTag("Player"))
            {
                PommermanAgent player = col.GetComponent<PommermanAgent>();
                if (player != null)
                {
                    player.Die(); // Zabija gracza, jeœli stoi na bombie
                }
            }
            // Mo¿esz te¿ dodaæ logikê niszczenia bonusów, jeœli ju¿ je zaimplementowa³eœ.
        }
        // Usuñ sam¹ bombê, która jest w³aœnie na tym polu.
        // Uwaga: Destroy(gameObject); na koñcu DetonateAfterDelay() ju¿ to robi.
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
                    hitSolid = true; // Zatrzymuje eksplozjê
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
                break; // Zatrzymuje promieñ po napotkaniu muru
            }
        }
    }
    public void SetOwner(PommermanAgent agent)
    {
        ownerAgent = agent;
    }
}
using UnityEngine;

public class SelfDestruct : MonoBehaviour
{
    // Publiczna zmienna do ustawienia czasu ¿ycia w Inspektorze
    public float lifetime = 0.5f;

    void Start()
    {
        // Wywo³uje metodê Destroy na tym obiekcie gry po up³ywie 'lifetime' sekund.
        Destroy(gameObject, lifetime);
    }
}
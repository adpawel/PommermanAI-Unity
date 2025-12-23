using UnityEngine;

public class SelfDestruct : MonoBehaviour
{
  // Publiczna zmienna do ustawienia czasu ycia w Inspektorze
  public float lifetime = 0.5f;

    void Start()
    {
    // Wywouje metod Destroy na tym obiekcie gry po upywie 'lifetime' sekund.
    Destroy(gameObject, lifetime);
    }
}
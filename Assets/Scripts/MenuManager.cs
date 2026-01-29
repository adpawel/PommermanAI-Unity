using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void LoadMenu()
    {
        SceneManager.LoadScene("Menu");
    }
    public void Start1v1()
    {
        SceneManager.LoadScene("Game_1v1");
    }

    public void Start2v2()
    {
        SceneManager.LoadScene("Game_2v2");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}


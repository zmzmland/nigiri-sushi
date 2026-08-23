using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class WaitSceneManager : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(3f);

        SceneManager.LoadScene("Game Scene 2");
    }
}
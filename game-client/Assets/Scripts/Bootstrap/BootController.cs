using QuizBattle.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Boot scene's only job: make sure AppRoot exists (it auto-bootstraps before this even
/// runs, but touching Instance here keeps the dependency explicit) then move on.
public class BootController : MonoBehaviour
{
    private void Start()
    {
        _ = AppRoot.Instance;
        SceneManager.LoadScene("Connect");
    }
}

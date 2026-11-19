using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
namespace Raid.UI.Editor
{
    // FishNet/Steam transport sessions are not hot-reloadable. End this isolated
    // Play session before compilation rather than letting a partial domain run.
    [InitializeOnLoad]
    public static class UiPlayCompileGuard
    {
        static UiPlayCompileGuard() { CompilationPipeline.compilationStarted+=OnCompilation; }
        static void OnCompilation(object context)
        {
            if(!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=UiAuthoring.ScenePath)return;
            Debug.Log("[RAID011] Ending UI Play session before script compilation. Restart Play after compilation completes.");
            EditorApplication.isPlaying=false;
        }
    }
}

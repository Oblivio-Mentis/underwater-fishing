using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderwaterFishing.World
{
    public static class DevelopmentGym
    {
        public static bool Active => (Application.isEditor || Debug.isDebugBuild) &&
            (SceneManager.GetActiveScene().name.EndsWith("Gym", StringComparison.Ordinal)
            || Array.IndexOf(Environment.GetCommandLineArgs(), "--raid-gym") >= 0);
    }
}

using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// Lives in the generated M1 scene. A scene reload (restart) recreates a clean GameRoot.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Start (not Awake) guarantees a scene reload has already removed the previous
            // GameRoot and its cameras/audio listener before the new root is composed.
            GameRoot existing = FindObjectOfType<GameRoot>();
            if (existing != null)
                return;
            GameObject root = new GameObject("GameRoot");
            root.AddComponent<GameRoot>();
        }
    }
}

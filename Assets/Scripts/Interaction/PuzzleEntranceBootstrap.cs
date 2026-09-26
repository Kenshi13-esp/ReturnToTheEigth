using System.Collections.Generic;
using ReturnToTheEigth.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>
    /// Opt-in helper that attaches and configures <see cref="PuzzleAssetInteractable"/> on registered world assets.
    /// It never runs automatically: call <see cref="EnableAutomaticSetup"/> explicitly if a project wants asset-based entrances.
    /// </summary>
    public static class PuzzleEntranceBootstrap
    {
        private static readonly PuzzleEntranceDefinition[] DefaultEntrances = System.Array.Empty<PuzzleEntranceDefinition>();

        private static bool isSubscribed;

        /// <summary>Starts configuring registered entrances on every scene load and on the scenes already loaded.</summary>
        public static void EnableAutomaticSetup()
        {
            if (!isSubscribed)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                SceneManager.sceneLoaded += HandleSceneLoaded;
                isSubscribed = true;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                ApplyToScene(SceneManager.GetSceneAt(index));
            }
        }

        /// <summary>Stops configuring entrances on scene load.</summary>
        public static void DisableAutomaticSetup()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            isSubscribed = false;
        }

        /// <summary>Configures every registered puzzle entrance found in the supplied scene; safe to call more than once.</summary>
        public static void ApplyToScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            PlayerInteraction player = Object.FindAnyObjectByType<PlayerInteraction>();
            foreach (PuzzleEntranceDefinition definition in GetEntrances())
            {
                GameObject asset = FindInScene(scene, definition.assetName);
                if (asset != null)
                {
                    EnsureEntrance(asset, definition, player);
                }
            }
        }

        /// <summary>Makes the supplied asset a puzzle entrance using the given definition.</summary>
        public static PuzzleAssetInteractable EnsureEntrance(
            GameObject asset, PuzzleEntranceDefinition definition, PlayerInteraction player)
        {
            PuzzleAssetInteractable entrance = asset.GetComponent<PuzzleAssetInteractable>();
            if (entrance == null)
            {
                entrance = asset.AddComponent<PuzzleAssetInteractable>();
            }

            entrance.Configure(definition.sceneToLoad, definition.puzzleId, player);
            if (!string.IsNullOrWhiteSpace(definition.interactionPrompt))
            {
                entrance.SetInteractionPrompt(definition.interactionPrompt);
            }

            return entrance;
        }

        private static IEnumerable<PuzzleEntranceDefinition> GetEntrances()
        {
            PuzzleEntranceRegistry registry = Resources.Load<PuzzleEntranceRegistry>(PuzzleEntranceRegistry.ResourceName);
            if (registry != null)
            {
                foreach (PuzzleEntranceDefinition entry in registry.Entries)
                {
                    yield return entry;
                }
            }

            foreach (PuzzleEntranceDefinition entry in DefaultEntrances)
            {
                if (registry == null || !registry.TryGetEntry(entry.assetName, out _))
                {
                    yield return entry;
                }
            }
        }

        private static GameObject FindInScene(Scene scene, string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = FindInHierarchy(root.transform, assetName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindInHierarchy(Transform current, string assetName)
        {
            if (current.name == assetName)
            {
                return current;
            }

            for (int index = 0; index < current.childCount; index++)
            {
                Transform match = FindInHierarchy(current.GetChild(index), assetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToScene(scene);
        }
    }
}

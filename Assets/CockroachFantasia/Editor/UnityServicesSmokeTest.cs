using System;
using CockroachFantasia.App;
using UnityEditor;
using UnityEngine;

namespace CockroachFantasia.Editor
{
    [InitializeOnLoad]
    public static class UnityServicesSmokeTest
    {
        private const string PendingKey = "CockroachFantasia.UnityServicesSmokePending";

        static UnityServicesSmokeTest()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
        }

        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
            {
                return;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            RunInPlayMode();
        }

        private static async void RunInPlayMode()
        {
            try
            {
                var bootstrap = ServicesBootstrap.Instance;
                if (bootstrap == null)
                {
                    throw new InvalidOperationException("Services bootstrap was not created in Play Mode.");
                }

                var result = await bootstrap.InitializeAsync();
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(result.Message);
                }

                if (string.IsNullOrWhiteSpace(bootstrap.PlayerId))
                {
                    throw new InvalidOperationException("Authentication completed without a player ID.");
                }

                Debug.Log($"UNITY_SERVICES_SMOKE_SUCCESS player={bootstrap.PlayerId}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("UNITY_SERVICES_SMOKE_FAILED");
                EditorApplication.Exit(1);
            }
            finally
            {
                SessionState.EraseBool(PendingKey);
            }
        }
    }
}

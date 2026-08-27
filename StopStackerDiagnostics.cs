using UnityEngine;

namespace StopStacker
{
    internal static class StopStackerDiagnostics
    {
        private const string Prefix = "[StopStacker]";

        public static void Advanced(string message)
        {
            if (StopStackerModSettings.AdvancedDiagnostics)
                Debug.Log(Format(message));
        }

        public static void AdvancedWarning(string message)
        {
            if (StopStackerModSettings.AdvancedDiagnostics)
                Debug.LogWarning(Format(message));
        }

        public static void Info(string message)
        {
            Debug.Log(Format(message));
        }

        private static string Format(string message)
        {
            if (string.IsNullOrEmpty(message))
                return Prefix;

            return message.StartsWith(Prefix)
                ? message
                : Prefix + " " + message;
        }
    }
}

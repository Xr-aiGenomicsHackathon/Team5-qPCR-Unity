using UnityEngine;

namespace Team5.qPCR
{
    // Session-only data survives scene loads, never written to disk or sent to a service.
    public static class TrainingSession
    {
        public static string Nickname { get; private set; } = "Learner";
        public static bool PpeComplete { get; set; }
        public static float VoiceVolume { get; set; } = .8f;
        public static bool VoiceMuted { get; set; }
        public static bool SimulatorEnabled { get; set; } = true;
        public static bool TryBegin(string nickname)
        {
            var clean = (nickname ?? string.Empty).Trim();
            if (clean.Length == 0 || clean.Length > 24) return false;
            Nickname = clean;
            PpeComplete = false;
            return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear()
        {
            Nickname = "Learner"; PpeComplete = false; VoiceVolume = .8f;
            VoiceMuted = false; SimulatorEnabled = Application.isEditor;
        }
    }
}

using System;
using UnityEngine;

namespace Hollow.Core
{
    public enum GameState { Title, Intro, Fighting, Paused, Victory, Defeat }

    // Subscribe with += in OnEnable and -= in OnDisable. Never use = .
    // Append-only: never rename or delete an event once it is on main.
    public static class GameEvents
    {
        // Raised by Student A
        public static Action<int, int> PlayerHealthChanged;  // current, max masks
        public static Action<float> PlayerFocusChanged;      // 0..1
        public static Action<DamageInfo> PlayerDamaged;
        public static Action PlayerDied;
        public static Action<GameState> GameStateChanged;

        // Raised by Student B
        public static Action EncounterStarted;               // player crossed the arena door
        public static Action BossIntroFinished;
        public static Action<int, int> BossHealthChanged;    // current, max HP
        public static Action<int> BossPhaseChanged;          // 1, 2, 3
        public static Action BossStaggered;
        public static Action BossRoared;                     // mixer ducking
        public static Action BossDied;

        // Raised automatically by Hitbox on every damaging hit
        public static Action<Vector2, float> HitLanded;      // world position, strength 0..1

        // Clears stale subscribers when domain reload is disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            PlayerHealthChanged = null; PlayerFocusChanged = null; PlayerDamaged = null;
            PlayerDied = null; GameStateChanged = null; EncounterStarted = null;
            BossIntroFinished = null; BossHealthChanged = null; BossPhaseChanged = null;
            BossStaggered = null; BossRoared = null; BossDied = null; HitLanded = null;
        }
    }
}
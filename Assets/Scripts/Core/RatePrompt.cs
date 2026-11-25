namespace CargoSort.Core
{
    /// <summary>
    /// When to ask the player to rate the game and leave a comment in the store. The first ask comes after a win at
    /// <see cref="FirstLevel"/>; a player who taps "Later" is asked again <see cref="Gap"/> levels later, at most
    /// <see cref="MaxAsks"/> times. A player who rated (or tapped "No thanks") is never asked again.
    /// </summary>
    public static class RatePrompt
    {
        public const int FirstLevel = 8, Gap = 20, MaxAsks = 3;

        /// <param name="wonLevel">The level just won (0 = the daily challenge, which never asks).</param>
        /// <param name="asks">How many times the prompt was shown so far.</param>
        /// <param name="lastAskLevel">The level at which it was last shown (0 = never).</param>
        /// <param name="done">True once the player rated or said no thanks.</param>
        public static bool ShouldAsk(int wonLevel, int asks, int lastAskLevel, bool done)
        {
            if (done || wonLevel <= 0 || asks >= MaxAsks) return false;
            if (asks == 0) return wonLevel >= FirstLevel;
            return wonLevel >= lastAskLevel + Gap;
        }
    }
}

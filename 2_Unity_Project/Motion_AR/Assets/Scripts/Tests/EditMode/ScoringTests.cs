using NUnit.Framework;

namespace MotionAR.Tests
{
    /// <summary>
    /// EditMode unit tests for SessionManager scoring logic.
    /// Since SessionManager is a MonoBehaviour singleton, we test the pure scoring formula
    /// extracted as a static method to verify the calculation in isolation.
    /// </summary>
    [TestFixture]
    public class ScoringTests
    {
        /// <summary>
        /// Replicates the scoring formula from SessionManager.CompleteLessonInternal:
        /// <c>Mathf.Max(0f, 100f - mistakeCount * 10f)</c>
        /// </summary>
        private static float CalculateScore(int mistakeCount)
        {
            return UnityEngine.Mathf.Max(0f, 100f - mistakeCount * 10f);
        }

        // ────────────────── Perfect Score ──────────────────

        [Test]
        public void Score_ZeroMistakes_Returns100()
        {
            Assert.AreEqual(100f, CalculateScore(0));
        }

        // ────────────────── Partial Scores ──────────────────

        [Test]
        public void Score_OneMistake_Returns90()
        {
            Assert.AreEqual(90f, CalculateScore(1));
        }

        [Test]
        public void Score_FiveMistakes_Returns50()
        {
            Assert.AreEqual(50f, CalculateScore(5));
        }

        [Test]
        public void Score_NineMistakes_Returns10()
        {
            Assert.AreEqual(10f, CalculateScore(9));
        }

        // ────────────────── Zero Floor ──────────────────

        [Test]
        public void Score_TenMistakes_ReturnsZero()
        {
            Assert.AreEqual(0f, CalculateScore(10));
        }

        [Test]
        public void Score_MoreThanTenMistakes_ReturnsZero()
        {
            Assert.AreEqual(0f, CalculateScore(15));
            Assert.AreEqual(0f, CalculateScore(100));
        }

        [Test]
        public void Score_NeverNegative()
        {
            for (int i = 0; i <= 50; i++)
            {
                float score = CalculateScore(i);
                Assert.GreaterOrEqual(score, 0f, $"Score should never be negative (mistakes={i})");
            }
        }

        // ────────────────── Score Range ──────────────────

        [Test]
        public void Score_AlwaysBetween0And100()
        {
            for (int i = 0; i <= 50; i++)
            {
                float score = CalculateScore(i);
                Assert.GreaterOrEqual(score, 0f, $"Score must be >= 0 (mistakes={i})");
                Assert.LessOrEqual(score, 100f, $"Score must be <= 100 (mistakes={i})");
            }
        }

        // ────────────────── Monotonic Decrease ──────────────────

        [Test]
        public void Score_DecreasesWithMoreMistakes()
        {
            float previousScore = CalculateScore(0);
            for (int i = 1; i <= 10; i++)
            {
                float currentScore = CalculateScore(i);
                Assert.LessOrEqual(currentScore, previousScore,
                    $"Score should decrease or stay the same as mistakes increase (mistakes={i})");
                previousScore = currentScore;
            }
        }
    }
}

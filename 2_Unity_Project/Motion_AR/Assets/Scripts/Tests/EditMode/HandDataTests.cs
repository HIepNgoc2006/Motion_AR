using NUnit.Framework;
using UnityEngine;
using MotionAR.Interaction;

namespace MotionAR.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="HandData"/> struct.
    /// Validates immutability, defensive copying, null safety, and data integrity.
    /// </summary>
    [TestFixture]
    public class HandDataTests
    {
        // ────────────────── Constructor — Normal ──────────────────

        [Test]
        public void Constructor_ValidLandmarks_CopiesArray()
        {
            Vector3[] original = CreateLandmarks(21);
            HandData data = new HandData(original, 1.0f, true);

            Assert.AreEqual(21, data.Landmarks.Length);
            Assert.AreEqual(original[0], data.Landmarks[0]);
        }

        [Test]
        public void Constructor_PreservesTimestamp()
        {
            HandData data = new HandData(CreateLandmarks(21), 42.5f, true);
            Assert.AreEqual(42.5f, data.Timestamp);
        }

        [Test]
        public void Constructor_PreservesIsTracking()
        {
            HandData tracked = new HandData(CreateLandmarks(21), 0f, true);
            HandData untracked = new HandData(CreateLandmarks(21), 0f, false);

            Assert.IsTrue(tracked.IsTracking);
            Assert.IsFalse(untracked.IsTracking);
        }

        // ────────────────── Defensive Copy ──────────────────

        [Test]
        public void Constructor_MakesDefensiveCopy_OriginalNotMutated()
        {
            Vector3[] original = CreateLandmarks(21);
            HandData data = new HandData(original, 0f, true);

            // Mutate original array.
            original[0] = new Vector3(999f, 999f, 999f);

            // HandData should still have the original value.
            Assert.AreNotEqual(original[0], data.Landmarks[0],
                "HandData must make a defensive copy so mutations to the source don't affect it.");
        }

        // ────────────────── Null Safety ──────────────────

        [Test]
        public void Constructor_NullLandmarks_CreatesEmptyArray()
        {
            HandData data = new HandData(null, 0f, false);

            Assert.IsNotNull(data.Landmarks);
            Assert.AreEqual(0, data.Landmarks.Length);
        }

        // ────────────────── Various Sizes ──────────────────

        [Test]
        public void Constructor_EmptyLandmarks_Works()
        {
            HandData data = new HandData(new Vector3[0], 0f, true);
            Assert.AreEqual(0, data.Landmarks.Length);
        }

        [Test]
        public void Constructor_PartialLandmarks_PreservesSize()
        {
            // Some providers may send fewer than 21 landmarks.
            Vector3[] partial = CreateLandmarks(10);
            HandData data = new HandData(partial, 0f, true);
            Assert.AreEqual(10, data.Landmarks.Length);
        }

        // ────────────────── GestureType Enum ──────────────────

        [Test]
        public void GestureType_None_IsZero()
        {
            Assert.AreEqual(0, (int)GestureType.None);
        }

        [Test]
        public void GestureType_AllValuesUnique()
        {
            var values = System.Enum.GetValues(typeof(GestureType));
            var set = new System.Collections.Generic.HashSet<int>();

            foreach (var value in values)
            {
                Assert.IsTrue(set.Add((int)value),
                    $"Duplicate GestureType value: {value} = {(int)value}");
            }
        }

        // ────────────────── PinchState Enum ──────────────────

        [Test]
        public void PinchState_Released_IsZero()
        {
            Assert.AreEqual(0, (int)PinchState.Released);
        }

        [Test]
        public void PinchState_Grabbed_IsOne()
        {
            Assert.AreEqual(1, (int)PinchState.Grabbed);
        }

        // ────────────────── Helpers ──────────────────

        private static Vector3[] CreateLandmarks(int count)
        {
            Vector3[] landmarks = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                landmarks[i] = new Vector3(i * 0.01f, i * 0.02f, i * 0.001f);
            }
            return landmarks;
        }
    }
}

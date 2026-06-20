using NUnit.Framework;
using System.Collections.Generic;
using MotionAR.Data;

namespace MotionAR.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="LessonData"/> and <see cref="StepData"/>.
    /// Validates data access, boundary conditions, and null safety.
    /// </summary>
    [TestFixture]
    public class LessonDataTests
    {
        private LessonData _lesson;

        [SetUp]
        public void SetUp()
        {
            _lesson = UnityEngine.ScriptableObject.CreateInstance<LessonData>();
            _lesson.lessonID = "test_lesson";
            _lesson.lessonTitle = "Test Lesson";
            _lesson.description = "A test lesson for unit testing.";
            _lesson.assemblySteps = new List<StepData>
            {
                new StepData
                {
                    stepIndex = 0,
                    requiredPartID = "part_a",
                    snapZoneID = "snap_a",
                    instructionText = "Step 0 instruction",
                    hintText = "Step 0 hint"
                },
                new StepData
                {
                    stepIndex = 1,
                    requiredPartID = "part_b",
                    snapZoneID = "snap_b",
                    instructionText = "Step 1 instruction",
                    hintText = "Step 1 hint"
                },
                new StepData
                {
                    stepIndex = 2,
                    requiredPartID = "part_c",
                    snapZoneID = "snap_c",
                    instructionText = "Step 2 instruction",
                    hintText = ""
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_lesson != null)
            {
                UnityEngine.Object.DestroyImmediate(_lesson);
            }
        }

        // ────────────────── TotalSteps ──────────────────

        [Test]
        public void TotalSteps_ReturnsCorrectCount()
        {
            Assert.AreEqual(3, _lesson.TotalSteps);
        }

        [Test]
        public void TotalSteps_EmptyList_ReturnsZero()
        {
            _lesson.assemblySteps.Clear();
            Assert.AreEqual(0, _lesson.TotalSteps);
        }

        [Test]
        public void TotalSteps_NullList_ReturnsZero()
        {
            _lesson.assemblySteps = null;
            Assert.AreEqual(0, _lesson.TotalSteps);
        }

        // ────────────────── GetStep ──────────────────

        [Test]
        public void GetStep_ValidIndex_ReturnsCorrectStep()
        {
            StepData step = _lesson.GetStep(1);
            Assert.IsNotNull(step);
            Assert.AreEqual("part_b", step.requiredPartID);
            Assert.AreEqual("snap_b", step.snapZoneID);
            Assert.AreEqual(1, step.stepIndex);
        }

        [Test]
        public void GetStep_FirstIndex_ReturnsFirstStep()
        {
            StepData step = _lesson.GetStep(0);
            Assert.IsNotNull(step);
            Assert.AreEqual("part_a", step.requiredPartID);
        }

        [Test]
        public void GetStep_LastIndex_ReturnsLastStep()
        {
            StepData step = _lesson.GetStep(2);
            Assert.IsNotNull(step);
            Assert.AreEqual("part_c", step.requiredPartID);
        }

        [Test]
        public void GetStep_NegativeIndex_ReturnsNull()
        {
            Assert.IsNull(_lesson.GetStep(-1));
        }

        [Test]
        public void GetStep_OutOfRangeIndex_ReturnsNull()
        {
            Assert.IsNull(_lesson.GetStep(3));
            Assert.IsNull(_lesson.GetStep(100));
        }

        [Test]
        public void GetStep_NullStepsList_ReturnsNull()
        {
            _lesson.assemblySteps = null;
            Assert.IsNull(_lesson.GetStep(0));
        }

        // ────────────────── Data Integrity ──────────────────

        [Test]
        public void StepData_PreservesInstructionText()
        {
            StepData step = _lesson.GetStep(0);
            Assert.AreEqual("Step 0 instruction", step.instructionText);
        }

        [Test]
        public void StepData_PreservesHintText()
        {
            StepData step = _lesson.GetStep(0);
            Assert.AreEqual("Step 0 hint", step.hintText);
        }

        [Test]
        public void StepData_EmptyHintIsAllowed()
        {
            StepData step = _lesson.GetStep(2);
            Assert.AreEqual("", step.hintText);
        }

        [Test]
        public void LessonData_PreservesLessonID()
        {
            Assert.AreEqual("test_lesson", _lesson.lessonID);
        }

        [Test]
        public void LessonData_PreservesTitle()
        {
            Assert.AreEqual("Test Lesson", _lesson.lessonTitle);
        }
    }
}

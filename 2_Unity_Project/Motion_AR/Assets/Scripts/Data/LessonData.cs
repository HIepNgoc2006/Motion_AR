using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MotionAR.Data
{
    /// <summary>
    /// ScriptableObject representing a complete training lesson composed of sequential assembly steps.
    /// Create instances via the Unity menu: Assets → Create → AR_Training → LessonData.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLesson", menuName = "AR_Training/LessonData")]
    public class LessonData : ScriptableObject
    {
        /// <summary>
        /// Unique identifier for this lesson.
        /// </summary>
        [Tooltip("Unique identifier for this lesson.")]
        public string lessonID;

        /// <summary>
        /// Human-readable title of the lesson shown in the UI.
        /// </summary>
        [Tooltip("Human-readable title of the lesson shown in the UI.")]
        public string lessonTitle;

        /// <summary>
        /// Detailed description of what the lesson covers.
        /// </summary>
        [Tooltip("Detailed description of what the lesson covers.")]
        [TextArea(2, 4)]
        public string description;

        /// <summary>
        /// Ordered list of assembly steps the trainee must complete.
        /// </summary>
        [Tooltip("Ordered list of assembly steps the trainee must complete.")]
        public List<StepData> assemblySteps = new List<StepData>();

        /// <summary>
        /// Total number of assembly steps in this lesson.
        /// </summary>
        public int TotalSteps => assemblySteps?.Count ?? 0;

        /// <summary>
        /// Returns the <see cref="StepData"/> at the given index,
        /// or <c>null</c> if the index is out of range.
        /// </summary>
        /// <param name="index">Zero-based step index to retrieve.</param>
        /// <returns>The matching <see cref="StepData"/>, or <c>null</c>.</returns>
        public StepData GetStep(int index)
        {
            if (assemblySteps == null || index < 0 || index >= assemblySteps.Count)
            {
                return null;
            }

            return assemblySteps[index];
        }

        /// <summary>
        /// Editor callback that sorts <see cref="assemblySteps"/> by
        /// <see cref="StepData.stepIndex"/> whenever the asset is modified.
        /// </summary>
        private void OnValidate()
        {
            if (assemblySteps != null && assemblySteps.Count > 1)
            {
                assemblySteps = assemblySteps.OrderBy(s => s.stepIndex).ToList();
            }
        }
    }
}

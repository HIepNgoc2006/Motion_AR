using System;
using UnityEngine;

namespace MotionAR.Data
{
    /// <summary>
    /// Serializable data class representing a single assembly step in a training lesson.
    /// This is a plain data container — not a MonoBehaviour.
    /// </summary>
    [Serializable]
    public class StepData
    {
        /// <summary>
        /// Zero-based index defining the order of this step within the lesson.
        /// </summary>
        [Tooltip("Zero-based index defining the order of this step within the lesson.")]
        public int stepIndex;

        /// <summary>
        /// The unique identifier of the part that must be placed during this step.
        /// </summary>
        [Tooltip("The unique identifier of the part that must be placed during this step.")]
        public string requiredPartID;

        /// <summary>
        /// The identifier of the SnapZone where the part should be placed.
        /// </summary>
        [Tooltip("The identifier of the SnapZone where the part should be placed.")]
        public string snapZoneID;

        /// <summary>
        /// The instruction text displayed to the user for this step.
        /// </summary>
        [Tooltip("The instruction text displayed to the user for this step.")]
        [TextArea(2, 5)]
        public string instructionText;

        /// <summary>
        /// Optional hint text that can be consumed by the AI assistant.
        /// </summary>
        [Tooltip("Optional hint text that can be consumed by the AI assistant.")]
        [TextArea(1, 3)]
        public string hintText;
    }
}

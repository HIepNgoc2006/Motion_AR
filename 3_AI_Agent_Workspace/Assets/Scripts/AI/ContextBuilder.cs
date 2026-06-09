using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MotionAR.AI
{
    /// <summary>
    /// Static utility class that constructs a JSON payload describing the
    /// current state of the 3D AR training session. This context is sent
    /// alongside user questions to the Gemini API so the model can provide
    /// spatially-aware, step-relevant guidance.
    /// </summary>
    public static class ContextBuilder
    {
        /// <summary>
        /// Represents the status of a single snap zone in the 3D scene.
        /// </summary>
        public struct SnapZoneStatus
        {
            /// <summary>
            /// Unique identifier for the snap zone (e.g. "sz_cylinder").
            /// </summary>
            public string id;

            /// <summary>
            /// Whether a part is currently occupying this snap zone.
            /// </summary>
            public bool occupied;

            /// <summary>
            /// The identifier of the part placed in this zone, or empty if unoccupied.
            /// </summary>
            public string partId;
        }

        /// <summary>
        /// Builds a JSON string representing the full current session state.
        /// </summary>
        /// <param name="lessonId">Identifier of the active lesson (e.g. "engine_v1").</param>
        /// <param name="currentStep">The 1-based index of the current assembly step.</param>
        /// <param name="totalSteps">Total number of steps in this lesson.</param>
        /// <param name="currentInstruction">The localised instruction text for the current step.</param>
        /// <param name="heldPartId">ID of the part the user is currently holding, or null/empty.</param>
        /// <param name="mistakeCount">Number of mistakes the user has made so far.</param>
        /// <param name="snapZones">List of snap zone statuses in the scene.</param>
        /// <returns>A compact JSON string ready to be embedded in the Gemini prompt.</returns>
        public static string BuildContextJson(
            string lessonId,
            int currentStep,
            int totalSteps,
            string currentInstruction,
            string heldPartId,
            int mistakeCount,
            List<SnapZoneStatus> snapZones)
        {
            var snapZonesArray = new JArray();
            if (snapZones != null)
            {
                foreach (SnapZoneStatus zone in snapZones)
                {
                    var zoneObj = new JObject
                    {
                        ["id"] = zone.id ?? string.Empty,
                        ["occupied"] = zone.occupied,
                        ["partId"] = zone.partId ?? string.Empty
                    };
                    snapZonesArray.Add(zoneObj);
                }
            }

            var context = new JObject
            {
                ["lessonId"] = lessonId ?? string.Empty,
                ["currentStep"] = currentStep,
                ["totalSteps"] = totalSteps,
                ["currentInstruction"] = currentInstruction ?? string.Empty,
                ["heldPartId"] = heldPartId ?? string.Empty,
                ["mistakeCount"] = mistakeCount,
                ["snapZonesStatus"] = snapZonesArray
            };

            return context.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}

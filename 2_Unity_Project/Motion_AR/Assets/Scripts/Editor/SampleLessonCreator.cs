#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using MotionAR.Data;

namespace MotionAR.Editor
{
    /// <summary>
    /// Editor utility that creates sample <see cref="LessonData"/> ScriptableObject assets
    /// for testing the training system without manual data entry.
    /// <para>
    /// Accessible via menu: <c>Motion AR → Create Sample Lessons</c>.
    /// Creates two sample lessons with realistic Vietnamese instruction text:
    /// 1. "Lắp ráp Máy bơm nước" (3 steps)
    /// 2. "Bảo trì Động cơ điện" (4 steps)
    /// </para>
    /// </summary>
    public static class SampleLessonCreator
    {
        private const string MENU_PATH = "Motion AR/Create Sample Lessons";
        private const string ASSET_FOLDER = "Assets/Data/Lessons";

        [MenuItem(MENU_PATH)]
        public static void CreateSampleLessons()
        {
            // Ensure directory exists.
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            if (!AssetDatabase.IsValidFolder(ASSET_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Lessons");
            }

            // ═══════════════════════ LESSON 1: Water Pump Assembly ═══════════════════════
            LessonData lesson1 = ScriptableObject.CreateInstance<LessonData>();
            lesson1.lessonID = "lesson_pump_assembly";
            lesson1.lessonTitle = "Lắp ráp Máy bơm nước";
            lesson1.description = "Học viên sẽ lắp ráp một máy bơm nước cơ bản gồm 3 bộ phận chính: " +
                "thân bơm, cánh quạt và nắp đậy.";
            lesson1.assemblySteps = new List<StepData>
            {
                new StepData
                {
                    stepIndex = 0,
                    requiredPartID = "part_a",
                    snapZoneID = "snap_a",
                    instructionText = "Bước 1: Đặt thân bơm (Part A - màu vàng) vào vị trí trung tâm (SnapZone A).",
                    hintText = "Thân bơm là khối hộp màu vàng. Kéo nó xuống vị trí đánh dấu ở giữa."
                },
                new StepData
                {
                    stepIndex = 1,
                    requiredPartID = "part_b",
                    snapZoneID = "snap_b",
                    instructionText = "Bước 2: Lắp cánh quạt (Part B - màu xanh lá) vào trục bơm (SnapZone B).",
                    hintText = "Cánh quạt là hình trụ màu xanh lá. Gắn vào vị trí bên phải."
                },
                new StepData
                {
                    stepIndex = 2,
                    requiredPartID = "part_c",
                    snapZoneID = "snap_c",
                    instructionText = "Bước 3: Đậy nắp bảo vệ (Part C - màu xanh dương) lên trên (SnapZone C).",
                    hintText = "Nắp bảo vệ là hình cầu màu xanh dương. Đặt vào vị trí bên trái."
                }
            };

            string path1 = $"{ASSET_FOLDER}/Lesson_PumpAssembly.asset";
            AssetDatabase.CreateAsset(lesson1, path1);
            Debug.Log($"[SampleLessonCreator] Created: {path1}");

            // ═══════════════════════ LESSON 2: Motor Maintenance ═══════════════════════
            LessonData lesson2 = ScriptableObject.CreateInstance<LessonData>();
            lesson2.lessonID = "lesson_motor_maintenance";
            lesson2.lessonTitle = "Bảo trì Động cơ điện";
            lesson2.description = "Quy trình bảo trì định kỳ động cơ điện 3 pha: " +
                "kiểm tra, tháo lắp, bôi trơn và kiểm tra cuối.";
            lesson2.assemblySteps = new List<StepData>
            {
                new StepData
                {
                    stepIndex = 0,
                    requiredPartID = "part_a",
                    snapZoneID = "snap_a",
                    instructionText = "Bước 1: Tháo nắp bảo vệ — đặt vào khay an toàn (SnapZone A).",
                    hintText = "Dùng tay kéo nắp ra và đặt vào vị trí chỉ định."
                },
                new StepData
                {
                    stepIndex = 1,
                    requiredPartID = "part_b",
                    snapZoneID = "snap_b",
                    instructionText = "Bước 2: Kiểm tra vòng bi — đặt vòng bi cũ vào khay kiểm tra (SnapZone B).",
                    hintText = "Kiểm tra xem vòng bi có bị mòn không. Xoay thử để cảm nhận ma sát."
                },
                new StepData
                {
                    stepIndex = 2,
                    requiredPartID = "part_c",
                    snapZoneID = "snap_c",
                    instructionText = "Bước 3: Lắp vòng bi mới — đặt vào vị trí trục (SnapZone C).",
                    hintText = "Cẩn thận không đặt nghiêng. Vòng bi phải nằm thẳng trên trục."
                },
                new StepData
                {
                    stepIndex = 3,
                    requiredPartID = "part_a",
                    snapZoneID = "snap_a",
                    instructionText = "Bước 4: Đậy nắp bảo vệ lại — đặt về vị trí ban đầu (SnapZone A).",
                    hintText = "Đảm bảo nắp khớp hoàn toàn trước khi vặn ốc."
                }
            };

            string path2 = $"{ASSET_FOLDER}/Lesson_MotorMaintenance.asset";
            AssetDatabase.CreateAsset(lesson2, path2);
            Debug.Log($"[SampleLessonCreator] Created: {path2}");

            // ═══════════════════════ FINALIZE ═══════════════════════
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Motion AR — Sample Lessons Created",
                $"Created 2 sample LessonData assets:\n\n" +
                $"✅ {path1}\n" +
                $"   → \"{lesson1.lessonTitle}\" (3 steps)\n\n" +
                $"✅ {path2}\n" +
                $"   → \"{lesson2.lessonTitle}\" (4 steps)\n\n" +
                "Next: Assign a LessonData to TrainingFlowController in the Inspector.",
                "OK");

            // Select first lesson in Project window.
            Selection.activeObject = lesson1;
            EditorGUIUtility.PingObject(lesson1);
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateCreateSampleLessons()
        {
            return !Application.isPlaying;
        }
    }
}
#endif

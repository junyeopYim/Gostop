using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hwatu.View.Editor
{
    public static class InkMaskAssetGenerator
    {
        private const string OutputDir = "Assets/Art/InkMasks";

        [MenuItem("Tools/Hwatu/Generate Ink Masks")]
        public static void GenerateInkMasks()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(OutputDir);

            WriteMask("ink_sweep_diag", InkMaskKind.SweepDiag);
            WriteMask("ink_sweep_horiz", InkMaskKind.SweepHoriz);
            WriteMask("ink_edge_radial", InkMaskKind.EdgeRadial);

            // Future scanned or generated real-brush textures can replace these files without code changes
            // as long as luminance still means arrival order.
            AssetDatabase.Refresh();
            Debug.Log("[Hwatu] Ink masks regenerated at " + OutputDir);
        }

        private static void WriteMask(string fileName, InkMaskKind kind)
        {
            var texture = InkMaskGenerator.Create(kind, InkMaskGenerator.DefaultSize);
            var path = OutputDir + "/" + fileName + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

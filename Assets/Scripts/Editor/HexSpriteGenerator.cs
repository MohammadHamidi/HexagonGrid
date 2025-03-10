using UnityEngine;
using UnityEditor;
using System.IO;

namespace HexaAway.Editor
{
    // Create a menu item to generate the hex sprite
    public class HexSpriteGenerator : EditorWindow
    {
        [MenuItem("HexaAway/Generate Hex Sprite")]
        public static void GenerateHexSprite()
        {
            // Create the Resources directory if it doesn't exist
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }

            // Create a new texture
            int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            // Fill with transparency
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }
            texture.SetPixels(pixels);
            
            // Draw hexagon
            int center = size / 2;
            int radius = size / 2 - 4;
            
            // Calculate hex vertices for a flat-top hexagon
            Vector2[] vertices = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = ((i * 60) + 30) * Mathf.Deg2Rad; // +30 to make it flat-top
                vertices[i] = new Vector2(
                    center + radius * Mathf.Cos(angle),
                    center + radius * Mathf.Sin(angle)
                );
            }
            
            // Fill the hexagon with white color
            FillPolygon(texture, vertices, Color.white);
            
            // Draw an arrow pointing to the right (East)
            DrawArrow(texture, center, radius);
            
            // Apply all the pixel changes
            texture.Apply();
            
            // Save the texture as a PNG
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes("Assets/Resources/HexTileSprite.png", bytes);
            
            // Import the asset
            AssetDatabase.ImportAsset("Assets/Resources/HexTileSprite.png");
            
            // Get the texture asset
            TextureImporter importer = AssetImporter.GetAtPath("Assets/Resources/HexTileSprite.png") as TextureImporter;
            if (importer != null)
            {
                // Set proper texture settings for sprite
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                
                // Define sprite pivot to center
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                
                // Apply settings
                importer.SaveAndReimport();
            }
            
            Debug.Log("Hex sprite generated and saved to Assets/Resources/HexTileSprite.png");
        }
        
        private static void FillPolygon(Texture2D texture, Vector2[] vertices, Color color)
        {
            // Find min and max Y coordinates of the polygon
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            
            foreach (Vector2 v in vertices)
            {
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
            }
            
            // For each scanline
            for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
            {
                // Find all intersections with polygon edges
                var intersections = new System.Collections.Generic.List<float>();
                
                for (int i = 0; i < vertices.Length; i++)
                {
                    int j = (i + 1) % vertices.Length;
                    
                    Vector2 vi = vertices[i];
                    Vector2 vj = vertices[j];
                    
                    // Check if the edge crosses the scanline
                    if ((vi.y > y && vj.y <= y) || (vj.y > y && vi.y <= y))
                    {
                        // Calculate the x-coordinate of the intersection
                        float x = vi.x + (y - vi.y) * (vj.x - vi.x) / (vj.y - vi.y);
                        intersections.Add(x);
                    }
                }
                
                // Sort the intersections
                intersections.Sort();
                
                // Fill between pairs of intersections
                for (int i = 0; i < intersections.Count; i += 2)
                {
                    if (i + 1 < intersections.Count)
                    {
                        int startX = Mathf.FloorToInt(intersections[i]);
                        int endX = Mathf.CeilToInt(intersections[i + 1]);
                        
                        for (int x = startX; x <= endX; x++)
                        {
                            if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                            {
                                texture.SetPixel(x, y, color);
                            }
                        }
                    }
                }
            }
        }
        
        private static void DrawArrow(Texture2D texture, int center, int radius)
        {
            // Draw an arrow pointing to the right (East)
            int arrowLength = radius / 2;
            int arrowWidth = radius / 8;
            int headSize = radius / 5;
            
            // Arrow shaft
            for (int x = center - arrowLength/2; x < center + arrowLength/2; x++)
            {
                for (int y = center - arrowWidth; y <= center + arrowWidth; y++)
                {
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                    {
                        texture.SetPixel(x, y, Color.black);
                    }
                }
            }
            
            // Arrow head
            for (int x = center + arrowLength/2; x < center + arrowLength/2 + headSize; x++)
            {
                // Calculate width of arrowhead at this position
                float ratio = (float)(x - (center + arrowLength/2)) / headSize;
                int width = Mathf.RoundToInt(arrowWidth * 2 + ratio * arrowWidth * 2);
                
                for (int y = center - width; y <= center + width; y++)
                {
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                    {
                        texture.SetPixel(x, y, Color.black);
                    }
                }
            }
        }
    }
}
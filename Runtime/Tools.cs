using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinder Tools
    /// </summary>
    internal static class Tools
    {
        /// <summary>
        /// Creates the square.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="localPosition">The local position.</param>
        /// <returns></returns>
        public static SpriteRenderer CreateSquare(Transform parent = null, Vector3 localPosition = default)
        {
            var gameObject = new GameObject("Square_Test", typeof(SpriteRenderer));
            var transform = gameObject.transform;
            transform.SetParent(parent, false);
            transform.position = localPosition;
            var sprite = gameObject.GetComponent<SpriteRenderer>();
            sprite.sprite = Resources.Load<Sprite>("Square");

            return sprite;
        }

        /// <summary>
        /// Creates the world text mesh.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="parent">The parent.</param>
        /// <param name="localPosition">The local position.</param>
        /// <param name="fontSize">Size of the font.</param>
        /// <param name="color">The color.</param>
        /// <param name="textAnchor">The text anchor.</param>
        /// <param name="textAlignment">The text alignment.</param>
        /// <returns></returns>
        public static TextMesh CreateWorldTextMesh(string text, Transform parent = null, Vector3 localPosition = default, int fontSize = 40, Color color = default(Color), TextAnchor textAnchor = TextAnchor.MiddleCenter, TextAlignment textAlignment = TextAlignment.Center)
        {
            if (color == default(Color)) color = Color.white;
            return CreateWorldTextMesh(parent, text, localPosition, fontSize, color, textAnchor, textAlignment);
        }

        /// <summary>
        /// Creates the world text mesh.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="text">The text.</param>
        /// <param name="localPosition">The local position.</param>
        /// <param name="fontSize">Size of the font.</param>
        /// <param name="color">The color.</param>
        /// <param name="textAnchor">The text anchor.</param>
        /// <param name="textAlignment">The text alignment.</param>
        /// <returns></returns>
        public static TextMesh CreateWorldTextMesh(Transform parent, string text, Vector3 localPosition, int fontSize, Color color, TextAnchor textAnchor, TextAlignment textAlignment)
        {
            var gameObject = new GameObject("World_Text", typeof(TextMesh));
            var transform = gameObject.transform;
            transform.SetParent(parent, false);
            transform.position = localPosition;
            var textMesh = gameObject.GetComponent<TextMesh>();
            textMesh.text = text;
            textMesh.color = color;
            textMesh.fontSize = fontSize;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = textAlignment;

            return textMesh;
        }
    }
}

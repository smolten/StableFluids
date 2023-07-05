using UnityEngine;

public class TextureInterpolation : MonoBehaviour
{
    public Texture2D texture1;
    public Texture2D texture2;
    public int outputSize = 64;

    private Texture2D interpolatedTexture;

    private void Start()
    {
        interpolatedTexture = new Texture2D(outputSize, outputSize);
        GetComponent<Renderer>().material.mainTexture = interpolatedTexture;

        InterpolateTextures();
    }

    private struct Int2
    {
        public int x;
        public int y;

        public Int2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    private void InterpolateTextures()
    {
        int inputWidth = texture1.width;
        int inputHeight = texture1.height;
        float xRatio = (float)(inputWidth - 1) / (outputSize - 1);
        float yRatio = (float)(inputHeight - 1) / (outputSize - 1);

        Color[] pixels = new Color[outputSize * outputSize];

        for (int y = 0; y < outputSize; y++)
        {
            for (int x = 0; x < outputSize; x++)
            {
                Vector2 texture1Coords = new Vector2(x * xRatio, y * yRatio);
                Vector2 texture2Coords = new Vector2((x + 1) * xRatio, (y + 1) * yRatio);

                Int2 minCoords = new Int2(
                    Mathf.FloorToInt(texture1Coords.x),
                    Mathf.FloorToInt(texture1Coords.y)
                );
                Int2 maxCoords = new Int2(
                    Mathf.FloorToInt(texture2Coords.x),
                    Mathf.FloorToInt(texture2Coords.y)
                );

                Color color1 = texture1.GetPixel(minCoords.x, minCoords.y);
                Color color2 = texture1.GetPixel(maxCoords.x, minCoords.y);
                Color color3 = texture1.GetPixel(minCoords.x, maxCoords.y);
                Color color4 = texture1.GetPixel(maxCoords.x, maxCoords.y);

                float tX = texture2Coords.x % 1f;
                float tY = texture2Coords.y % 1f;

                Color interpolatedColor = BilinearInterpolation(color1, color2, color3, color4, tX, tY);
                pixels[y * outputSize + x] = interpolatedColor;
            }
        }

        interpolatedTexture.SetPixels(pixels);
        interpolatedTexture.Apply();
    }

    private Color BilinearInterpolation(Color color1, Color color2, Color color3, Color color4, float tX, float tY)
    {
        Color topInterpolation = Color.Lerp(color1, color2, tX);
        Color bottomInterpolation = Color.Lerp(color3, color4, tX);
        return Color.Lerp(topInterpolation, bottomInterpolation, tY);
    }
}
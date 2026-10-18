using UnityEngine;
using UnityEngine.UI;

namespace MapAssist;

// Project through the game's actual map image. Render-texture pixels need not
// match the screen resolution or the UI rectangle on ultrawide displays.
internal sealed class MapProjection
{
    private readonly Camera camera;
    private readonly RawImage? image;
    internal MapProjection(Camera mapCamera, RawImage? mapImage) { camera = mapCamera; image = mapImage; }

    internal bool TryScreenPoint(Vector3 world, out Vector2 point)
    {
        point = Vector2.zero;
        var viewport = camera.WorldToViewportPoint(world);
        if (viewport.z <= 0 || viewport.x < 0 || viewport.y < 0 || viewport.x > 1 || viewport.y > 1) return false;
        if (!camera.targetTexture)
        {
            var screen = camera.WorldToScreenPoint(world);
            point = new Vector2(screen.x, screen.y);
        }
        else
        {
            if (!image || !image!.isActiveAndEnabled || image.texture != camera.targetTexture) return false;
            var uv = image.uvRect;
            if (Mathf.Abs(uv.width) < 0.0001f || Mathf.Abs(uv.height) < 0.0001f) return false;
            var view = camera.rect;
            var x = (view.x + viewport.x * view.width - uv.x) / uv.width;
            var y = (view.y + viewport.y * view.height - uv.y) / uv.height;
            if (x < 0 || y < 0 || x > 1 || y > 1) return false;
            var rect = image.rectTransform;
            var position = rect.TransformPoint(new Vector3(rect.rect.xMin + x * rect.rect.width, rect.rect.yMin + y * rect.rect.height, 0));
            var canvas = image.canvas;
            var uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            point = RectTransformUtility.WorldToScreenPoint(uiCamera, position);
        }
        return point.x >= 0 && point.y >= 0 && point.x <= Screen.width && point.y <= Screen.height;
    }
}

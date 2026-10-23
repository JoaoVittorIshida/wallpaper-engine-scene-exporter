namespace WallpaperSceneExporter.Helpers;

public static class CaptureSurfaceGuard
{
    public static void Validate(int expectedWidth, int expectedHeight, int width, int height)
    {
        if (width != expectedWidth || height != expectedHeight)
            throw new InvalidOperationException(
                "O tamanho da janela de renderização mudou durante a captura. A cena foi preservada; tente novamente sem redimensionar a janela ou alterar a escala do Windows.");
    }
}

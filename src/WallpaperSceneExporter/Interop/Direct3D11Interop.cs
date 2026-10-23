using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace WallpaperSceneExporter.Interop;

internal sealed class Direct3D11Interop : IDisposable
{
    private static readonly Guid Texture2DGuid = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    private readonly ID3D11Device _nativeDevice;

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface(in Guid iid);
    }

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", CallingConvention = CallingConvention.StdCall)]
    private static extern uint CreateDirect3D11DeviceFromDxgiDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    public Direct3D11Interop()
    {
        _nativeDevice = D3D11.D3D11CreateDevice(DriverType.Hardware, DeviceCreationFlags.BgraSupport, null!);
        using IDXGIDevice dxgiDevice = _nativeDevice.QueryInterface<IDXGIDevice>();
        uint result = CreateDirect3D11DeviceFromDxgiDevice(dxgiDevice.NativePointer, out IntPtr inspectable);
        Marshal.ThrowExceptionForHR(unchecked((int)result));
        try { WinRtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable); }
        finally { Marshal.Release(inspectable); }
    }

    public IDirect3DDevice WinRtDevice { get; }
    public ID3D11Device Device => _nativeDevice;
    public ID3D11DeviceContext Context => _nativeDevice.ImmediateContext;

    public ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var access = WinRT.CastExtensions.As<IDirect3DDxgiInterfaceAccess>(surface);
        IntPtr pointer = access.GetInterface(Texture2DGuid);
        return ComObject.As<ID3D11Texture2D>(pointer);
    }

    public void Dispose()
    {
        if (WinRtDevice is IDisposable disposable) disposable.Dispose();
        _nativeDevice.Dispose();
    }
}

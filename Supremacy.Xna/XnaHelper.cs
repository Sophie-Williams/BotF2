using System;
using System.Windows.Threading;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Windows;

using Supremacy.Utility;

namespace Supremacy.Xna
{
    public static class XnaHelper
    {
        public const int ReferenceDeviceDesiredFrameRate = 20;

        private const int DesiredMaxAnisotropy = 16;

        private static readonly MultiSampleType[] DesiredMultiSampleTypes = new[]
                                                                            {
                                                                                MultiSampleType.FourSamples,
                                                                                MultiSampleType.TwoSamples
                                                                            };

        public static SurfaceFormat DetectSurfaceFormat(DeviceType deviceType)
        {
            try
            {
                var adapter = GraphicsAdapter.DefaultAdapter;
                var currentSurfaceFormat = adapter.CurrentDisplayMode.Format;

                if (adapter.CheckDeviceFormat(
                    deviceType,
                    currentSurfaceFormat,
                    TextureUsage.None,
                    QueryUsages.None,
                    ResourceType.RenderTarget,
                    SurfaceFormat.Color))
                {
                    return SurfaceFormat.Color;
                }

                return currentSurfaceFormat;
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.LogException(e);
            }

            return SurfaceFormat.Bgr32;
        }

        public static DeviceType DetermineDeviceType()
        {
            var adapter = GraphicsAdapter.DefaultAdapter;

            if (!adapter.IsDeviceTypeAvailable(DeviceType.Hardware))
            {
                return DeviceType.NullReference;
            }

            var surfaceFormat = DetectSurfaceFormat(DeviceType.Hardware);
            if (surfaceFormat == SurfaceFormat.Color)
                return DeviceType.Hardware;

            return DeviceType.NullReference;
        }

        private static void ResolveDeviceParameters(DeviceType deviceType, out SurfaceFormat backBufferFormat, out DepthFormat depthFormat)
        {
            var adapter = GraphicsAdapter.DefaultAdapter;

            backBufferFormat = DetectSurfaceFormat(deviceType);

            if (adapter.CheckDepthStencilMatch(
                deviceType,
                adapter.CurrentDisplayMode.Format,
                backBufferFormat,
                DepthFormat.Depth24))
            {
                depthFormat = DepthFormat.Depth24;
            }
            else if (adapter.CheckDepthStencilMatch(
                deviceType,
                adapter.CurrentDisplayMode.Format,
                backBufferFormat,
                DepthFormat.Depth16))
            {
                depthFormat = DepthFormat.Depth16;
            }
            else
            {
                depthFormat = DepthFormat.Unknown;
            }
        }

        public static GraphicsDeviceManager CreateDeviceManager(XnaComponent owner)
        {
            var deviceManager = new GraphicsDeviceManager(owner)
                                {
                                    PreferMultiSampling = owner.GraphicsOptions.PreferMultiSampling,
                                    PreferredBackBufferFormat = SurfaceFormat.Color,
                                    PreferredDepthStencilFormat = DepthFormat.Depth24
                                };

            deviceManager.PreparingDeviceSettings +=
                (sender, args) =>
                {
                    var deviceType = args.GraphicsDeviceInformation.DeviceType;

                    args.GraphicsDeviceInformation.PresentationParameters.EnableAutoDepthStencil = owner.GraphicsOptions.EnableDepthStencil;

                    if (!owner.GraphicsOptions.PreferAnisotropicFiltering || deviceType != DeviceType.Hardware)
                        return;

                    deviceManager.DeviceCreated +=
                        (s, e) =>
                        {
                            var device = deviceManager.GraphicsDevice;
                            var deviceCapabilities = device.GraphicsDeviceCapabilities;

                            /*
                             * If the graphics adapter supports anisotropic filtering, then enable it with
                             * the the desired level of anisotropy, or the highest level supported (whichever
                             * is lower).
                             */
                            if (deviceCapabilities.TextureFilterCapabilities.SupportsMinifyAnisotropic &&
                                deviceCapabilities.TextureFilterCapabilities.SupportsMagnifyAnisotropic)
                            {

                                device.SamplerStates[0].MinFilter = TextureFilter.Anisotropic;
                                device.SamplerStates[0].MagFilter = TextureFilter.Anisotropic;
                                device.SamplerStates[0].MaxAnisotropy = Math.Min(
                                    deviceCapabilities.MaxAnisotropy,
                                    DesiredMaxAnisotropy);
                            }
                        };
                };

            return deviceManager;
        }

        public static GraphicsDevice CreateDevice(Int32Rect backBufferSize, bool enableDepthStencil, bool preferMultiSampling, bool preferAnisotropicFiltering)
        {
            var adapter = GraphicsAdapter.DefaultAdapter;
            var deviceType = DetermineDeviceType();

            DepthFormat depthFormat;
            SurfaceFormat backBufferFormat;

            ResolveDeviceParameters(deviceType, out backBufferFormat, out depthFormat);

            if (deviceType != DeviceType.Hardware)
            {
                GameLog.Client.General.Warn(
                    "Hardware graphics device is unavailable; using reference device instead.  " +
                    "Performance may be extremely poor.");
            }

            if (enableDepthStencil && depthFormat == DepthFormat.Unknown)
            {
                enableDepthStencil = false;

                GameLog.Client.General.Warn(
                    "Hardware graphics device is unavailable; using reference device instead.  " +
                    "Performance may be extremely poor.");
            }

            var presentationParameters = new PresentationParameters
                                         {
                                             AutoDepthStencilFormat = depthFormat,
                                             BackBufferCount = 1,
                                             BackBufferFormat = backBufferFormat,
                                             BackBufferWidth = backBufferSize.Width,
                                             BackBufferHeight = backBufferSize.Height,
                                             EnableAutoDepthStencil = enableDepthStencil,
                                             IsFullScreen = false,
                                             SwapEffect = SwapEffect.Discard,
                                             MultiSampleType = MultiSampleType.None,
                                             MultiSampleQuality = 0,
                                             PresentOptions = PresentOptions.None
                                         };

            if (preferMultiSampling &&
                deviceType == DeviceType.Hardware)
            {
                /*
                 * Run through our list of desired multisampling (antialiasing) levels and
                 * enable the best one that's supported by the hardware.
                 */
                foreach (var multiSampleType in DesiredMultiSampleTypes)
                {
                    if (adapter.CheckDeviceMultiSampleType(
                        DeviceType.Hardware,
                        presentationParameters.BackBufferFormat,
                        presentationParameters.IsFullScreen,
                        multiSampleType,
                        out int multiSampleQuality))
                    {
                        presentationParameters.MultiSampleType = multiSampleType;
                        break;
                    }
                }
            }

            var device = new GraphicsDevice(
                adapter,
                deviceType,
                IntPtr.Zero,
                presentationParameters);

            if (preferAnisotropicFiltering &&
                deviceType == DeviceType.Hardware)
            {
                var graphicsCapabilities = GraphicsAdapter.DefaultAdapter.GetCapabilities(deviceType);

                /*
                 * If the graphics adapter supports anisotropic filtering, then enable it with
                 * the the desired level of anisotropy, or the highest level supported (whichever
                 * is lower).
                 */
                if (graphicsCapabilities.TextureFilterCapabilities.SupportsMinifyAnisotropic &&
                    graphicsCapabilities.TextureFilterCapabilities.SupportsMagnifyAnisotropic)
                {
                    device.SamplerStates[0].MinFilter = TextureFilter.Anisotropic;
                    device.SamplerStates[0].MagFilter = TextureFilter.Anisotropic;
                    device.SamplerStates[0].MaxAnisotropy = Math.Min(
                        graphicsCapabilities.MaxAnisotropy, 
                        DesiredMaxAnisotropy);
                }
            }

            return device;
        }

        public static DispatcherTimer CreateRenderTimer(int desiredFrameRate = ReferenceDeviceDesiredFrameRate)
        {
            var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(1000d / desiredFrameRate),
                        };
            return timer;
        }

        public static BoundingBox ComputeBoundingBox(Model model, Matrix worldTransform)
        {
            /*
             * Initialize minimum and maximum corners of the bounding box to max and min values.
             */
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            // For each mesh of the model
            foreach (var mesh in model.Meshes)
            {
                foreach (var meshPart in mesh.MeshParts)
                {
                    // Vertex buffer parameters
                    var vertexStride = meshPart.VertexStride;
                    var vertexBufferSize = meshPart.NumVertices * vertexStride;

                    // Get vertex data as float
                    var vertexData = new float[vertexBufferSize / sizeof(float)];

                    mesh.VertexBuffer.GetData(vertexData, meshPart.StartIndex, meshPart.NumVertices);

                    // Iterate through vertices (possibly) growing bounding box, all calculations are done in world space
                    for (var i = 0; i < vertexBufferSize / sizeof(float); i += vertexStride / sizeof(float))
                    {
                        var transformedPosition = Vector3.Transform(
                            new Vector3(
                                vertexData[i],
                                vertexData[i + 1],
                                vertexData[i + 2]),
                            worldTransform);

                        min = Vector3.Min(min, transformedPosition);
                        max = Vector3.Max(max, transformedPosition);
                    }
                }
            }

            // Create and return bounding box
            return new BoundingBox(min, max);
        }
    }
}
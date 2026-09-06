using Microsoft.UI.Xaml;
using NorthStar;

namespace NorthStar.UI
{
    public partial class App : Application
    {
        private Window? _window;
        private MediaFoundationRuntime? mediaFoundation;
        private NorthStarRuntime? runtime;

        public App()
        {
            InitializeComponent();

            mediaFoundation =
                new MediaFoundationRuntime();

            runtime =
                new NorthStarRuntime(
                    "Models\\rtmpose-m.onnx");
        }

        protected override void OnLaunched(
            Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window =
                new MainWindow();

            _window.Closed +=
                Window_Closed;

            _window.Activate();
        }

        private void Window_Closed(
            object sender,
            WindowEventArgs args)
        {
            runtime?.Dispose();
            runtime = null;

            mediaFoundation?.Dispose();
            mediaFoundation = null;

            _window = null;
        }
    }
}
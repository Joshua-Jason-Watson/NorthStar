using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NorthStar;
using NorthStar.UI.Pages;

using System;

namespace NorthStar.UI.Navigation
{
    // Switches between the main camera view and the developer page.
    internal sealed class DevViewHost : IDisposable
    {
        private readonly NorthStarRuntime runtime;

        private readonly Grid mainView;

        private readonly Frame devFrame;

        private readonly Button devButton;

        private readonly DevPage devPage;

        private bool disposed;


        public DevViewHost(
            NorthStarRuntime runtime,
            Grid mainView,
            Frame devFrame,
            Button devButton)
        {
            this.runtime =
                runtime;

            this.mainView =
                mainView;

            this.devFrame =
                devFrame;

            this.devButton =
                devButton;

            devPage =
                new DevPage();

            devFrame.Content =
                devPage;

            devPage.SetRuntime(
                runtime);
        }


        public void Toggle()
        {
            if (devFrame.Visibility ==
                Visibility.Visible)
            {
                devFrame.Visibility =
                    Visibility.Collapsed;

                mainView.Visibility =
                    Visibility.Visible;

                devButton.Content =
                    "Dev";

                return;
            }

            mainView.Visibility =
                Visibility.Collapsed;

            devFrame.Visibility =
                Visibility.Visible;

            devButton.Content =
                "Main";

            devPage.SetRuntime(
                runtime);
        }


        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            devPage.Dispose();

            disposed =
                true;
        }
    }
}

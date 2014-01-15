using System;
using System.Windows;
using Sharparam.SharpBlade.Native;
using Sharparam.SharpBlade.Razer;
using Sharparam.SharpBlade.Razer.Events;

namespace SwitchSpeak
{
    public static class RazerProvider
    {
        private static RazerManager _razer;

        public static RazerManager Razer
        {
            get
            {
                if (_razer == null)
                {
                    _razer = new RazerManager();
                    _razer.AppEvent += OnAppEvent;
                }

                return _razer;
            }
        }

        private static void OnAppEvent(object sender, AppEventEventArgs e)
        {
            switch (e.Type)
            {
                //case RazerAPI.AppEventType.Activated:
                    //_razer.Touchpad.ClearWindow();
                    //_razer.Touchpad.SetWindow(Application.Current.MainWindow, Touchpad.RenderMethod.Polling, new TimeSpan(0, 0, 0, 0, 42));
                    //break;
                case RazerAPI.AppEventType.Deactivated:
                    //_razer.Touchpad.ClearWindow();
                    //break;
                case RazerAPI.AppEventType.Close:
                case RazerAPI.AppEventType.Exit:
                    Application.Current.Shutdown();
                    break;
            }
        }
    }
}

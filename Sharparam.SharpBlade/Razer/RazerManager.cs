﻿/* RazerManager.cs
 * 
 * Copyright © 2013 by Adam Hellberg and Brandon Scott.
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of
 * this software and associated documentation files (the "Software"), to deal in
 * the Software without restriction, including without limitation the rights to
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
 * of the Software, and to permit persons to whom the Software is furnished to do
 * so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * Disclaimer: SharpBlade is in no way affiliated
 * with Razer and/or any of its employees and/or licensors.
 * Adam Hellberg does not take responsibility for any harm caused, direct
 * or indirect, to any Razer peripherals via the use of SharpBlade.
 * 
 * "Razer" is a trademark of Razer USA Ltd.
 */

using System;
using System.IO;
using Sharparam.SharpBlade.Integration;
using Sharparam.SharpBlade.Native;
using Sharparam.SharpBlade.Razer.Events;
using Sharparam.SharpBlade.Razer.Exceptions;

namespace Sharparam.SharpBlade.Razer
{
    /// <summary>
    /// Manages everything related to Razer and its devices.
    /// </summary>
    public class RazerManager : IDisposable
    {
        /// <summary>
        /// Raised when an app event occurs.
        /// </summary>
        public event AppEventEventHandler AppEvent;

        /// <summary>
        /// Raised when a dynamic key event occurs.
        /// </summary>
        public event DynamicKeyEventHandler DynamicKeyEvent;

        /// <summary>
        /// Raised when a keyboard raw event occurs.
        /// </summary>
        public event KeyboardRawEventHandler KeyboardRawEvent;

        /// <summary>
        /// Raised when a keyboard char event occurs.
        /// </summary>
        public event KeyboardCharEventHandler KeyboardCharTyped;

        /// <summary>
        /// Raised when a keyboard key is pressed.
        /// </summary>
        public event KeyboardKeyEventHandler KeyboardKeyDown;

        /// <summary>
        /// Raised when a keyboard key is released.
        /// </summary>
        public event KeyboardKeyEventHandler KeyboardKeyUp;

        private const string RazerControlFile = "DO_NOT_TOUCH__RAZER_CONTROL_FILE";
        
        // Native code callbacks
        private static RazerAPI.AppEventCallbackDelegate _appEventCallback;
        private static RazerAPI.DynamicKeyCallbackFunctionDelegate _dkCallback;
        private static RazerAPI.KeyboardCallbackFunctionDelegate _kbCallback;

        private bool _disposed;

        private readonly DynamicKey[] _dynamicKeys;

        private KeyboardControl _keyboardControl;

        /// <summary>
        /// Gets the touchpad on the keyboard.
        /// </summary>
        public Touchpad Touchpad { get; private set; }

        /// <summary>
        /// Gets a boolean indicating whether keyboard capture is enabled or not.
        /// </summary>
        public bool KeyboardCapture { get; private set; }

        /// <summary>
        /// Creates a new <see cref="RazerManager" />.
        /// </summary>
        /// <param name="disableOSGestures">
        /// If true, all OS gestures will by default be disabled on the touchpad,
        /// making it do nothing until gestures are enabled manually.
        /// </param>
        /// <param name="useControlFile">
        /// If true, creates a control file that is checked on subsequent creations
        /// of <see cref="RazerManager" />, initialization will fail if a control file
        /// is found and useControlFile is true.
        /// </param>
        /// <remarks>Only one Razer manager should be active at any one time.</remarks>
        public RazerManager(bool disableOSGestures = true, bool useControlFile = false)
        {
            //_log.Info("RazerManager is initializing");

            if (useControlFile && File.Exists(RazerControlFile))
            {
                //_log.Error("Detected control file presence, throwing exception.");
                throw new RazerUnstableShutdownException();
            }

            if (useControlFile)
                CreateControlFile();

            //_log.Debug("Calling RzSBStart()");

            var hResult = RazerAPI.RzSBStart();
            if (HRESULT.RZSB_FAILED(hResult))
            {
                // Try one more time
                hResult = RazerAPI.RzSBStart();
                if (HRESULT.RZSB_FAILED(hResult))
                    throw new RazerNativeException("RzSBStart", hResult);
            }

            //_log.Info("Setting up dynamic keys");

            //_log.Debug("Creating new app event callback");
            _appEventCallback = HandleAppEvent;
            //_log.Debug("Calling RzSBAppEventSetCallback");
            hResult = RazerAPI.RzSBAppEventSetCallback(_appEventCallback);
            if (HRESULT.RZSB_FAILED(hResult))
                throw new RazerNativeException("RzSBAppEventSetCallback", hResult);

            //_log.Info("Setting up touchpad");
            Touchpad = new Touchpad();

            if (disableOSGestures)
                Touchpad.DisableOSGesture(RazerAPI.GestureType.All);

            //_log.Debug("Creating dynamic key callback");
            _dkCallback = HandleDynamicKeyEvent;
            //_log.Debug("Calling RzSBDynamicKeySetCallback");
            hResult = RazerAPI.RzSBDynamicKeySetCallback(_dkCallback);
            if (HRESULT.RZSB_FAILED(hResult))
                throw new RazerNativeException("RzSBDynamicKeySetCallback", hResult);

            //_log.Info("Setting up keyboard");

            // Keyboard capture callback
            //_log.Debug("Creating keyboard callback");
            _kbCallback = HandleKeyboardEvent;
            //_log.Debug("Calling RzSBDynamicKeySetCallback");
            hResult = RazerAPI.RzSBKeyboardCaptureSetCallback(_kbCallback);
            if (HRESULT.RZSB_FAILED(hResult))
                throw new RazerNativeException("RzSBKeyboardCaptureSetCallback", hResult);

            //_log.Debug("Initializing dynamic key arrays");

            _dynamicKeys = new DynamicKey[RazerAPI.DynamicKeysCount];
        }

        /// <summary>
        /// Allows an object to try to free resources and perform other
        /// cleanup operations before it is reclaimed by garbage collection.
        /// <see cref="RazerManager" /> will call <see cref="RazerAPI.RzSBStop" />
        /// if not disposed at the point of finalization.
        /// </summary>
        ~RazerManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes of this <see cref="RazerManager" />.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            //_log.Debug("RazerManager is disposing...");

            if (disposing)
            {
                if (Touchpad != null)
                {
                    Touchpad.Dispose();
                    Touchpad = null;
                }
            }

            //_log.Debug("Dispose: Calling Stop()");
            Stop();

            _disposed = true;
        }

        private void OnAppEvent(RazerAPI.AppEventType type, RazerAPI.AppEventMode mode, uint processId)
        {
            var func = AppEvent;
            if (func != null)
                func(this, new AppEventEventArgs(type, mode, processId));
        }

        private void OnDynamicKeyEvent(RazerAPI.DynamicKeyType keyType, RazerAPI.DynamicKeyState state)
        {
            var func = DynamicKeyEvent;
            if (func != null)
                func(this, new DynamicKeyEventArgs(keyType, state));
        }

        private void OnKeyboardRawEvent(uint uMsg, UIntPtr wParam, IntPtr lParam)
        {
            var func = KeyboardRawEvent;
            if (func != null)
                func(this, new KeyboardRawEventArgs(uMsg, wParam, lParam));
        }

        private void OnKeyboardCharTyped(char character)
        {
            var func = KeyboardCharTyped;
            if (func != null)
                func(this, new KeyboardCharEventArgs(character));
        }

        private void OnKeyboardKeyDown(WinAPI.VirtualKey key, ModifierKeys modifiers)
        {
            var func = KeyboardKeyDown;
            if (func != null)
                func(this, new KeyboardKeyEventArgs(key, modifiers));
        }

        private void OnKeyboardKeyUp(WinAPI.VirtualKey key, ModifierKeys modifiers)
        {
            var func = KeyboardKeyUp;
            if (func != null)
                func(this, new KeyboardKeyEventArgs(key, modifiers));
        }

        /// <summary>
        /// Creates the Razer control file.
        /// </summary>
        public static void CreateControlFile()
        {
            try
            {
                if (File.Exists(RazerControlFile))
                {
                    //StaticLog.Warn("CreateControlFile: File already exists");
                }
                else
                {
                    File.Create(RazerControlFile).Close();
                    //StaticLog.Info("CreateControlFile: Success!");
                }
            }
            catch (IOException ex)
            {
                //StaticLog.ErrorFormat("CreateControlFile: [IOException] Failed to create control file: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Deletes the Razer control file.
        /// </summary>
        public static void DeleteControlFile()
        {
            try
            {
                if (File.Exists(RazerControlFile))
                {
                    File.Delete(RazerControlFile);
                    //StaticLog.Info("DeleteControlFile: Success!");
                }
                else
                {
                    //StaticLog.Warn("DeleteControlFile: File does not exist");
                }
            }
            catch (IOException ex)
            {
                //StaticLog.ErrorFormat("DeleteControlFile: [IOException] Failed to delete control file: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Stops all Razer interaction.
        /// </summary>
        /// <param name="cleanup">True to delete the control file and clean up, false otherwise.</param>
        public void Stop(bool cleanup = true)
        {
            //_log.Info("RazerManager is stopping! Calling RzSBStop...");
            RazerAPI.RzSBStop();
            if (cleanup)
                DeleteControlFile();
            //_log.Info("RazerManager has stopped.");
        }

        [Obsolete("Throw a RazerNativeException directly instead (with the same arguments).")]
        internal static void NativeCallFailure(string func, HRESULT result)
        {
            //StaticLog.FatalFormat("Call to RazerAPI native function {0} FAILED with error: {1}", func, result.ToString());
            //StaticLog.Debug("Throwing exception...");
            throw new RazerNativeException(func, result);
        }

        /// <summary>
        /// Gets the SwitchBlade touchpad device.
        /// </summary>
        /// <returns></returns>
        [Obsolete("Access the touchpad via the Touchpad property instead.")]
        public Touchpad GetTouchpad()
        {
            return Touchpad;
        }

        /// <summary>
        /// Gets a specific dynamic key.
        /// </summary>
        /// <param name="keyType">The key type to get.</param>
        /// <returns><see cref="DynamicKey" /> object representing the specified key type.</returns>
        public DynamicKey GetDynamicKey(RazerAPI.DynamicKeyType keyType)
        {
            return _dynamicKeys[(int) keyType - 1];
        }

        /// <summary>
        /// Enables a specific dynamic key.
        /// </summary>
        /// <param name="keyType">The key type to enable.</param>
        /// <param name="upImage">Image to display on this key when in the UP state.</param>
        /// <param name="downImage">Image to display on this key when in the DOWN state.</param>
        /// <param name="replace">True to override this key's previous configuration
        /// if it has already been enabled, otherwise returns current key if already enabled.</param>
        /// <returns>The dynamic key that was enabled.</returns>
        public DynamicKey EnableDynamicKey(RazerAPI.DynamicKeyType keyType, string upImage, string downImage = null, bool replace = false)
        {
            return EnableDynamicKey(keyType, null, upImage, downImage, replace);
        }

        /// <summary>
        /// Enables a specific dynamic key.
        /// </summary>
        /// <param name="keyType">The key type to enable.</param>
        /// <param name="callback">Callback called when this key is pressed.</param>
        /// <param name="upImage">Image to display on this key when in the UP state.</param>
        /// <param name="downImage">Image to display on this key when in the DOWN state.</param>
        /// <param name="replace">True to override this key's previous configuration
        /// if it has already been enabled, otherwise returns current key if already enabled.</param>
        /// <returns>The dynamic key that was enabled.</returns>
        public DynamicKey EnableDynamicKey(RazerAPI.DynamicKeyType keyType, DynamicKeyPressedEventHandler callback, string upImage, string downImage = null, bool replace = false)
        {
            var index = (int) keyType - 1;
            if (_dynamicKeys[index] != null && !replace)
            {
                //_log.Info("Dynamic key already enabled and replace is false.");
                return _dynamicKeys[index];
            }

            //_log.Debug("Resetting dynamic key (DisableDynamicKey)");
            DisableDynamicKey(keyType);
            try
            {
                //_log.Debug("Creating new DynamicKey object");
                var dk = new DynamicKey(keyType, upImage, downImage, callback);
                _dynamicKeys[index] = dk;
            }
            catch (RazerNativeException ex)
            {
                //_log.ErrorFormat("Failed to enable dynamic key {0}: {1}", keyType, ex.Hresult);
                throw new RazerDynamicKeyException(String.Format("Failed to enable dynamic key {0} due to a native call exception.", keyType), ex);
            }

            return _dynamicKeys[index];
        }

        /// <summary>
        /// Disables a specific dynamic key.
        /// </summary>
        /// <param name="keyType">The key type to disable.</param>
        public void DisableDynamicKey(RazerAPI.DynamicKeyType keyType)
        {
            var index = (int) keyType - 1;
            var dk = _dynamicKeys[index];
            if (dk != null)
                dk.Disable();
            _dynamicKeys[index] = null;
        }

        /// <summary>
        /// Enables or disables keyboard capture.
        /// </summary>
        /// <param name="enabled"></param>
        public void SetKeyboardCapture(bool enabled)
        {
            if (enabled == KeyboardCapture)
                return;

            var hresult = RazerAPI.RzSBCaptureKeyboard(enabled);
            if (HRESULT.RZSB_FAILED(hresult))
                throw new RazerNativeException("RzSBCaptureKeyboard", hresult);

            if (!enabled)
                _keyboardControl = null;

            KeyboardCapture = enabled;
        }

        /// <summary>
        /// Starts forwarding keyboard events to the specified WinForms control.
        /// </summary>
        /// <param name="control">THe control to forward input to.</param>
        /// <param name="releaseOnEnter">If true, keyboard capture will cease when the enter key is pressed,
        ///     otherwise, <see cref="SetKeyboardCapture" /> has to be called explicitly with false as the argument.</param>
        public void StartWinFormsControlKeyboardCapture(System.Windows.Forms.Control control, bool releaseOnEnter = true)
        {
            SetKeyboardCapture(true);
            _keyboardControl = new KeyboardControl(control, releaseOnEnter);
        }

        /// <summary>
        /// Starts forwarding keyboard events to the specified WPF control.
        /// </summary>
        /// <param name="control">The control to forward input to.</param>
        /// <param name="releaseOnEnter">If true, keyboard capture will cease when the enter key is pressed,
        /// otherwise, <see cref="SetKeyboardCapture" /> has to be called explicitly with false as the argument.</param>
        public void StartWPFControlKeyboardCapture(System.Windows.Controls.Control control, bool releaseOnEnter = true)
        {
            SetKeyboardCapture(true);
            _keyboardControl = new KeyboardControl(control, releaseOnEnter);
        }

        private HRESULT HandleAppEvent(RazerAPI.AppEventType type, uint appMode, uint processId)
        {
            var hResult = HRESULT.RZSB_OK;
            if (type == RazerAPI.AppEventType.Invalid || type == RazerAPI.AppEventType.None)
            {
                //_log.DebugFormat("Unsupported AppEventType: {0}", type);
                return hResult;
            }

            OnAppEvent(type, (RazerAPI.AppEventMode) appMode, processId);

            return hResult;
        }

        private HRESULT HandleDynamicKeyEvent(RazerAPI.DynamicKeyType keyType, RazerAPI.DynamicKeyState state)
        {
            var result = HRESULT.RZSB_OK;

            //_log.Debug("Raising DynamicKeyEvent event");
            OnDynamicKeyEvent(keyType, state);

            var index = (int) keyType - 1;
            var dk = _dynamicKeys[index];
            if (dk == null)
            {
                //_log.Debug("Key has not been registered by app");
                return result;
            }

            //_log.Debug("Updating key state");
            // UpdateState will check if it's a valid press and call any event subscribers
            dk.UpdateState(state);

            return result;
        }

        private HRESULT HandleKeyboardEvent(uint uMsg, UIntPtr wParam, IntPtr lParam)
        {
            var result = HRESULT.RZSB_OK;

            var msgType = (WinAPI.MessageType) uMsg;
            var asChar = (char) wParam;

            OnKeyboardRawEvent(uMsg, wParam, lParam);

            // We only want to send the char event if it's a char that can actually be typed
            // So it doesn't handle SHIFT and CONTROL as "characters"
            if (msgType == WinAPI.MessageType.CHAR && !Char.IsControl(asChar))
            {
                OnKeyboardCharTyped(asChar);
                if (_keyboardControl != null)
                    _keyboardControl.SendChar(asChar);
            }
            else if (msgType == WinAPI.MessageType.KEYDOWN || msgType == WinAPI.MessageType.KEYUP)
            {
                var key = (WinAPI.VirtualKey) (uint) wParam;

                // Workaround to get the modifier keys
                var modifiers = ModifierKeys.None;

                if ((WinAPI.GetKeyState((int) WinAPI.VirtualKey.SHIFT) & WinAPI.KEY_PRESSED) != 0)
                    modifiers |= ModifierKeys.Shift;

                if ((WinAPI.GetKeyState((int) WinAPI.VirtualKey.CONTROL) & WinAPI.KEY_PRESSED) != 0)
                    modifiers |= ModifierKeys.Control;

                // MENU == ALT
                if ((WinAPI.GetKeyState((int) WinAPI.VirtualKey.MENU) & WinAPI.KEY_PRESSED) != 0)
                    modifiers |= ModifierKeys.Alt;

                if ((WinAPI.GetKeyState((int)WinAPI.VirtualKey.CAPITAL) & WinAPI.KEY_TOGGLED) != 0)
                    modifiers |= ModifierKeys.CapsLock;

                if (msgType == WinAPI.MessageType.KEYDOWN)
                {
                    OnKeyboardKeyDown(key, modifiers);
                    if (_keyboardControl != null)
                        _keyboardControl.SendKeyDown(key);
                }
                else if (msgType == WinAPI.MessageType.KEYUP)
                {
                    OnKeyboardKeyUp(key, modifiers);
                    if (_keyboardControl != null)
                    {
                        _keyboardControl.SendKeyUp(key);
                        if (key == WinAPI.VirtualKey.RETURN && _keyboardControl.ReleaseOnEnter)
                            SetKeyboardCapture(false);
                    }
                }
            }

            return result;
        }

    }
}

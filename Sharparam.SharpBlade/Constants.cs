﻿/* Constants.cs
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

namespace Sharparam.SharpBlade
{
    /// <summary>
    /// Constant values used by the SharpBlade library.
    /// </summary>
    /// <remarks>
    /// Please note that most values here expect a certain project structure like so:
    /// <code>
    /// ${APP_ROOT}\res\
    /// ${APP_ROOT}\res\images\
    /// ${APP_ROOT}\res\images\tp_blank.png
    /// ${APP_ROOT}\res\images\dk_disabled.png
    /// </code>
    /// Path values in this class should not be used unless your project structure
    /// matches that described above.
    /// </remarks>
    public static class Constants
    {
        /// <summary>
        /// True if compiled with DEBUG enabled, false otherwise.
        /// </summary>
#if DEBUG
        public const bool DebugEnabled = true;
#else
        public const bool DebugEnabled = false;
#endif

        /// <summary>
        /// Blank image used for touchpad.
        /// </summary>
        public const string BlankTouchpadImage = @"res\images\tp_blank.png";
        
        /// <summary>
        /// Blank image used for dynamic keys.
        /// </summary>
        public const string DisabledDynamicKeyImage = @"res\images\dk_disabled.png";
    }
}

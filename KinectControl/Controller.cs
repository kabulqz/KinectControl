using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace KinectControl
{
    internal static class SystemController
    {// class that controls the system clicks and processes like closing the application, etc.

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx;              // Horizontal position of the mouse
            public int dy;              // Vertical position of the mouse
            public uint mouseData;      // Additional data for the mouse event, such as wheel movement
            public uint dwFlags;        // Flags that specify the type of mouse event (e.g., move, click)
            public uint time;           // Timestamp for the event, can be 0 to use the system time
            public IntPtr dwExtraInfo;  // Additional information associated with the event, can be null
        }
        private static Input CreateMouseInput(uint flags, int dx = 0, int dy = 0, uint mouseData = 0)
        {
            return new Input
            {
                type = App.Flags.InputType.MOUSE,
                mi = new MouseInput
                {
                    dx = dx,
                    dy = dy,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0, // Use system time
                    dwExtraInfo = IntPtr.Zero // No additional information
                }
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort wVk;          // Virtual key code of the key being pressed or released
            public ushort wScan;        // Hardware scan code of the key
            public uint dwFlags;        // Flags that specify the type of keyboard event (e.g., key down, key up)
            public uint time;           // Timestamp for the event, can be 0 to use the system time
            public IntPtr dwExtraInfo;  // Additional information associated with the event, can be null
        }
        private static Input CreateKeyboardInput(ushort virtualKey, uint flags)
        {
            return new Input
            {
                type = App.Flags.InputType.KEYBOARD,
                ki = new KeyboardInput
                {
                    wVk = virtualKey,
                    wScan = 0, // No hardware scan code
                    dwFlags = flags,
                    time = 0, // Use system time
                    dwExtraInfo = IntPtr.Zero // No additional information
                }
            };
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Input
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(8)] public MouseInput mi;
            [FieldOffset(8)] public KeyboardInput ki;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        private static void SendSingleInput(Input input)
        {
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input)));
        }

        public static void KeyDown(ushort virtualKey) => SendSingleInput(CreateKeyboardInput(virtualKey, 0));
        public static void KeyUp(ushort virtualKey) => SendSingleInput(CreateKeyboardInput(virtualKey, App.Flags.Keyboard.KEYUP));
        public static void QuickKeyCombo(ushort[] keys, int delayMs = 100)
        {
            foreach (var key in keys) KeyDown(key);
            Thread.Sleep(delayMs);
            foreach (var key in keys.Reverse()) KeyUp(key);
        }
        public static void LeftDown() => SendSingleInput(CreateMouseInput(App.Flags.Mouse.LEFTDOWN));
        public static void LeftUp() => SendSingleInput(CreateMouseInput(App.Flags.Mouse.LEFTUP));
        public static void LeftClick()
        {
            LeftDown();
            Thread.Sleep(50); // Short delay to simulate a click
            LeftUp();
        }
        public static void DoubleLeftClick()
        { 
            LeftClick();
            Thread.Sleep(50); // Short delay between clicks
            LeftClick();
        }
        public static void MiddleDown() => SendSingleInput(CreateMouseInput(App.Flags.Mouse.MIDDLEDOWN));
        public static void MiddleUp() => SendSingleInput(CreateMouseInput(App.Flags.Mouse.MIDDLEUP));
        public static void RightDown() => SendSingleInput(CreateMouseInput(App.Flags.Mouse.RIGHTDOWN));
        public static void RightUp() => SendSingleInput(CreateMouseInput(App.Flags.Mouse.RIGHTUP));
        public static void MoveBy(int dx, int dy) => SendSingleInput(CreateMouseInput(App.Flags.Mouse.MOVE, dx, dy));
        public static void MoveTo(int x, int y)
        {
            SetCursorPos(x, y);
        }

    }
}

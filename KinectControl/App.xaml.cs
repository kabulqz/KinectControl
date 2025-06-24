using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace KinectControl
{
    public partial class App : Application
    {
        public const int Alpha =
#if DEBUG
            255;
#else
            (int)(255 * 66.6f) / 100;
#endif
        public const float WindowScaleFactor =
#if DEBUG
            1.0f;
#else
            2.0f;
#endif
        // thresholds
        public const float WarningDistance = 1.5f;
        public const float CalibrationTimeThreshold = 2.5f;
        public const float EndControlTimeThreshold = 2.0f;

        public static class Flags
        {
            public static class InputType
            {
                public const uint MOUSE = 0;
                public const uint KEYBOARD = 1;
                public const uint HARDWARE = 2;
            }

            public static class Mouse
            {
                public const uint MOVE = 0x0001;
                public const uint LEFTDOWN = 0x0002;
                public const uint LEFTUP = 0x0004;
                public const uint RIGHTDOWN = 0x0008;
                public const uint RIGHTUP = 0x0010;
                public const uint MIDDLEDOWN = 0x0020;
                public const uint MIDDLEUP = 0x0040;
                public const uint XDOWN = 0x0080;
                public const uint XUP = 0x0100;
                public const uint WHEEL = 0x0800;
                public const uint HWHEEL = 0x1000;
                public const uint ABSOLUTE = 0x8000;
                public const uint VIRTUALDESK = 0x4000;
                public const uint MOVE_NOCOALESCE = 0x2000;
            }

            public static class Keyboard
            {
                public const uint EXTENDEDKEY = 0x0001;
                public const uint KEYUP = 0x0002;
                public const uint UNICODE = 0x0004;
                public const uint SCANCODE = 0x0008;

                public static class Key
                {
                    public const ushort BACKSPACE = 0x08;
                    public const ushort TAB = 0x09;
                    public const ushort ENTER = 0x0D;
                    public const ushort SHIFT = 0x10;
                    public const ushort CTRL = 0x11;
                    public const ushort ALT = 0x12;
                    public const ushort PAUSE = 0x13;
                    public const ushort CAPS_LOCK = 0x14;
                    public const ushort ESCAPE = 0x1B;
                    public const ushort SPACE = 0x20;
                    public const ushort PAGE_UP = 0x21;
                    public const ushort PAGE_DOWN = 0x22;
                    public const ushort END = 0x23;
                    public const ushort HOME = 0x24;
                    public const ushort LEFT_ARROW = 0x25;
                    public const ushort UP_ARROW = 0x26;
                    public const ushort RIGHT_ARROW = 0x27;
                    public const ushort DOWN_ARROW = 0x28;
                    public const ushort PRINT_SCREEN = 0x2C;
                    public const ushort INSERT = 0x2D;
                    public const ushort DELETE = 0x2E;

                    public const ushort D0 = 0x30;
                    public const ushort D1 = 0x31;
                    public const ushort D2 = 0x32;
                    public const ushort D3 = 0x33;
                    public const ushort D4 = 0x34;
                    public const ushort D5 = 0x35;
                    public const ushort D6 = 0x36;
                    public const ushort D7 = 0x37;
                    public const ushort D8 = 0x38;
                    public const ushort D9 = 0x39;

                    public const ushort A = 0x41;
                    public const ushort B = 0x42;
                    public const ushort C = 0x43;
                    public const ushort D = 0x44;
                    public const ushort E = 0x45;
                    public const ushort F = 0x46;
                    public const ushort G = 0x47;
                    public const ushort H = 0x48;
                    public const ushort I = 0x49;
                    public const ushort J = 0x4A;
                    public const ushort K = 0x4B;
                    public const ushort L = 0x4C;
                    public const ushort M = 0x4D;
                    public const ushort N = 0x4E;
                    public const ushort O = 0x4F;
                    public const ushort P = 0x50;
                    public const ushort Q = 0x51;
                    public const ushort R = 0x52;
                    public const ushort S = 0x53;
                    public const ushort T = 0x54;
                    public const ushort U = 0x55;
                    public const ushort V = 0x56;
                    public const ushort W = 0x57;
                    public const ushort X = 0x58;
                    public const ushort Y = 0x59;
                    public const ushort Z = 0x5A;

                    public const ushort LEFT_WINDOWS = 0x5B;
                    public const ushort RIGHT_WINDOWS = 0x5C;
                    public const ushort APPS = 0x5D;

                    public const ushort NUMPAD0 = 0x60;
                    public const ushort NUMPAD1 = 0x61;
                    public const ushort NUMPAD2 = 0x62;
                    public const ushort NUMPAD3 = 0x63;
                    public const ushort NUMPAD4 = 0x64;
                    public const ushort NUMPAD5 = 0x65;
                    public const ushort NUMPAD6 = 0x66;
                    public const ushort NUMPAD7 = 0x67;
                    public const ushort NUMPAD8 = 0x68;
                    public const ushort NUMPAD9 = 0x69;
                    public const ushort MULTIPLY = 0x6A;
                    public const ushort ADD = 0x6B;
                    public const ushort SEPARATOR = 0x6C;
                    public const ushort SUBTRACT = 0x6D;
                    public const ushort DECIMAL = 0x6E;
                    public const ushort DIVIDE = 0x6F;

                    public const ushort F1 = 0x70;
                    public const ushort F2 = 0x71;
                    public const ushort F3 = 0x72;
                    public const ushort F4 = 0x73;
                    public const ushort F5 = 0x74;
                    public const ushort F6 = 0x75;
                    public const ushort F7 = 0x76;
                    public const ushort F8 = 0x77;
                    public const ushort F9 = 0x78;
                    public const ushort F10 = 0x79;
                    public const ushort F11 = 0x7A;
                    public const ushort F12 = 0x7B;

                    public const ushort NUM_LOCK = 0x90;
                    public const ushort SCROLL_LOCK = 0x91;

                    public const ushort LEFT_SHIFT = 0xA0;
                    public const ushort RIGHT_SHIFT = 0xA1;
                    public const ushort LEFT_CONTROL = 0xA2;
                    public const ushort RIGHT_CONTROL = 0xA3;
                    public const ushort LEFT_MENU = 0xA4;
                    public const ushort RIGHT_MENU = 0xA5;
                }
            }

            public static class ShowWindow
            {
                public const int HIDE = 0;
                public const int NORMAL = 1;
                public const int SHOWNORMAL = 1;
                public const int SHOWMINIMIZED = 2;
                public const int SHOWMAXIMIZED = 3;
                public const int MAXIMIZE = 3;
                public const int SHOWNOACTIVATE = 4;
                public const int SHOW = 5;
                public const int MINIMIZE = 6;
                public const int SHOWMINNOACTIVE = 7;
                public const int SHOWNA = 8;
                public const int RESTORE = 9;
                public const int SHOWDEFAULT = 10;
                public const int FORCEMINIMIZE = 11;
            }

            public static class Ancestor
            {
                public const uint PARENT = 1;
                public const uint ROOT = 2;
                public const uint ROOTOWNER = 3;
            }

            public static class WindowMessage
            {
                public const uint CLOSE = 0x0010;
                public const uint QUIT = 0x0012;
                public const uint KEYDOWN = 0x0100;
                public const uint KEYUP = 0x0101;
                public const uint SYSKEYDOWN = 0x0104;
                public const uint SYSKEYUP = 0x0105;
            }

            public static class ChildWindowFlags
            {
                public const uint ALL = 0x0000;
                public const uint SKIPINVISIBLE = 0x0001;
                public const uint SKIPDISABLED = 0x0002;
                public const uint SKIPTRANSPARENT = 0x0004;
            }
        }
    }
}

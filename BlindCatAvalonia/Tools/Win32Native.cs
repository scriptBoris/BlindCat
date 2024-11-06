using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Tools;

public static class Win32Native
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    public const int VK_CONTROL = 0x11;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;

    public const int VK_MENU = 0x12; // Обозначение для клавиш Alt
    public const int VK_LMENU = 0xA4; // Левая клавиша Alt
    public const int VK_RMENU = 0xA5; // Правая клавиша Alt

    public static bool IsCtrlPressed()
    {
        // Проверяем состояние левой и правой клавиш Ctrl
        bool isLeftCtrlPressed = (Win32Native.GetKeyState(Win32Native.VK_LCONTROL) & 0x8000) != 0;
        bool isRightCtrlPressed = (Win32Native.GetKeyState(Win32Native.VK_RCONTROL) & 0x8000) != 0;
        return isLeftCtrlPressed || isRightCtrlPressed;
    }

    public static bool IsAltPressed()
    {
        // Проверяем состояние левой и правой клавиш Alt
        bool isLeftAltPressed = (GetKeyState(VK_LMENU) & 0x8000) != 0;
        bool isRightAltPressed = (GetKeyState(VK_RMENU) & 0x8000) != 0;
        return isLeftAltPressed || isRightAltPressed;
    }
}

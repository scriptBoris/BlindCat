using System.Runtime.InteropServices;
using BlindCatAvalonia.Services;
using BlindCatAvalonia.Tools;

namespace BlindCatAvalonia.Windows.Implementations;

public class KeyboardWin32 : IKeyboardNative
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_MENU = 0x12; // Обозначение для клавиш Alt
    private const int VK_LMENU = 0xA4; // Левая клавиша Alt
    private const int VK_RMENU = 0xA5; // Правая клавиша Alt

    public bool IsCtrlPressed
    {
        get
        {
            // Проверяем состояние левой и правой клавиш Ctrl
            bool isLeftCtrlPressed = (GetKeyState(VK_LCONTROL) & 0x8000) != 0;
            bool isRightCtrlPressed = (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
            return isLeftCtrlPressed || isRightCtrlPressed;
        }
    }

    public bool IsAltPressed
    {
        get
        {
            // Проверяем состояние левой и правой клавиш Alt
            bool isLeftAltPressed = (GetKeyState(VK_LMENU) & 0x8000) != 0;
            bool isRightAltPressed = (GetKeyState(VK_RMENU) & 0x8000) != 0;
            return isLeftAltPressed || isRightAltPressed;
        }
    }
}
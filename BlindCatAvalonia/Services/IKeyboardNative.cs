namespace BlindCatAvalonia.Services;

public interface IKeyboardNative
{
    bool IsCtrlPressed { get; }
    bool IsAltPressed { get; }
}

public class KeyboardDefault : IKeyboardNative
{
    public bool IsCtrlPressed { get; set; }
    public bool IsAltPressed { get; set; }
}
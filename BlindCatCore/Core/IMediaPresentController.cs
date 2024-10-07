using BlindCatCore.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BlindCatCore.Core;

public interface IMediaPresentController
{
    /// <summary>
    /// Верхние кнопки
    /// </summary>
    ReadOnlyObservableCollection<MPButtonContext> TopButtons { get; }

    /// <summary>
    /// Правая визуальная панель
    /// </summary>
    object? RightViewPanel { get; }

    /// <summary>
    /// Текст который будет отображаться в качестве титула для вью
    /// </summary>
    string? Title { get; }

    void ShowHideFileInfo();
    void OnConnected();
    void OnDisconnected();
    void OnMetaReceived(object? sender, FileMetaData[] e);
}
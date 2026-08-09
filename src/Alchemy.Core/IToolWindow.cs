using System;

namespace Alchemy.Core;

public interface IToolWindow
{
    bool IsVisible { get; }
    event EventHandler? Closed;
    void Show();
    void Activate();
}
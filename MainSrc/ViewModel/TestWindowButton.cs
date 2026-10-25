// TestWindowViewModel.cs
using Avalonia.Controls;
using System;
namespace Project.Launch.ViewModel
{
    public class TestWindowViewModel
    {
        public void ShowHelloWorld()
        {
            var messageBox = new Window
            {
                Title = "Window",
                Content = "Hello World!",
                Width = 200,
                Height = 100
            };
            messageBox.Show();
        }
    }
}
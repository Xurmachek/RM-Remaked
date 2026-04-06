using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace RMPlayer
{
    public static class Lang
    {
        private static Dictionary<string, string> _dict = new Dictionary<string, string>();
        public static string CurrentCode { get; private set; } = "ru";

        public static void Load(string langCode)
        {
            CurrentCode = langCode;
            string path = $"Languages/{langCode}.json";
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
        }

        public static string Get(string key) => _dict.ContainsKey(key) ? _dict[key] : key;

        public static bool ShowYesNo(string message, string title)
        {
            var win = new Window { Title = title, Width = 300, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            win.SetResourceReference(Window.BackgroundProperty, "WindowBackground");
            
            var stack = new StackPanel();
            var msg = new TextBlock { Text = message, Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap };
            msg.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryText");
            stack.Children.Add(msg);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var yes = new Button { Content = CurrentCode == "ru" ? "Да" : "Yes", Width = 70, Margin = new Thickness(5) };
            var no = new Button { Content = CurrentCode == "ru" ? "Нет" : "No", Width = 70, Margin = new Thickness(5) };
            
            bool result = false;
            yes.Click += (s, e) => { result = true; win.Close(); };
            no.Click += (s, e) => { win.Close(); };
            
            btnStack.Children.Add(yes); 
            btnStack.Children.Add(no);
            stack.Children.Add(btnStack);
            win.Content = stack;
            win.ShowDialog();
            return result;
        }
    }
}
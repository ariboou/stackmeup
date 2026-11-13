using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StackMeUp
{
    /// <summary>Küçük "Yeniden adlandır" penceresi. Uzantı hariç ad seçili gelir.</summary>
    public static class RenameDialog
    {
        public static string? Ask(string currentName, bool isDirectory)
        {
            var box = new TextBox { Text = currentName, FontSize = 14, Padding = new Thickness(6, 4, 6, 4) };
            var ok = new Button { Content = "Yeniden adlandır", IsDefault = true, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Vazgeç", IsCancel = true, Padding = new Thickness(14, 5, 14, 5) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(new TextBlock { Text = "Yeni ad:", Margin = new Thickness(0, 0, 0, 6) });
            root.Children.Add(box);
            root.Children.Add(buttons);

            var win = new Window
            {
                Title = "Yeniden adlandır", Content = root, Width = 420, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize,
                Topmost = true, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), ShowInTaskbar = false,
            };
            ok.Click += (_, _) =>
            {
                var name = box.Text.Trim();
                if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    MessageBox.Show(win, "Ad boş olamaz ve şu karakterleri içeremez:  \\ / : * ? \" < > |", "StackMeUp");
                    return;
                }
                win.DialogResult = true;
            };
            win.Loaded += (_, _) =>
            {
                box.Focus();
                int len = isDirectory ? currentName.Length : Path.GetFileNameWithoutExtension(currentName).Length;
                box.Select(0, len);
                Keyboard.Focus(box);
            };
            return win.ShowDialog() == true ? box.Text.Trim() : null;
        }
    }
}

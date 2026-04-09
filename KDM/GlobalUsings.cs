// Global using aliases để giải quyết xung đột namespace giữa WPF và WinForms
// Khi UseWindowsForms = true, cả System.Windows.Forms và System.Windows đều được import
// gây ra ambiguous references cho Application, MessageBox, Color, etc.

global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage = System.Windows.MessageBoxImage;
global using MessageBoxResult = System.Windows.MessageBoxResult;
global using Color = System.Windows.Media.Color;
global using Window = System.Windows.Window;
global using WindowState = System.Windows.WindowState;
global using Clipboard = System.Windows.Clipboard;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KID;

Console.WriteLine($"Hello!");

Console.ReadLine();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.BackgroundColor = ConsoleColor.Red;

Console.WriteLine($"Foreground: {Console.ForegroundColor}!");
Console.WriteLine($"BackgroundColor: {Console.BackgroundColor}!");

Console.ReadLine();

Console.Clear();
using WpfKey = global::System.Windows.Input.Key;

namespace KID
{
    /// <summary>
    /// Удобные константы клавиш, чтобы детям не нужно было писать using System.Windows.Input.
    /// </summary>
    public static class KeyboardKeys
    {
        public static WpfKey None => WpfKey.None;

        // --- Управление / сервисные ---
        public static WpfKey Enter => WpfKey.Enter;
        public static WpfKey Escape => WpfKey.Escape;
        public static WpfKey Space => WpfKey.Space;
        public static WpfKey Tab => WpfKey.Tab;
        public static WpfKey Backspace => WpfKey.Back;

        // --- Стрелки ---
        public static WpfKey Left => WpfKey.Left;
        public static WpfKey Right => WpfKey.Right;
        public static WpfKey Up => WpfKey.Up;
        public static WpfKey Down => WpfKey.Down;

        // --- Буквы (латиница) ---
        public static WpfKey A => WpfKey.A;
        public static WpfKey B => WpfKey.B;
        public static WpfKey C => WpfKey.C;
        public static WpfKey D => WpfKey.D;
        public static WpfKey E => WpfKey.E;
        public static WpfKey F => WpfKey.F;
        public static WpfKey G => WpfKey.G;
        public static WpfKey H => WpfKey.H;
        public static WpfKey I => WpfKey.I;
        public static WpfKey J => WpfKey.J;
        public static WpfKey K => WpfKey.K;
        public static WpfKey L => WpfKey.L;
        public static WpfKey M => WpfKey.M;
        public static WpfKey N => WpfKey.N;
        public static WpfKey O => WpfKey.O;
        public static WpfKey P => WpfKey.P;
        public static WpfKey Q => WpfKey.Q;
        public static WpfKey R => WpfKey.R;
        public static WpfKey S => WpfKey.S;
        public static WpfKey T => WpfKey.T;
        public static WpfKey U => WpfKey.U;
        public static WpfKey V => WpfKey.V;
        public static WpfKey W => WpfKey.W;
        public static WpfKey X => WpfKey.X;
        public static WpfKey Y => WpfKey.Y;
        public static WpfKey Z => WpfKey.Z;

        // --- Цифры (верхний ряд) ---
        public static WpfKey D0 => WpfKey.D0;
        public static WpfKey D1 => WpfKey.D1;
        public static WpfKey D2 => WpfKey.D2;
        public static WpfKey D3 => WpfKey.D3;
        public static WpfKey D4 => WpfKey.D4;
        public static WpfKey D5 => WpfKey.D5;
        public static WpfKey D6 => WpfKey.D6;
        public static WpfKey D7 => WpfKey.D7;
        public static WpfKey D8 => WpfKey.D8;
        public static WpfKey D9 => WpfKey.D9;

        // --- Функциональные ---
        public static WpfKey F1 => WpfKey.F1;
        public static WpfKey F2 => WpfKey.F2;
        public static WpfKey F3 => WpfKey.F3;
        public static WpfKey F4 => WpfKey.F4;
        public static WpfKey F5 => WpfKey.F5;
        public static WpfKey F6 => WpfKey.F6;
        public static WpfKey F7 => WpfKey.F7;
        public static WpfKey F8 => WpfKey.F8;
        public static WpfKey F9 => WpfKey.F9;
        public static WpfKey F10 => WpfKey.F10;
        public static WpfKey F11 => WpfKey.F11;
        public static WpfKey F12 => WpfKey.F12;
    }
}


﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenshotTool
{
    public class Shortcut
    {
        public static readonly string[] Specials = new string[] { "Shift", "Ctrl", "Alt" };
        public static readonly Keys[] AllKeys = (Keys[])Enum.GetValues(typeof(Keys));
        
        public bool Shift;
        public bool Ctrl;
        public bool Alt;
        public Keys Key;

        public bool[] SpecialKeys() => new bool[] { Shift, Ctrl, Alt };
        public string SpecialKey() {
            int i = SpecialKeys().ToList().FindIndex(x => x);
            if (i >= 0)
                return Specials[i];
            else
                return "";
        }

        public bool IsPressed(bool Shift, bool Ctrl, bool Alt, Keys Key)
        {
            var key = Key.ToString();
            if (key.Contains("Shift"))
                return this.Ctrl == Ctrl && this.Alt == Alt && this.Key.Equals(Key);
            else if (key.Contains("Control"))
                return this.Shift == Shift && this.Alt == Alt && this.Key.Equals(Key);
            else if (key.Contains("Alt"))
                return this.Shift == Shift && this.Ctrl == Ctrl && this.Key.Equals(Key);
            else
                return this.Shift == Shift && this.Ctrl == Ctrl && this.Alt == Alt && this.Key.Equals(Key);
        }

        public static Shortcut DefaultInstantKeys => new Shortcut() { Key = Keys.Pause };
        public static Shortcut DefaultCropKeys => new Shortcut() { Alt = true, Key = Keys.Pause };
        public static Shortcut DefaultGifKeys => new Shortcut() { Ctrl = true, Key = Keys.RControlKey };

        public override string ToString() => $"{Shift}|{Ctrl}|{Alt}|{Key}";
        public Shortcut FromString(string s)
        {
            string[] split = s.Split('|');
            Shift = bool.Parse(split[0]);
            Ctrl = bool.Parse(split[1]);
            Alt = bool.Parse(split[2]);
            Key = (Keys)Enum.Parse(typeof(Keys), split[3]);
            return this;
        }
    }
}

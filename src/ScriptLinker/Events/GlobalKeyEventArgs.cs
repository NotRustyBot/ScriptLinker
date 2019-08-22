﻿using ScriptLinker.Utilities;
using System.Windows.Input;

namespace ScriptLinker.Events
{
    public class GlobalKeyEventArgs
    {
        public HotKey HotKey { get; set; }

        public GlobalKeyEventArgs(Key key, bool shift, bool ctrl, bool alt)
        {
            var modifiers = ModifierKeys.None;

            if (shift)
                modifiers = modifiers.Add(ModifierKeys.Shift);
            if (ctrl)
                modifiers = modifiers.Add(ModifierKeys.Control);
            if (alt)
                modifiers = modifiers.Add(ModifierKeys.Alt);

            HotKey = new HotKey(key, modifiers);
        }
    }
}
using System;
using System.Diagnostics;
using Godot;

namespace Hsm
{
    public static partial class Client
    {
        public static void Log(StateMachine aStateMachine, string aMessage)
        {
            GD.Print(aMessage);
        }

        public static void LogError(StateMachine aStateMachine, string aMessage)
        {
            GD.PrintErr(aMessage);
            Debug.Assert(false, "You forgot to assign a Camera3D to the TRMoveController!");
        }
    }
}

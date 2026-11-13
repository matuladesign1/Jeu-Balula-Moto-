// BALULA MOTO — commandes : écran tactile (Android/iOS) et clavier (éditeur, PC).
// Compatible avec l'ancien Input Manager et le nouveau Input System (selon le réglage du projet).
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public enum GameKey { Throttle, Brake, BrakeRear, Left, Right, Horn, Lights, Camera, Action, Menu, IndicatorLeft, IndicatorRight, Reverse, Missions }

    public static class PlayerControls
    {
        // États des boutons tactiles (remplis par le HUD)
        public static float TouchThrottle;
        public static float TouchBrake;
        public static float TouchBrakeRear;
        public static float TouchSteer;
        public static bool TouchHorn;

        public static DriveInput Read(bool moto)
        {
            DriveInput d = new DriveInput();
            float kbSteer = (Held(GameKey.Right) ? 1f : 0f) - (Held(GameKey.Left) ? 1f : 0f);
            d.Steer = Mathf.Clamp(TouchSteer + kbSteer, -1f, 1f);
            d.Throttle = Mathf.Max(TouchThrottle, Held(GameKey.Throttle) ? 1f : 0f);
            d.FrontBrake = Mathf.Max(TouchBrake, Held(GameKey.Brake) ? 1f : 0f);
            d.RearBrake = Mathf.Max(TouchBrakeRear, Held(GameKey.BrakeRear) ? 1f : 0f);
            return d;
        }

        public static bool HornHeld() { return TouchHorn || Held(GameKey.Horn); }

        public static void ResetTouch()
        {
            TouchThrottle = 0f; TouchBrake = 0f; TouchBrakeRear = 0f; TouchSteer = 0f; TouchHorn = false;
        }

        public static bool Held(GameKey k) { return Raw(k, false); }
        public static bool Pressed(GameKey k) { return Raw(k, true); }

        private static bool Raw(GameKey k, bool down)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                foreach (Key key in KeysFor(k))
                {
                    var control = kb[key];
                    if (down ? control.wasPressedThisFrame : control.isPressed) return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            foreach (KeyCode code in CodesFor(k))
                if (down ? Input.GetKeyDown(code) : Input.GetKey(code)) return true;
#endif
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private static Key[] KeysFor(GameKey k)
        {
            switch (k)
            {
                case GameKey.Throttle: return new[] { Key.W, Key.UpArrow };
                case GameKey.Brake: return new[] { Key.S, Key.DownArrow };
                case GameKey.BrakeRear: return new[] { Key.Space };
                case GameKey.Left: return new[] { Key.A, Key.LeftArrow };
                case GameKey.Right: return new[] { Key.D, Key.RightArrow };
                case GameKey.Horn: return new[] { Key.H };
                case GameKey.Lights: return new[] { Key.L };
                case GameKey.Camera: return new[] { Key.C };
                case GameKey.Action: return new[] { Key.F, Key.Enter };
                case GameKey.Menu: return new[] { Key.Escape };
                case GameKey.IndicatorLeft: return new[] { Key.Q };
                case GameKey.IndicatorRight: return new[] { Key.E };
                case GameKey.Reverse: return new[] { Key.R };
                case GameKey.Missions: return new[] { Key.M };
            }
            return new Key[0];
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private static KeyCode[] CodesFor(GameKey k)
        {
            switch (k)
            {
                case GameKey.Throttle: return new[] { KeyCode.W, KeyCode.UpArrow };
                case GameKey.Brake: return new[] { KeyCode.S, KeyCode.DownArrow };
                case GameKey.BrakeRear: return new[] { KeyCode.Space };
                case GameKey.Left: return new[] { KeyCode.A, KeyCode.LeftArrow };
                case GameKey.Right: return new[] { KeyCode.D, KeyCode.RightArrow };
                case GameKey.Horn: return new[] { KeyCode.H };
                case GameKey.Lights: return new[] { KeyCode.L };
                case GameKey.Camera: return new[] { KeyCode.C };
                case GameKey.Action: return new[] { KeyCode.F, KeyCode.Return };
                case GameKey.Menu: return new[] { KeyCode.Escape };
                case GameKey.IndicatorLeft: return new[] { KeyCode.Q };
                case GameKey.IndicatorRight: return new[] { KeyCode.E };
                case GameKey.Reverse: return new[] { KeyCode.R };
                case GameKey.Missions: return new[] { KeyCode.M };
            }
            return new KeyCode[0];
        }
#endif
    }
}

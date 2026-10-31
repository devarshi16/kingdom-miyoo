// Clean-room replacement for the parts of the Rewired input API that Kingdom uses at runtime.
// Actions come from the game's own serialized Rewired setup (InputManager._userData.actions);
// each action is bound by *name* to the handheld/desktop buttons (see docs/DECISIONS.md D11).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewired.Data
{
    [Serializable]
    public sealed class KnlActionData { public int _id; public string _name = ""; public int _type; public string _descriptiveName = ""; public string _positiveDescriptiveName = ""; public string _negativeDescriptiveName = ""; public int _categoryId; public bool _userAssignable; public int _behaviorId; }

    public partial class UserData
    {
        [SerializeField] internal List<KnlActionData> actions = new List<KnlActionData>();
    }

    public abstract partial class UserDataStore
    {
        public virtual void Load() { }
    }
}

namespace Rewired
{
    public abstract partial class InputManager_Base
    {
        [SerializeField] internal Rewired.Data.UserData _userData;
        [SerializeField] internal bool _dontDestroyOnLoad;
        public Rewired.Data.UserData userData { get { return _userData; } }
        public Rewired.Platforms.Platform platform;
        public Rewired.Platforms.EditorPlatform editorPlatform;
        public Rewired.Platforms.WebplayerPlatform webplayerPlatform;
        public bool isEditor;

        // The game's InputManager subclass may define its own Awake; Unity would still run the
        // base initialisation, so ReInput also initialises lazily on first use.
        protected virtual void Awake()
        {
            ReInput.Init(this);
            if (_dontDestroyOnLoad) UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }
    }

    public static partial class ReInput
    {
        internal static InputManager_Base s_Manager;
        static PlayerHelper s_Players; static ControllerHelper s_Controllers; static MappingHelper s_Mapping;
        internal static readonly Dictionary<int, InputAction> s_Actions = new Dictionary<int, InputAction>();
        internal static readonly Dictionary<string, int> s_ActionIds = new Dictionary<string, int>();

        internal static void Init(InputManager_Base m)
        {
            if (s_Manager != null) return;
            s_Manager = m;
            s_Actions.Clear(); s_ActionIds.Clear();
            if (m != null && m._userData != null && m._userData.actions != null)
                foreach (var a in m._userData.actions)
                {
                    var ia = new InputAction();
                    ia.m_Id = a._id; ia.m_Name = a._name; ia.m_Type = a._type; ia.m_Desc = a._descriptiveName;
                    ia.m_PosDesc = a._positiveDescriptiveName; ia.m_NegDesc = a._negativeDescriptiveName;
                    ia.m_Category = a._categoryId; ia.m_UserAssignable = a._userAssignable; ia.m_Behavior = a._behaviorId;
                    s_Actions[a._id] = ia; s_ActionIds[a._name] = a._id;
                }
            KNL.Log.Info("Rewired (KNL): " + s_Actions.Count + " actions bound");
        }
        static void Ensure() { if (s_Manager == null) Init(UnityEngine.Object.FindObjectOfType<InputManager_Base>()); }

        public static bool isReady { get { Ensure(); return s_Manager != null; } }
        public static PlayerHelper players { get { Ensure(); return s_Players ?? (s_Players = new PlayerHelper()); } }
        public static ControllerHelper controllers { get { return s_Controllers ?? (s_Controllers = new ControllerHelper()); } }
        public static MappingHelper mapping { get { Ensure(); return s_Mapping ?? (s_Mapping = new MappingHelper()); } }
        public static Rewired.Interfaces.IUserDataStore userDataStore { get { return null; } }

        static Action<ControllerStatusChangedEventArgs> s_Conn, s_Disc, s_PreDisc; static Action s_Recompile, s_SourceUpdate;
        public static event Action<ControllerStatusChangedEventArgs> ControllerConnectedEvent { add { s_Conn += value; } remove { s_Conn -= value; } }
        public static event Action<ControllerStatusChangedEventArgs> ControllerDisconnectedEvent { add { s_Disc += value; } remove { s_Disc -= value; } }
        public static event Action<ControllerStatusChangedEventArgs> ControllerPreDisconnectEvent { add { s_PreDisc += value; } remove { s_PreDisc -= value; } }
        public static event Action EditorRecompileEvent { add { s_Recompile += value; } remove { s_Recompile -= value; } }
        public static event Action InputSourceUpdateEvent { add { s_SourceUpdate += value; } remove { s_SourceUpdate -= value; } }

        public sealed partial class PlayerHelper
        {
            readonly Player m_Player0 = new Player(0, "Player0"), m_System = new Player(9999999, "System");
            public Player GetPlayer(int id) { return id == 9999999 ? m_System : m_Player0; }
            public Player GetPlayer(string name) { return name == "System" ? m_System : m_Player0; }
            public Player GetSystemPlayer() { return m_System; }
            public Player SystemPlayer { get { return m_System; } }
            public IList<Player> Players { get { return new[] { m_Player0 }; } }
            public IList<Player> AllPlayers { get { return new[] { m_System, m_Player0 }; } }
            public int playerCount { get { return 1; } }
        }

        public sealed partial class MappingHelper
        {
            public InputAction GetAction(int id) { InputAction a; return s_Actions.TryGetValue(id, out a) ? a : null; }
            public InputAction GetAction(string name) { int id; return s_ActionIds.TryGetValue(name, out id) ? GetAction(id) : null; }
            public int GetActionId(string name) { int id; return s_ActionIds.TryGetValue(name, out id) ? id : -1; }
            public IEnumerable<InputAction> Actions { get { return s_Actions.Values; } }
        }
    }

    public sealed partial class InputAction
    {
        internal int m_Id, m_Type, m_Category, m_Behavior; internal string m_Name = "", m_Desc = "", m_PosDesc = "", m_NegDesc = ""; internal bool m_UserAssignable;
        public int id { get { return m_Id; } }
        public string name { get { return m_Name; } }
        public InputActionType type { get { return (InputActionType)m_Type; } }
        public string descriptiveName { get { return m_Desc; } }
        public string positiveDescriptiveName { get { return m_PosDesc; } }
        public string negativeDescriptiveName { get { return m_NegDesc; } }
        public int categoryId { get { return m_Category; } }
        public int behaviorId { get { return m_Behavior; } }
        public bool userAssignable { get { return m_UserAssignable; } }
    }

    public sealed partial class Player
    {
        readonly int m_Id; readonly string m_Name;
        public ControllerHelper controllers = new ControllerHelper();
        internal Player(int id, string name) { m_Id = id; m_Name = name; }
        public int id { get { return m_Id; } }
        public string name { get { return m_Name; } }
        public string descriptiveName { get { return m_Name; } }

        // ---- action -> buttons (by the game's action names)
        static uint Pos(string action)
        {
            switch (action)
            {
                case "Horizontal": case "HorizontalP2": return KNL.Buttons.Right;
                case "Vertical": return KNL.Buttons.Up;
                case "Pay": return KNL.Buttons.Down | KNL.Buttons.A | KNL.Buttons.X;
                case "Gallop": return KNL.Buttons.B | KNL.Buttons.L | KNL.Buttons.R;
                case "Submit": return KNL.Buttons.A;
                case "Start": return KNL.Buttons.Start;
                case "Cancel": return KNL.Buttons.B | KNL.Buttons.Select;
                default: return 0;
            }
        }
        static uint Neg(string action)
        {
            switch (action)
            {
                case "Horizontal": case "HorizontalP2": return KNL.Buttons.Left;
                case "Vertical": return KNL.Buttons.Down;
                default: return 0;
            }
        }
        static string NameOf(int id) { InputAction a; return ReInput.s_Actions.TryGetValue(id, out a) ? a.m_Name : ""; }
        static int IdOf(string n) { int id; return ReInput.s_ActionIds.TryGetValue(n, out id) ? id : -1; }

        public bool GetButton(int id) { var n = NameOf(id); return KNL.Buttons.Held(Pos(n)); }
        public bool GetButtonDown(int id) { var n = NameOf(id); return KNL.Buttons.Pressed(Pos(n)); }
        public bool GetButtonUp(int id) { var n = NameOf(id); return KNL.Buttons.Released(Pos(n)); }
        public bool GetNegativeButton(int id) { return KNL.Buttons.Held(Neg(NameOf(id))); }
        public bool GetNegativeButtonDown(int id) { return KNL.Buttons.Pressed(Neg(NameOf(id))); }
        public bool GetButtonDoublePressDown(int id) { return KNL.Buttons.DoublePressed(Pos(NameOf(id))); }
        public bool GetNegativeButtonDoublePressDown(int id) { return KNL.Buttons.DoublePressed(Neg(NameOf(id))); }
        public float GetAxis(int id) { return GetAxisRaw(id); }
        public float GetAxisRaw(int id)
        {
            var n = NameOf(id);
            return (KNL.Buttons.Held(Pos(n)) ? 1f : 0f) - (KNL.Buttons.Held(Neg(n)) ? 1f : 0f);
        }
        public float GetAxisTimeActive(int id) { return GetAxisRawTimeActive(id); }
        public float GetAxisRawTimeActive(int id) { var n = NameOf(id); return KNL.Buttons.TimeHeld(Pos(n) | Neg(n)); }

        public bool GetButton(string a) { return GetButton(IdOf(a)); }
        public bool GetButtonDown(string a) { return GetButtonDown(IdOf(a)); }
        public bool GetButtonUp(string a) { return GetButtonUp(IdOf(a)); }
        public bool GetNegativeButtonDown(string a) { return GetNegativeButtonDown(IdOf(a)); }
        public float GetAxis(string a) { return GetAxis(IdOf(a)); }
        public float GetAxisRaw(string a) { return GetAxisRaw(IdOf(a)); }

        public void AddInputEventDelegate(Action<InputActionEventData> cb, UpdateLoopType u, InputActionEventType t, int actionId) { }
        public void RemoveInputEventDelegate(Action<InputActionEventData> cb, UpdateLoopType u, InputActionEventType t, int actionId) { }
    }
}


namespace Rewired
{
    public static partial class ReInput
    {
        public sealed partial class ControllerHelper
        {
            static readonly Keyboard s_Keyboard = new Keyboard();
            static readonly Mouse s_Mouse = new Mouse();
            public Keyboard Keyboard { get { return s_Keyboard; } }
            public Mouse Mouse { get { return s_Mouse; } }
            public System.Collections.Generic.IList<Joystick> Joysticks { get { return KnlPad.List; } }
            public int joystickCount { get { return KnlPad.List.Length; } }
            public Joystick GetJoystick(int id) { return KnlPad.List.Length > 0 && id == 0 ? KnlPad.List[0] : null; }
            public bool IsControllerAssigned(ControllerType t, int id) { return t == ControllerType.Keyboard || (t == ControllerType.Joystick && KnlPad.List.Length > 0); }
        }
    }
}

namespace Rewired
{
    public sealed partial class Mouse
    {
        public UnityEngine.Vector2 screenPosition { get { return UnityEngine.Vector2.zero; } }
        public UnityEngine.Vector2 screenPositionDelta { get { return UnityEngine.Vector2.zero; } }
    }
}

namespace Rewired
{
    public abstract partial class Controller
    {
        public int id;
        public virtual bool GetButton(int i) { return false; }
        public virtual bool GetButtonDown(int i) { return false; }
        public virtual bool GetButtonUp(int i) { return false; }
        public virtual string name { get { return GetType().Name; } }
        public string hardwareName { get { return name; } }
        public string hardwareIdentifier { get { return name; } }
        public string mapTypeString { get { return name; } }
    }
    public sealed partial class Keyboard
    {
        public override string name { get { return "Keyboard"; } }
        public ControllerType type { get { return ControllerType.Keyboard; } }
        public bool GetKey(UnityEngine.KeyCode k) { return UnityEngine.Input.GetKey(k); }
        public bool GetKeyDown(UnityEngine.KeyCode k) { return UnityEngine.Input.GetKeyDown(k); }
        public float GetKeyTimePressed(UnityEngine.KeyCode k) { return 0f; }
        public System.Collections.Generic.IEnumerable<ControllerPollingInfo> PollForAllKeys() { return new ControllerPollingInfo[0]; }
        public static string GetKeyName(UnityEngine.KeyCode k) { return k.ToString(); }
        public static bool IsModifierKey(UnityEngine.KeyCode k) { return false; }
    }
}

namespace Rewired
{
    public sealed partial class Player
    {
        public sealed partial class ControllerHelper
        {
            public T GetControllerWithTag<T>(string tag) where T : Controller { return null; }
            public Keyboard Keyboard { get { return ReInput.controllers.Keyboard; } }
            public System.Collections.Generic.IList<Joystick> Joysticks { get { return KnlPad.List; } }
            public int joystickCount { get { return KnlPad.List.Length; } }
            public bool hasMouse { get; set; }
            public bool ContainsController(ControllerType t, int id) { return t == ControllerType.Keyboard || (t == ControllerType.Joystick && KnlPad.List.Length > 0); }
            public void AddController(Controller c, bool removeFromOtherPlayers) { }
            public void AddController(ControllerType t, int id, bool removeFromOtherPlayers) { }
            public void RemoveController(ControllerType t, int id) { }
        }
    }
}

namespace Rewired
{
    /// The device's built-in controls as one Rewired joystick (gamepad mode only, see KNL.Buttons.GamepadMode).
    public sealed class KnlPad : Joystick
    {
        public static readonly Joystick[] List = KNL.Buttons.GamepadMode ? new Joystick[] { new KnlPad() } : new Joystick[0];
        public override string name { get { return "Miyoo Mini Plus"; } }
    }
}
namespace Rewired
{
    public partial class Joystick
    {
        // a generic gamepad; the game only uses it to pick a glyph set
        public System.Guid hardwareTypeGuid { get { return System.Guid.Empty; } }
    }
}

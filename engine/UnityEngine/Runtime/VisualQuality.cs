// Optional decoration budget. KNL_VISUALS=lite is selected by the launcher; an unset
// value (or "full") retains the original compatibility renderer and script dispatch.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace KNL
{
    public static class VisualQuality
    {
        static bool configured, lite;
        // The launcher's environment is process configuration. Renderer hot loops read this
        // cached flag; developer tools changing it in-process must explicitly refresh it.
        public static bool Lite { get { if (!configured) Refresh(); return lite; } }
        public static void Refresh() {
            lite = string.Equals(Environment.GetEnvironmentVariable("KNL_VISUALS"), "lite", StringComparison.OrdinalIgnoreCase);
            configured = true;
        }

        // The shipped main scene has dedicated decorative cloud controllers (160
        // cumulii, 30 fog sheets, 25 mists, 10 stratus at maximum weather density).
        // Budget before their first Awake/Update creates any pooled cloud objects.
        // World weather/density/time still advance; only these named decorations shrink.
        public static void BeforeAwake(MonoBehaviour mb)
        {
            LiteWeather.Observe(mb);
            if (!Lite || mb == null || mb.GetType().FullName != "CloudController") return;
            var max = mb.GetType().GetField("maxClouds", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (max == null || max.FieldType != typeof(int) || mb.gameObject == null) return;
            int cap;
            switch (mb.gameObject.name)
            {
                case "CloudsCumulus": cap = 4; break;
                case "Fogs": cap = 0; break;
                case "Mists": cap = 0; break;
                case "CloudsStratus": cap = 1; break;
                default: return;
            }
            max.SetValue(mb, Math.Min((int)max.GetValue(mb), cap));
        }

        // ParticleSystemRenderer is not drawn by this port. The shipped game only writes
        // emitter settings; it never reads the resulting particles for gameplay. Keep
        // Awake/Start/OnEnable and all emitter APIs intact, but avoid computing invisible
        // cosmetic particles. If an event or particle-tracking consumer appears, run the
        // original manager instead. This check is repeated so later subscriptions work.
        public static Action WrapLateUpdate(MonoBehaviour mb, Action original)
        {
            if (mb.GetType().FullName != "ParticlePlayground.PlaygroundC") return original;
            var policy = new ParticleBudget(mb, original);
            return policy.Tick;
        }

        sealed class ParticleBudget
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            readonly object manager;
            readonly Action original, setTime;
            readonly Func<object,IList> systems, manipulators;
            readonly Func<object,Delegate>[] callbacks;
            readonly Dictionary<Type, ConsumerFields> consumers = new Dictionary<Type, ConsumerFields>();
            bool reported;

            sealed class ConsumerFields
            {
                public Func<object,ICollection> events;
                public Func<object,IList> manipulators;
                public Func<object,bool> track;
                public Func<object,Delegate>[] callbacks;
            }

            // Compile field access once per concrete type instead of reflecting over every
            // particle emitter every frame. The same live callbacks/lists/flags are checked,
            // so a new gameplay consumer immediately restores the original simulation.
            static Func<object,T> Getter<T>(FieldInfo field) {
                if (field == null) return null;
                // Preserve the old `as IList`/`as ICollection` behavior on unknown shapes;
                // never emit unverifiable IL for a field with an incompatible declared type.
                if (!typeof(T).IsAssignableFrom(field.FieldType))
                    return owner => { object value = field.GetValue(field.IsStatic ? null : owner); return value is T ? (T)value : default(T); };
                try {
                    var method = new DynamicMethod("knl_visual_"+field.Name, typeof(T), new[] { typeof(object) }, typeof(VisualQuality).Module, true);
                    var il = method.GetILGenerator();
                    if (!field.IsStatic) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, field.DeclaringType); }
                    il.Emit(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
                    il.Emit(OpCodes.Ret);
                    return (Func<object,T>)method.CreateDelegate(typeof(Func<object,T>));
                } catch (NotSupportedException) { }
                  catch (MemberAccessException) { }
                  catch (ArgumentException) { }
                // Remain correct on runtimes that cannot generate an accessor.
                return owner => (T)field.GetValue(field.IsStatic ? null : owner);
            }
            static Func<object,Delegate>[] CallbackFields(Type t)
            {
                var list = new List<Func<object,Delegate>>();
                foreach (var f in t.GetFields(Fields | BindingFlags.Static))
                    if (f.Name.StartsWith("particleEvent", StringComparison.Ordinal) && typeof(Delegate).IsAssignableFrom(f.FieldType)) list.Add(Getter<Delegate>(f));
                return list.ToArray();
            }

            public ParticleBudget(object mb, Action runOriginal)
            {
                manager = mb; original = runOriginal;
                var t = mb.GetType();
                systems = Getter<IList>(t.GetField("particleSystems", Fields));
                manipulators = Getter<IList>(t.GetField("manipulators", Fields));
                callbacks = CallbackFields(t);
                var clock = t.GetMethod("SetTime", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (clock != null) setTime = (Action)Delegate.CreateDelegate(typeof(Action), clock);
            }

            static bool HasCallbacks(object o, Func<object,Delegate>[] fields)
            {
                foreach (var f in fields) if (f(o) != null) return true;
                return false;
            }

            bool HasConsumers(object o)
            {
                if (o == null) return false;
                ConsumerFields c;
                var t = o.GetType();
                if (!consumers.TryGetValue(t, out c))
                {
                    c = new ConsumerFields { events = Getter<ICollection>(t.GetField("events", Fields)), manipulators = Getter<IList>(t.GetField("manipulators", Fields)), track = Getter<bool>(t.GetField("trackParticles", Fields)), callbacks = CallbackFields(t) };
                    consumers[t] = c;
                }
                if (HasCallbacks(o, c.callbacks) || (c.track != null && c.track(o))) return true;
                if (c.events != null)
                {
                    var events = c.events(o);
                    if (events != null && events.Count > 0) return true;
                }
                return c.manipulators != null && ListHasConsumers(c.manipulators(o));
            }

            bool ListHasConsumers(IList list)
            {
                if (list != null) for (int i = 0; i < list.Count; i++) if (HasConsumers(list[i])) return true;
                return false;
            }

            public void Tick()
            {
                if (!Lite || systems == null || manipulators == null || setTime == null ||
                    HasCallbacks(manager, callbacks) || ListHasConsumers(manipulators(manager)) || ListHasConsumers(systems(manager)))
                { original(); return; }
                setTime();
                if (!reported) { reported = true; Console.Error.WriteLine("[visuals] lite: bypassing unrendered cosmetic particle calculations; emitter APIs retained"); }
            }
        }

        // Fill the entire same water region as the mirror pass, with no stale framebuffer
        // holes. Ambient still follows time/weather; water tint follows the original script.
        public static void WaterColors(Color tint, Color ambient, out Color top, out Color bottom)
        {
            top = new Color(ambient.r * .24f + tint.r * .32f, ambient.g * .30f + tint.g * .32f, ambient.b * .38f + tint.b * .32f, 1f);
            bottom = new Color(top.r * .55f, top.g * .60f, top.b * .70f, 1f);
        }
    }
}

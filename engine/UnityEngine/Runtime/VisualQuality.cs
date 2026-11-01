// Optional decoration budget. KNL_VISUALS=lite is selected by the launcher; an unset
// value (or "full") retains the original compatibility renderer and script dispatch.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KNL
{
    public static class VisualQuality
    {
        public static bool Lite { get { return string.Equals(Environment.GetEnvironmentVariable("KNL_VISUALS"), "lite", StringComparison.OrdinalIgnoreCase); } }

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
            readonly FieldInfo systems, manipulators;
            readonly FieldInfo[] callbacks;
            readonly Dictionary<Type, ConsumerFields> consumers = new Dictionary<Type, ConsumerFields>();
            bool reported;

            sealed class ConsumerFields
            {
                public FieldInfo events, manipulators, track;
                public FieldInfo[] callbacks;
            }

            static FieldInfo[] CallbackFields(Type t)
            {
                var list = new List<FieldInfo>();
                foreach (var f in t.GetFields(Fields | BindingFlags.Static))
                    if (f.Name.StartsWith("particleEvent", StringComparison.Ordinal) && typeof(Delegate).IsAssignableFrom(f.FieldType)) list.Add(f);
                return list.ToArray();
            }

            public ParticleBudget(object mb, Action runOriginal)
            {
                manager = mb; original = runOriginal;
                var t = mb.GetType();
                systems = t.GetField("particleSystems", Fields);
                manipulators = t.GetField("manipulators", Fields);
                callbacks = CallbackFields(t);
                var clock = t.GetMethod("SetTime", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (clock != null) setTime = (Action)Delegate.CreateDelegate(typeof(Action), clock);
            }

            static bool HasCallbacks(object o, FieldInfo[] fields)
            {
                foreach (var f in fields) if (f.GetValue(f.IsStatic ? null : o) != null) return true;
                return false;
            }

            bool HasConsumers(object o)
            {
                if (o == null) return false;
                ConsumerFields c;
                var t = o.GetType();
                if (!consumers.TryGetValue(t, out c))
                {
                    c = new ConsumerFields { events = t.GetField("events", Fields), manipulators = t.GetField("manipulators", Fields), track = t.GetField("trackParticles", Fields), callbacks = CallbackFields(t) };
                    consumers[t] = c;
                }
                if (HasCallbacks(o, c.callbacks) || (c.track != null && (bool)c.track.GetValue(o))) return true;
                if (c.events != null)
                {
                    var events = c.events.GetValue(o) as ICollection;
                    if (events != null && events.Count > 0) return true;
                }
                return c.manipulators != null && ListHasConsumers(c.manipulators.GetValue(o) as IList);
            }

            bool ListHasConsumers(IList list)
            {
                if (list != null) for (int i = 0; i < list.Count; i++) if (HasConsumers(list[i])) return true;
                return false;
            }

            public void Tick()
            {
                if (!Lite || systems == null || manipulators == null || setTime == null ||
                    HasCallbacks(manager, callbacks) || ListHasConsumers(manipulators.GetValue(manager) as IList) || ListHasConsumers(systems.GetValue(manager) as IList))
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

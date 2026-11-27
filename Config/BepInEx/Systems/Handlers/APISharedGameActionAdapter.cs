// Config/BepInEx/Systems/Handlers/APISharedGameActionAdapter.cs
//
// PURPOSE: Runs the recruit gate (MakeTroopRecruitHook) as a registration on APIShared's GameActionEvents
//          (Serpens66's shared library, BepInEx GUID APIShared_Serp) instead of the mod's own MonoMod hook on
//          EngineInterface.GameAction (2.11.12, GitLab #17, plan FWP-U-20 / P6-01). APIShared owns one hook for
//          that method; Serpens's UnitLimit, UnitCosts and others register there. Two separate hooks on the same
//          call each reserved recruits the other might still trim or veto (phantom reservations).
//
// SOFT DEPENDENCY, NO BUILD REFERENCE: this file never names an APIShared type. Everything is found by reflection
//   in the loaded APIShared assembly, and every member the gate uses is checked before anything is registered
//   (capability, not version: the author's game had 0.9.2, development is past 0.70). Missing assembly, missing
//   member or a failed registration -> TryRegister returns false and the mod installs its own hook as before.
//   Plugin.cs declares a BepInEx SoftDependency on APIShared_Serp so it loads first when it is installed.
//
// CONTRACT USED (APIShared GameActionEvents, present from 0.6.0; checked against 0.9.2 and origin/develop d2c9e9f):
//   bool TryRegister(string ownerGuid, string registrationId, Action<GameActionPreEventArgs> pre,
//                    Action<GameActionPostEventArgs> post, out string reason, int order, Action<GameActionAcceptedEventArgs> accepted)
//   Pre: Command, StructureId (get/set; MakeTroop amount), ActionState (unit type), SkipOriginalFunction (get/set, a
//        sticky veto), State (registration-private). Accepted: frozen final inputs of an UNVETOED call, right before
//        the game's original; StructureId, ActionState, State. Registered on the Unity startup thread (Plugin.Awake ->
//        LibraryLoaded -> BepInExConfigManager.Initialize). Callbacks run on the caller's thread.
//
// SAME DECISION AS THE OWN HOOK: Pre calls MakeTroopRecruitHook.TryDecide (no reservation): refuse -> veto, trim ->
//   StructureId = allowed, else untouched. Accepted reserves the approved amount, so a later participant's veto or
//   trim leaves no reservation behind. Order 1000: after participants at the default order 0 (UnitLimit, UnitCosts),
//   so the gate sees their final request and nobody changes it after the gate. An exception in Pre is logged and
//   rethrown: APIShared then rolls back this registration's changes, which matches the own hook forwarding the
//   request unchanged after an error.
//
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    internal static class APISharedGameActionAdapter
    {
        internal const string APISharedGuid = "APIShared_Serp";
        internal const string RegistrationId = "RecruitCapsAndHorses";
        internal const int RegistrationOrder = 1000;

        private const string EventsTypeName = "APIShared.Commands.GameActionEvents";
        private const string PreTypeName = "APIShared.Commands.GameActionPreEventArgs";
        private const string PostTypeName = "APIShared.Commands.GameActionPostEventArgs";
        private const string AcceptedTypeName = "APIShared.Commands.GameActionAcceptedEventArgs";

        /// <summary>Typed access to APIShared's Pre arguments (or a test double with the same members).</summary>
        internal sealed class PreAccess
        {
            public Func<object, global::Enums.GameActionCommand> Command;
            public Func<object, int> GetStructureId;
            public Action<object, int> SetStructureId;
            public Func<object, int> GetActionState;
            public Func<object, bool> GetSkip;
            public Action<object, bool> SetSkip;
            public Action<object, object> SetState;
        }

        /// <summary>Typed access to APIShared's Accepted arguments (or a test double).</summary>
        internal sealed class AcceptedAccess
        {
            public Func<object, int> GetStructureId;
            public Func<object, int> GetActionState;
            public Func<object, object> GetState;
        }

        /// <summary>What Pre approved, kept in the registration's State until Accepted.</summary>
        internal sealed class Approval
        {
            public MakeTroopRecruitHook.RecruitDecision Decision;
            /// <summary>The first payload the gate left: the trimmed amount, or the request as it came.</summary>
            public int ForwardedStructureId;
        }

        private static PreAccess _pre;
        private static AcceptedAccess _accepted;

        // ------------------------------------------------------------------
        // Registration
        // ------------------------------------------------------------------

        /// <summary>
        /// Registers the gate with APIShared. False (with the reason) when APIShared is absent, lacks a member the
        /// gate needs, or refuses the registration; the caller then installs the mod's own hook.
        /// </summary>
        internal static bool TryRegister(out string reason)
        {
            try
            {
                Assembly asm = FindAPIShared(out string version);
                if (asm == null) { reason = "APIShared is not installed"; return false; }

                if (!TryBindAssembly(asm, out Type tPre, out Type tPost, out Type tAccepted, out MethodInfo tryRegister,
                                     out PreAccess pre, out AcceptedAccess accepted, out string missing))
                {
                    reason = $"APIShared {version} lacks {missing}";
                    return false;
                }

                _pre = pre;
                _accepted = accepted;
                object preDelegate = MakeAction(tPre, OnPre);
                object acceptedDelegate = MakeAction(tAccepted, OnAccepted);
                var args = new object[] { PluginInfo.PLUGIN_GUID, RegistrationId, preDelegate, null, null, RegistrationOrder, acceptedDelegate };
                bool ok = (bool)tryRegister.Invoke(null, args);
                if (!ok)
                {
                    reason = $"APIShared {version} refused the registration: {args[4] as string ?? "no reason given"}";
                    return false;
                }
                reason = $"APIShared {version}, order {RegistrationOrder}";
                return true;
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                reason = $"registration failed: {inner.GetType().Name}: {inner.Message}";
                return false;
            }
        }

        /// <summary>The loaded APIShared plugin's assembly (BepInEx chainloader), or null.</summary>
        private static Assembly FindAPIShared(out string version)
        {
            version = "?";
            if (global::BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(APISharedGuid, out var info) && info?.Instance != null)
            {
                version = info.Metadata?.Version?.ToString() ?? "?";
                return info.Instance.GetType().Assembly;
            }
            return null;
        }

        /// <summary>
        /// Capability check without registering: every type, the TryRegister overload and every member the gate uses.
        /// Also used offline against an APIShared DLL (no game needed).
        /// </summary>
        internal static bool CheckCapability(Assembly asm, out string missing) =>
            TryBindAssembly(asm, out _, out _, out _, out _, out _, out _, out missing);

        private static bool TryBindAssembly(Assembly asm, out Type tPre, out Type tPost, out Type tAccepted, out MethodInfo tryRegister,
                                            out PreAccess pre, out AcceptedAccess accepted, out string missing)
        {
            tryRegister = null;
            pre = null;
            accepted = null;
            Type tEvents = asm.GetType(EventsTypeName, false);
            tPre = asm.GetType(PreTypeName, false);
            tPost = asm.GetType(PostTypeName, false);
            tAccepted = asm.GetType(AcceptedTypeName, false);
            var absent = new List<string>();
            if (tEvents == null) absent.Add(EventsTypeName);
            if (tPre == null) absent.Add(PreTypeName);
            if (tPost == null) absent.Add(PostTypeName);
            if (tAccepted == null) absent.Add(AcceptedTypeName);
            if (absent.Count > 0) { missing = string.Join(", ", absent); return false; }

            tryRegister = tEvents.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static, null, new[]
            {
                typeof(string), typeof(string), typeof(Action<>).MakeGenericType(tPre), typeof(Action<>).MakeGenericType(tPost),
                typeof(string).MakeByRefType(), typeof(int), typeof(Action<>).MakeGenericType(tAccepted)
            }, null);
            if (tryRegister == null || tryRegister.ReturnType != typeof(bool))
            {
                missing = "GameActionEvents.TryRegister(string, string, Action<Pre>, Action<Post>, out string, int, Action<Accepted>)";
                return false;
            }

            return TryBind(tPre, tAccepted, out pre, out accepted, out missing);
        }

        /// <summary>Builds the accessors for the given Pre / Accepted types; false lists what is missing.</summary>
        internal static bool TryBind(Type tPre, Type tAccepted, out PreAccess pre, out AcceptedAccess accepted, out string missing)
        {
            var absent = new List<string>();
            pre = new PreAccess
            {
                Command = Getter<global::Enums.GameActionCommand>(tPre, "Command", absent),
                GetStructureId = Getter<int>(tPre, "StructureId", absent),
                SetStructureId = Setter<int>(tPre, "StructureId", absent),
                GetActionState = Getter<int>(tPre, "ActionState", absent),
                GetSkip = Getter<bool>(tPre, "SkipOriginalFunction", absent),
                SetSkip = Setter<bool>(tPre, "SkipOriginalFunction", absent),
                SetState = Setter<object>(tPre, "State", absent),
            };
            accepted = new AcceptedAccess
            {
                GetStructureId = Getter<int>(tAccepted, "StructureId", absent),
                GetActionState = Getter<int>(tAccepted, "ActionState", absent),
                GetState = Getter<object>(tAccepted, "State", absent),
            };
            missing = string.Join(", ", absent);
            if (absent.Count == 0) return true;
            pre = null;
            accepted = null;
            return false;
        }

        // ------------------------------------------------------------------
        // Callbacks (APIShared calls them on the GameAction caller's thread)
        // ------------------------------------------------------------------

        private static void OnPre(object args) => Pre(_pre, args);

        private static void OnAccepted(object args) => Accepted(_accepted, args);

        /// <summary>The gate on one Pre notification (internal for the tests).</summary>
        internal static void Pre(PreAccess access, object args)
        {
            if (access.Command(args) != global::Enums.GameActionCommand.MakeTroop || access.GetSkip(args)) return;
            try
            {
                int structureId = access.GetStructureId(args);
                if (!MakeTroopRecruitHook.TryDecide(structureId, access.GetActionState(args), reserve: false, out var decision))
                    return;
                ApplyDecision(access, args, structureId, decision);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[UnitCaps] MakeTroop hook error: {ex.Message}");
                throw; // APIShared rolls back this registration's changes: the request goes on as it came
            }
        }

        /// <summary>Pure apart from the args object: refuse -> veto, trim -> StructureId, then remember the approval.</summary>
        internal static void ApplyDecision(PreAccess access, object args, int structureId, MakeTroopRecruitHook.RecruitDecision decision)
        {
            MakeTroopRecruitHook.LogDecision(decision);
            if (decision.Allowed <= 0)
            {
                access.SetSkip(args, true);
                return;
            }
            int forwarded = structureId;
            if (decision.Allowed != decision.Requested)
            {
                access.SetStructureId(args, decision.Allowed);
                forwarded = decision.Allowed;
            }
            access.SetState(args, new Approval { Decision = decision, ForwardedStructureId = forwarded });
        }

        /// <summary>Reserve what an accepted request will recruit (internal for the tests).</summary>
        internal static void Accepted(AcceptedAccess access, object args)
        {
            if (!(access.GetState(args) is Approval approval)) return;
            if (access.GetActionState(args) != (int)approval.Decision.Unit) return;
            MakeTroopRecruitHook.ReserveAccepted(approval.Decision, ReserveAmount(approval, access.GetStructureId(args)));
        }

        /// <summary>
        /// Pure: the approved amount when the request reached the game as the gate left it (the own hook reserves the
        /// same), else what another participant changed it to (0 or less counts as 1, like the gate's reading).
        /// </summary>
        internal static int ReserveAmount(Approval approval, int finalStructureId)
        {
            if (finalStructureId == approval.ForwardedStructureId) return approval.Decision.Allowed;
            return finalStructureId > 0 ? finalStructureId : 1;
        }

        // ------------------------------------------------------------------
        // Reflection helpers (one delegate per member, built once)
        // ------------------------------------------------------------------

        private static Func<object, TValue> Getter<TValue>(Type owner, string name, List<string> absent)
        {
            PropertyInfo p = owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            MethodInfo get = p?.GetGetMethod();
            if (p == null || p.PropertyType != typeof(TValue) || get == null)
            {
                absent.Add($"{owner.Name}.{name} get ({typeof(TValue).Name})");
                return null;
            }
            return (Func<object, TValue>)typeof(APISharedGameActionAdapter)
                .GetMethod(nameof(MakeGetter), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(owner, typeof(TValue)).Invoke(null, new object[] { get });
        }

        private static Action<object, TValue> Setter<TValue>(Type owner, string name, List<string> absent)
        {
            PropertyInfo p = owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            MethodInfo set = p?.GetSetMethod();
            if (p == null || p.PropertyType != typeof(TValue) || set == null)
            {
                absent.Add($"{owner.Name}.{name} set ({typeof(TValue).Name})");
                return null;
            }
            return (Action<object, TValue>)typeof(APISharedGameActionAdapter)
                .GetMethod(nameof(MakeSetter), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(owner, typeof(TValue)).Invoke(null, new object[] { set });
        }

        private static Func<object, TValue> MakeGetter<TOwner, TValue>(MethodInfo get)
        {
            var typed = (Func<TOwner, TValue>)Delegate.CreateDelegate(typeof(Func<TOwner, TValue>), get);
            return o => typed((TOwner)o);
        }

        private static Action<object, TValue> MakeSetter<TOwner, TValue>(MethodInfo set)
        {
            var typed = (Action<TOwner, TValue>)Delegate.CreateDelegate(typeof(Action<TOwner, TValue>), set);
            return (o, v) => typed((TOwner)o, v);
        }

        /// <summary>An Action&lt;T&gt; for the APIShared argument type T that forwards to <paramref name="target"/>.</summary>
        private static object MakeAction(Type argType, Action<object> target) =>
            typeof(APISharedGameActionAdapter).GetMethod(nameof(MakeTypedAction), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(argType).Invoke(null, new object[] { target });

        private static Action<T> MakeTypedAction<T>(Action<object> target) => x => target(x);
    }
}

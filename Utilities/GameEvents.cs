using Sandbox.Engine.Multiplayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Network;
using VRageMath;

namespace SeamlessClient.Utilities
{
    public class GameEvents
    {
        private static Dictionary<MethodInfo, Delegate> _delegateCache = new Dictionary<MethodInfo, Delegate>();

        private static Func<T, TA> GetDelegate<T, TA>(MethodInfo method) where TA : class
        {
            if (!_delegateCache.TryGetValue(method, out var del))
            {
                del = (Func<T, TA>)(x => Delegate.CreateDelegate(typeof(TA), x, method) as TA);
                _delegateCache[method] = del;
            }

            return (Func<T, TA>)del;
        }

        public static void RaiseStaticEvent<T1>(MethodInfo method, T1 arg1, EndpointId target = default, Vector3D? position = null)
        {
            var del = GetDelegate<IMyEventOwner, Action<T1>>(method);
            MyMultiplayer.RaiseStaticEvent(del, arg1, target, position);
        }
    }
}

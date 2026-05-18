using System;
using System.Reflection;
using UnityEngine;

namespace ValheimLegends
{
    // Lee stats de EpicMMO por reflexión, sin dependencia dura.
    // Si EpicMMO no está, devuelve 0 (escalado por stat = 0).
    internal static class VL_EpicMMOBridge
    {
        private static bool _resolved;
        private static Type _levelSystemType;
        private static Type _parameterEnum;
        private static PropertyInfo _instanceProp;
        private static MethodInfo _getParameter;
        private static object _strengthValue;
        private static object _intellectValue;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("EpicMMOSystem.LevelSystem", false);
                    if (t == null) continue;
                    _levelSystemType = t;
                    _parameterEnum = asm.GetType("EpicMMOSystem.Parameter", false);
                    _instanceProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    _getParameter = t.GetMethod("getParameter", BindingFlags.Public | BindingFlags.Instance);
                    if (_parameterEnum != null)
                    {
                        _strengthValue = Enum.Parse(_parameterEnum, "Strength");
                        _intellectValue = Enum.Parse(_parameterEnum, "Intellect");
                    }
                    Debug.Log("[VL_Pantheon] EpicMMO detectado y bindeado.");
                    return;
                }
                Debug.Log("[VL_Pantheon] EpicMMO no encontrado, Strength = 0.");
            }
            catch (Exception ex) { Debug.LogWarning($"[VL_Pantheon] Bridge error: {ex.Message}"); }
        }

        public static int GetStrength()
        {
            Resolve();
            if (_levelSystemType == null || _getParameter == null || _strengthValue == null) return 0;
            try
            {
                var inst = _instanceProp.GetValue(null, null);
                if (inst == null) return 0;
                return (int)_getParameter.Invoke(inst, new[] { _strengthValue });
            }
            catch { return 0; }
        }

        public static int GetIntellect()
        {
            Resolve();
            if (_levelSystemType == null || _getParameter == null || _intellectValue == null) return 0;
            try
            {
                var inst = _instanceProp.GetValue(null, null);
                if (inst == null) return 0;
                return (int)_getParameter.Invoke(inst, new[] { _intellectValue });
            }
            catch { return 0; }
        }
    }
}
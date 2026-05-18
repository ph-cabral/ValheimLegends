using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimLegends
{
    /// <summary>
    /// Taunt nativo del fork para la Valkyrie. Fuerza a los MonsterAI cercanos
    /// a fijar al jugador como objetivo durante 'duration' segundos.
    /// Radio y duración salen de VL_TweakConfig (editable desde el .cfg).
    ///
    /// Usa Traverse (HarmonyX) porque SetTarget/SetAlerted/GetTargetCreature
    /// son privados en esta versión de Valheim.
    /// </summary>
    public static class VL_TauntHelper
    {
        public static void ApplyTaunt(Player player)
        {
            if (player == null) return;
            if (VL_TweakConfig.Valk_TauntEnabled == null
                || !VL_TweakConfig.Valk_TauntEnabled.Value) return;

            float radius   = VL_TweakConfig.Valk_TauntRadius.Value;
            float duration = VL_TweakConfig.Valk_TauntDuration.Value;
            if (radius <= 0f || duration <= 0f) return;

            List<Character> chars = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, radius, chars);

            int count = 0;
            foreach (Character ch in chars)
            {
                if (ch == null || ch == player) continue;
                if (!BaseAI.IsEnemy(player, ch)) continue;

                MonsterAI ai = ch.GetComponent<MonsterAI>();
                if (ai == null) continue;

                Traverse.Create(ai).Method("SetTarget", new object[] { player }).GetValue();
                Traverse.Create(ai).Method("SetAlerted", new object[] { true }).GetValue();

                VL_TauntTicker ticker = ch.gameObject.GetComponent<VL_TauntTicker>();
                if (ticker == null)
                    ticker = ch.gameObject.AddComponent<VL_TauntTicker>();
                ticker.Begin(player, duration);
                count++;
            }

            if (count > 0 && ZNetScene.instance != null)
            {
                GameObject fx = ZNetScene.instance.GetPrefab("fx_guardstone_activate");
                if (fx != null)
                    Object.Instantiate(fx, player.transform.position, Quaternion.identity);
            }
        }
    }

    /// <summary>
    /// Mantiene el target forzado hasta que expira. Si el AI cambia de objetivo
    /// (por daño recibido, etc.) lo vuelve a fijar al jugador.
    /// </summary>
    public class VL_TauntTicker : MonoBehaviour
    {
        private Player _target;
        private float _expiresAt;
        private MonsterAI _ai;

        public void Begin(Player target, float duration)
        {
            _target = target;
            _expiresAt = Time.time + duration;
            _ai = GetComponent<MonsterAI>();
        }

        private void Update()
        {
            if (_ai == null || _target == null) { Destroy(this); return; }
            if (Time.time >= _expiresAt) { Destroy(this); return; }

            Character current = Traverse.Create(_ai).Method("GetTargetCreature").GetValue<Character>();
            if (current != _target)
            {
                Traverse.Create(_ai).Method("SetTarget", new object[] { _target }).GetValue();
                Traverse.Create(_ai).Method("SetAlerted", new object[] { true }).GetValue();
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace ValheimLegends
{
    // SE persistente del Pantheon (igual patrón que SE_Valkyrie/SE_Rogue).
    // Lleva el contador de Mortal Will y, si Aegis está activo, dropea vida
    // propia y hace daño de fuego al frente cada segundo.
    public class SE_Pantheon : SE_Stats
    {
        public static Sprite AbilityIcon;
        public static GameObject GO_GlowFX;

        public static float m_baseTTL = 5f;

        // ---- Mortal Will (pasiva) ----
        public static int  mortalWillCount = 0;
        public static bool empowered = false;     // true cuando llega al tope
        private static bool _glowShown = false;

        // ---- Aegis Assault (E: dura un tiempo fijo) ----
        public static bool  aegisActive = false;
        public static float aegisEndTime = 0f;

        private float _tick = 0f;

        public SE_Pantheon()
        {
            base.name = "SE_VL_Pantheon";
            m_icon = AbilityIcon;
            m_tooltip = "Pantheon";
            m_name = "Pantheon";
            m_ttl = m_baseTTL;
        }

        public static int StacksNeeded =>
            VL_TweakConfig.Pan_MortalWillStacks != null
                ? VL_TweakConfig.Pan_MortalWillStacks.Value : 5;

        // Llamado al atacar / usar habilidad
        public static void AddMortalWill()
        {
            if (empowered) return;
            mortalWillCount++;
            if (mortalWillCount >= StacksNeeded)
            {
                mortalWillCount = StacksNeeded;
                empowered = true;
                ShowGlow();
            }
        }

        // Consume el estado empoderado (devuelve true si estaba activo)
        public static bool ConsumeEmpowered()
        {
            bool was = empowered;
            empowered = false;
            mortalWillCount = 0;
            _glowShown = false;
            return was;
        }

        private static void ShowGlow()
        {
            if (_glowShown || Player.m_localPlayer == null || ZNetScene.instance == null) return;
            _glowShown = true;
            // Mismo FX violeta que al activar un poder de jefe (guardian power)
            GameObject fx = ZNetScene.instance.GetPrefab("fx_guardstone_activate");
            if (fx == null) fx = ZNetScene.instance.GetPrefab("vfx_WishbonePing");
            if (fx != null)
                Object.Instantiate(fx, Player.m_localPlayer.GetCenterPoint(), Quaternion.identity);
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Mortal Will: empoderado!");
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            // Mostrar la cantidad de cargas de Mortal Will en el icono del buff
            // (mismo patrón que SE_Valkyrie: m_ttl = contador, m_time = 0).
            m_ttl = mortalWillCount;
            m_time = 0;

            if (m_character == null) return;

            if (!aegisActive) return;

            // Aegis tiene duración fija
            if (Time.time >= aegisEndTime)
            {
                aegisActive = false;
                if (m_character is Player pp)
                    pp.Message(MessageHud.MessageType.TopLeft, "Aegis terminó");
                return;
            }

            _tick += dt;
            if (_tick < 1f) return;
            _tick = 0f;

            Player p = m_character as Player;
            if (p == null) return;

            // Daño de fuego en cono frontal, escalado por Strength (EpicMMO)
            int str = VL_EpicMMOBridge.GetStrength();
            float fireDmg = VL_TweakConfig.Pan_E_FireDmgPerSec.Value
                          + str * VL_TweakConfig.Pan_E_FireDmgPerStrength.Value;

            float range = VL_TweakConfig.Pan_E_Range.Value;
            float halfAngle = VL_TweakConfig.Pan_E_Angle.Value * 0.5f;
            Vector3 fwd = p.transform.forward;

            List<Character> chars = new List<Character>();
            Character.GetCharactersInRange(p.transform.position, range, chars);
            foreach (Character ch in chars)
            {
                if (ch == null || ch == p) continue;
                if (!BaseAI.IsEnemy(p, ch)) continue;

                Vector3 dir = (ch.transform.position - p.transform.position);
                dir.y = 0f;
                if (Vector3.Angle(fwd, dir) > halfAngle) continue;

                HitData hd = new HitData();
                hd.m_damage.m_fire = fireDmg;
                hd.m_point = ch.GetCenterPoint();
                hd.m_dir = dir.normalized;
                hd.SetAttacker(p);
                hd.m_skill = ValheimLegends.DisciplineSkill;
                ch.Damage(hd);
            }
        }

        public override bool CanAdd(Character character) => true;
    }
}
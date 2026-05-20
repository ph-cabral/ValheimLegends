using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimLegends
{
    public class SE_Bulwark : StatusEffect
    {
        public static Sprite AbilityIcon;
        public static GameObject GO_SEFX;

        [Header("SE_VL_Bulwark")]
        public static float m_baseTTL = 12f;
        public float damageTakenModifier = .75f;
        private float m_timer = 0f;

        public SE_Bulwark()
        {
            base.name = "SE_VL_Bulwark";
            m_icon = AbilityIcon;
            m_tooltip = "Inmunidad total al daño mientras dure (requiere escudo equipado).";
            m_name = "Bulwark";
            m_ttl = m_baseTTL;
        }

        public override void OnDamaged(HitData hit, Character attacker)
        {
            // Fork: la reducción/inmunidad la aplica VL_Damage_Patch
            // (Valkyrie con escudo: 80%; Bulwark activo: 100%).
            base.OnDamaged(hit, attacker);
        }

        public override bool CanAdd(Character character)
        {
            return character.IsPlayer();
        }
    }
}
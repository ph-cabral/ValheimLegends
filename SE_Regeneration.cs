using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimLegends
{
    public class SE_Regeneration : StatusEffect
    {
        public static Sprite AbilityIcon;
        public static GameObject GO_SEFX;

        [Header("SE_VL_Regeneration")]
        public float m_damageInterval = 2f;
        public static float m_baseTTL = 20f;
        public float m_TTLPerDamagePlayer = 2f;
        public float m_TTLPerDamage = 2f;
        public float m_TTLPower = 0.5f;
        public float m_HealAmount = 2f;
        private float m_timer = 0f;
        public bool doOnce = true;

        // Fork: si true, NO recalcula amount/ttl/interval en el primer Update
        // (el llamador -ej. Priest- ya los seteó manualmente).
        public bool externalSetup = false;

        public SE_Regeneration()
        {
            base.name = "SE_VL_Regeneration";
            m_icon = AbilityIcon;
            m_tooltip = $"Regenerating {m_HealAmount} hp every second";
            m_name = "Regeneration";
            m_activationAnimation = "vfx_Potion_health_medium";
            m_ttl = m_baseTTL;
            doOnce = true;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if(doOnce)
            {
                doOnce = false;
                if (!externalSetup)
                {
                    // Fork: duración e intervalo configurables desde el cfg
                    m_ttl = VL_TweakConfig.RegenDuration;
                    m_damageInterval = VL_TweakConfig.RegenInterval;
                    //ZLog.Log("setting up regeneration, average skill is " +);
                    m_HealAmount = (2f + (.25f * (m_character.GetSkills().GetTotalSkill() / m_character.GetSkills().GetSkillList().Count))) * VL_GlobalConfigs.g_DamageModifer * VL_GlobalConfigs.c_druidRegen;
                }
            }
            m_timer -= dt;
            if (m_timer <= 0f)
            {
                m_timer = m_damageInterval;
                m_character.Heal(m_HealAmount, true);
                //GO_SEFX = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("vfx_Potion_health_medium"), m_character.GetCenterPoint(), Quaternion.identity);
            }            
        }

        public override bool CanAdd(Character character)
        {
            return true;
        }
    }
}
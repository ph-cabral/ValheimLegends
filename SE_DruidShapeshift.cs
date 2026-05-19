using System;
using UnityEngine;
using HarmonyLib;

namespace ValheimLegends
{
    public enum DruidForm { None, Dragon, Abomination, Serpent }

    /// <summary>
    /// Fork: transformación del Druida (Dragón / Abominación / Serpiente de océano).
    /// - Drena stamina por segundo (configurable).
    /// - Resistencia/daño/velocidad configurables por VL_TweakConfig.
    /// - Sin animación de activación.
    /// </summary>
    public class SE_DruidShapeshift : SE_Stats
    {
        public static GameObject GO_SEFX;

        public DruidForm form = DruidForm.None;

        // Parámetros (los rellena Class_Druid desde VL_TweakConfig)
        public float staminaDrainPerSec = 5f;
        public float damageModifier = 1.5f;     // multiplicador de daño infligido
        public float resistMultiplier = 0.5f;   // daño recibido (0.5 = recibe 50%)
        public float speedModifier = 1f;        // multiplicador de velocidad
        public float scale = 2f;

        private float m_drainTimer = 0f;
        private float savedStaminaRegenDelay = 1f;
        private Vector3 savedScale = Vector3.one;

        private GameObject mountedModel = null;
        private System.Collections.Generic.List<Renderer> hiddenRenderers
            = new System.Collections.Generic.List<Renderer>();

        private static string PrefabFor(DruidForm f)
        {
            switch (f)
            {
                case DruidForm.Dragon:      return "Hatchling";   // dragón volador escalable
                case DruidForm.Abomination: return "Abomination";
                case DruidForm.Serpent:    return "Serpent";
                default: return null;
            }
        }

        public SE_DruidShapeshift()
        {
            base.name = "SE_VL_DruidShapeshift";
            m_name = "Shapeshift";
            m_ttl = 0f; // dura hasta quedarse sin stamina o reactivar
        }

        public override void Setup(Character character)
        {
            savedStaminaRegenDelay = Traverse.Create(root: (Player)character).Field(name: "m_staminaRegenDelay").GetValue<float>();
            Traverse.Create(root: (Player)character).Field(name: "m_staminaRegenDelay").SetValue(0f);

            savedScale = character.transform.localScale;
            character.transform.localScale = savedScale * scale;

            switch (form)
            {
                case DruidForm.Dragon:
                    m_name = "Dragon Form";
                    m_tooltip = $"Vuela. Inmune al frío. Daño x{damageModifier}. Recibe {(int)(resistMultiplier * 100f)}% del daño. Drena {staminaDrainPerSec} stamina/s.";
                    break;
                case DruidForm.Abomination:
                    m_name = "Abomination Form";
                    m_tooltip = $"Inmune al veneno. Daño x{damageModifier}. Recibe {(int)(resistMultiplier * 100f)}% del daño. Drena {staminaDrainPerSec} stamina/s.";
                    break;
                case DruidForm.Serpent:
                    m_name = "Ocean Serpent Form";
                    m_tooltip = $"Respira bajo el agua. Velocidad x{speedModifier}. Recibe {(int)(resistMultiplier * 100f)}% del daño. Drena {staminaDrainPerSec} stamina/s.";
                    break;
            }

            base.Setup(character);

            // ===== Fork: ocultar druida y montar modelo de monstruo encima =====
            try
            {
                Transform visual = character.transform.Find("Visual");
                foreach (Renderer r in character.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled)
                    {
                        hiddenRenderers.Add(r);
                        r.enabled = false;
                    }
                }

                string prefabName = PrefabFor(form);
                if (prefabName != null && ZNetScene.instance != null)
                {
                    GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
                    if (prefab != null)
                    {
                        // tomar solo el sub-objeto visual del prefab (sin IA, sin colliders, sin ZNetView)
                        Transform pv = prefab.transform.Find("Visual") ?? prefab.transform;
                        mountedModel = UnityEngine.Object.Instantiate(pv.gameObject);
                        mountedModel.name = "VL_DruidFormModel";

                        // limpiar componentes peligrosos del clon visual
                        foreach (var c in mountedModel.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                        var znv = mountedModel.GetComponent<ZNetView>();
                        if (znv != null) UnityEngine.Object.Destroy(znv);

                        mountedModel.transform.SetParent(character.transform, false);
                        mountedModel.transform.localPosition = Vector3.zero;
                        mountedModel.transform.localRotation = Quaternion.identity;
                        mountedModel.transform.localScale = Vector3.one;
                        mountedModel.SetActive(true);
                    }
                }
            }
            catch (Exception e)
            {
                ZLog.LogWarning("VL DruidShapeshift visual error: " + e.Message);
            }
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            speed *= speedModifier;
            base.ModifySpeed(baseSpeed, ref speed, character, dir);
        }

        public override void ModifyDamageMods(ref HitData.DamageModifiers modifiers)
        {
            if (form == DruidForm.Dragon)
                modifiers.m_frost = HitData.DamageModifier.Immune;
            else if (form == DruidForm.Abomination)
                modifiers.m_poison = HitData.DamageModifier.Immune;
            base.ModifyDamageMods(ref modifiers);
        }

        public override void OnDamaged(HitData hit, Character attacker)
        {
            // Resistencia generalizada: escala todo el daño recibido.
            if (hit != null)
                hit.ApplyModifier(Mathf.Clamp01(resistMultiplier));
            base.OnDamaged(hit, attacker);
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            if (hitData != null)
                hitData.m_damage.Modify(damageModifier);
            base.ModifyAttack(skill, ref hitData);
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            m_drainTimer += dt;
            if (m_drainTimer >= 1f)
            {
                m_drainTimer -= 1f;
                if (m_character != null && m_character is Player p)
                {
                    if (p.GetStamina() <= staminaDrainPerSec)
                    {
                        p.GetSEMan().RemoveStatusEffect("SE_VL_DruidShapeshift".GetStableHashCode());
                        return;
                    }
                    p.UseStamina(staminaDrainPerSec);
                }
            }
        }

        public override void Stop()
        {
            // ===== Fork: restaurar druida =====
            try
            {
                if (mountedModel != null)
                {
                    UnityEngine.Object.Destroy(mountedModel);
                    mountedModel = null;
                }
                foreach (Renderer r in hiddenRenderers)
                    if (r != null) r.enabled = true;
                hiddenRenderers.Clear();
            }
            catch (Exception e)
            {
                ZLog.LogWarning("VL DruidShapeshift restore error: " + e.Message);
            }

            if (m_character != null)
            {
                m_character.transform.localScale = savedScale;
                if (m_character is Player p)
                    Traverse.Create(root: p).Field(name: "m_staminaRegenDelay").SetValue(savedStaminaRegenDelay);
            }
            Class_Druid.activeForm = DruidForm.None;
            base.Stop();
        }

        public override bool CanAdd(Character character)
        {
            return character.IsPlayer();
        }
    }
}
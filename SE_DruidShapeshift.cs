using System;
using System.Collections.Generic;
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
        private Animator mountedAnimator = null;
        private List<Renderer> hiddenRenderers = new List<Renderer>();

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
                        Transform pv = prefab.transform.Find("Visual");
                        if (pv == null) pv = prefab.transform;
                        GameObject visualGO = pv.gameObject;

                        // Instanciar DESACTIVADO: evita Awake() de CharacterAnimEvent/LookAt/etc.
                        // que buscan al Character padre y revientan con NRE.
                        bool prevState = visualGO.activeSelf;
                        visualGO.SetActive(false);
                        mountedModel = UnityEngine.Object.Instantiate(visualGO);
                        visualGO.SetActive(prevState);

                        mountedModel.name = "VL_DruidFormModel";

                        // Stripping de componentes que dependen del Character padre.
                        // DestroyImmediate porque el GO está inactivo y se activará en el mismo frame.
                        string[] killNames = new string[]
                        {
                            "CharacterAnimEvent", "FootStep", "LookAt",
                            "ZNetView", "ZSyncTransform", "ZSyncAnimation",
                            "Aoe", "Projectile", "EffectArea"
                        };
                        foreach (var comp in mountedModel.GetComponentsInChildren<Component>(true))
                        {
                            if (comp == null) continue;
                            if (comp is Transform) continue;
                            if (comp is Animator) continue;
                            if (comp is SkinnedMeshRenderer) continue;
                            if (comp is MeshRenderer) continue;
                            if (comp is MeshFilter) continue;
                            string n = comp.GetType().Name;
                            foreach (string kill in killNames)
                            {
                                if (n == kill)
                                {
                                    UnityEngine.Object.DestroyImmediate(comp);
                                    break;
                                }
                            }
                        }

                        // Desactivar colliders restantes
                        foreach (var c in mountedModel.GetComponentsInChildren<Collider>(true))
                            c.enabled = false;

                        // Animator (puede estar en el Visual o haberse perdido)
                        mountedAnimator = mountedModel.GetComponentInChildren<Animator>(true);
                        Animator srcAnim = prefab.GetComponentInChildren<Animator>(true);
                        if (mountedAnimator == null && srcAnim != null)
                        {
                            mountedAnimator = mountedModel.AddComponent<Animator>();
                            mountedAnimator.avatar = srcAnim.avatar;
                        }
                        if (mountedAnimator != null && srcAnim != null)
                        {
                            if (mountedAnimator.runtimeAnimatorController == null)
                                mountedAnimator.runtimeAnimatorController = srcAnim.runtimeAnimatorController;
                            mountedAnimator.enabled = true;
                            mountedAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                            mountedAnimator.applyRootMotion = false;
                        }

                        // Anclar al jugador antes de activar
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
                // Si la transformación falla, no dejar al jugador en estado roto (flotando)
                try
                {
                    if (character is Player p)
                        p.GetSEMan().RemoveStatusEffect("SE_VL_DruidShapeshift".GetStableHashCode());
                }
                catch { }
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

            // ===== Fork: puente de animación monstruo <- estado del jugador =====
            DriveMountedAnimator();

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
            // ===== Fork: restaurar druida (defensivo: cada bloque por separado) =====
            try
            {
                if (mountedModel != null)
                {
                    UnityEngine.Object.Destroy(mountedModel);
                    mountedModel = null;
                }
                mountedAnimator = null;
            }
            catch (Exception e) { ZLog.LogWarning("VL Druid Stop(model): " + e.Message); }

            try
            {
                foreach (Renderer r in hiddenRenderers)
                    if (r != null) r.enabled = true;
                hiddenRenderers.Clear();
            }
            catch (Exception e) { ZLog.LogWarning("VL Druid Stop(renderers): " + e.Message); }

            try
            {
                if (m_character != null)
                {
                    m_character.transform.localScale = savedScale;
                    if (m_character is Player p)
                        Traverse.Create(root: p).Field(name: "m_staminaRegenDelay")
                            .SetValue(savedStaminaRegenDelay);
                }
            }
            catch (Exception e) { ZLog.LogWarning("VL Druid Stop(scale): " + e.Message); }

            // CRÍTICO: limpiar activeForm ANTES del reset de vuelo,
            // porque el patch UpdateMotion fuerza m_flying=true mientras activeForm sea Dragon/Serpent
            Class_Druid.activeForm = DruidForm.None;

            // ===== Forzar fin de vuelo: resetear flying / altitud / velocidad =====
            try
            {
                if (m_character != null)
                {
                    Traverse t = Traverse.Create(m_character);
                    t.Field("m_flying").SetValue(false);
                    t.Field("m_maxAirAltitude").SetValue(m_character.transform.position.y);
                    t.Field("m_lastGroundTouch").SetValue(0f);

                    Rigidbody rb = m_character.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        // matar velocidad vertical -> gravedad toma control inmediato
                        Vector3 v = rb.velocity;
                        v.y = 0f;
                        rb.velocity = v;
                        rb.useGravity = true;
                    }
                }
            }
            catch (Exception e) { ZLog.LogWarning("VL Druid Stop(flight): " + e.Message); }

            base.Stop();
        }

        public override bool CanAdd(Character character)
        {
            return character.IsPlayer();
        }

        // ===== Fork: puente de animación =====
        private float lastAttackSeen = 0f;

        private void DriveMountedAnimator()
        {
            if (mountedAnimator == null || mountedModel == null || m_character == null) return;
            Player p = m_character as Player;
            if (p == null) return;

            Rigidbody rb = p.GetComponent<Rigidbody>();
            Vector3 v = rb != null ? rb.velocity : Vector3.zero;

            // Velocidad en plano horizontal -> forward speed normalizado (~ -1..1)
            Vector3 horiz = new Vector3(v.x, 0f, v.z);
            float speed = horiz.magnitude;
            float forwardNorm = Mathf.Clamp(speed / 5f, 0f, 1f);

            // Dirección relativa al modelo (para sidestep, si el animator lo soporta)
            Vector3 localVel = mountedModel.transform.InverseTransformDirection(horiz);
            float forwardSigned = Mathf.Clamp(localVel.z / 5f, -1f, 1f);
            float sideSigned = Mathf.Clamp(localVel.x / 5f, -1f, 1f);

            bool inAir = !p.IsOnGround();
            bool inAttack = p.InAttack();

            // Parámetros estándar de los Animator de criaturas en Valheim
            // (no todos existen en cada monstruo: TrySet ignora ausentes sin error)
            TrySetFloat("forward_speed", forwardNorm);
            TrySetFloat("ForwardSpeed", forwardNorm);
            TrySetFloat("speed", forwardNorm);
            TrySetFloat("Speed", forwardNorm);
            TrySetFloat("side_speed", sideSigned);
            TrySetFloat("turn_speed", 0f);
            TrySetFloat("MoveX", sideSigned);
            TrySetFloat("MoveZ", forwardSigned);

            TrySetBool("flying", inAir);
            TrySetBool("Flying", inAir);
            TrySetBool("inAir", inAir);
            TrySetBool("encumbered", false);
            TrySetBool("sneaking", false);
            TrySetBool("blocking", false);
            TrySetBool("equiping", false);

            // Detectar transición de no-atacar -> atacar (flanco) para disparar el trigger
            if (inAttack && lastAttackSeen < 0.5f)
            {
                TryTrigger("attack");
                TryTrigger("Attack");
                TryTrigger("swing");
                TryTrigger("bite");
            }
            lastAttackSeen = inAttack ? 1f : 0f;
        }

        private void TrySetFloat(string name, float value)
        {
            try
            {
                foreach (var p in mountedAnimator.parameters)
                {
                    if (p.name == name && p.type == AnimatorControllerParameterType.Float)
                    {
                        mountedAnimator.SetFloat(name, value);
                        return;
                    }
                }
            }
            catch { }
        }

        private void TrySetBool(string name, bool value)
        {
            try
            {
                foreach (var p in mountedAnimator.parameters)
                {
                    if (p.name == name && p.type == AnimatorControllerParameterType.Bool)
                    {
                        mountedAnimator.SetBool(name, value);
                        return;
                    }
                }
            }
            catch { }
        }

        private void TryTrigger(string name)
        {
            try
            {
                foreach (var p in mountedAnimator.parameters)
                {
                    if (p.name == name && p.type == AnimatorControllerParameterType.Trigger)
                    {
                        mountedAnimator.SetTrigger(name);
                        return;
                    }
                }
            }
            catch { }
        }
    }
}
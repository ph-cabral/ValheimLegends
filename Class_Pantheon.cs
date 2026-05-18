using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimLegends
{
    public class Class_Pantheon
    {
        private static int ScriptChar_Layermask = LayerMask.GetMask(
            "Default", "static_solid", "Default_small", "piece_nonsolid", "terrain",
            "vehicle", "piece", "viewblock", "character", "character_net", "character_ghost");

        // Estado del salto R (Aether Leap) - salto físico estilo Valkyrie
        public static bool inFlight = false;
        public static float fallImmuneUntil = 0f;

        // Detecta si el daño frontal debe anularse (Aegis activo).
        // Lo usa el patch de bloqueo. Frente = inmune total.
        public static bool ShouldNullifyFrontDamage(Player player, HitData hit)
        {
            if (!SE_Pantheon.aegisActive || player == null || hit == null) return false;
            Vector3 fromAttack = hit.m_dir;
            if (fromAttack == Vector3.zero) return false;
            // si el golpe viene de frente (mirando al jugador), el ángulo entre
            // -forward del jugador y la dirección del golpe es chico
            float ang = Vector3.Angle(player.transform.forward, -fromAttack);
            return ang <= VL_TweakConfig.Pan_E_Angle.Value * 0.5f;
        }

        public static void Process_Input(Player player)
        {
            // ----- Q : Embestida con lanza  /  Bloqueo + Q : lanzar lanza común -----
            if (VL_Utility.Ability1_Input_Down)
            {
                if (player.GetSEMan().HaveStatusEffect("SE_VL_Ability1_CD".GetStableHashCode()))
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
                // ----- Bloqueo + Q : lanza lanza común de Valheim -----
                else if (player.IsBlocking())
                {
                    if (player.GetStamina() >= VL_TweakConfig.Pan_Block_SpearStamina.Value)
                    {
                        StatusEffect se_cd = (SE_Ability1_CD)ScriptableObject.CreateInstance(typeof(SE_Ability1_CD));
                        se_cd.m_ttl = VL_TweakConfig.Pan_Q_Cooldown.Value;
                        player.GetSEMan().AddStatusEffect(se_cd);
                        player.UseStamina(VL_TweakConfig.Pan_Block_SpearStamina.Value);

                        VL_Utility.RotatePlayerToTarget(player);
                        ZSyncAnimation z = (ZSyncAnimation)typeof(Player).GetField("m_zanim",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                        z.StopAllCoroutines();
                        z.SetTrigger("spear_throw"); // animación de lanza arrojadiza común

                        Vector3 spawn = player.GetEyePoint() + player.GetLookDir() * .2f
                                      + player.transform.up * .2f;
                        GameObject prefab = ZNetScene.instance.GetPrefab("spear_bronze_projectile");
                        if (prefab == null) prefab = ZNetScene.instance.GetPrefab("spear_flint_projectile");
                        if (prefab == null) prefab = ZNetScene.instance.GetPrefab("spear_chitin_projectile");
                        if (prefab == null) prefab = ZNetScene.instance.GetPrefab("VL_ThrowingKnife");
                        if (prefab != null)
                        {
                            GameObject go = UnityEngine.Object.Instantiate(prefab, spawn, Quaternion.identity);
                            Projectile pj = go.GetComponent<Projectile>();
                            if (pj != null)
                            {
                                pj.name = "PantheonVanillaSpear";
                                pj.m_respawnItemOnHit = false;
                                pj.m_spawnOnHit = null;
                                pj.m_ttl = 10f;
                                pj.transform.localRotation = Quaternion.LookRotation(player.GetAimDir(spawn));

                                HitData hd = new HitData();
                                hd.m_damage.m_pierce = VL_TweakConfig.Pan_Block_SpearDamage.Value
                                                     * VL_GlobalConfigs.g_DamageModifer;
                                hd.m_skill = ValheimLegends.DisciplineSkill;
                                hd.SetAttacker(player);

                                Vector3 aim = player.GetAimDir(spawn);
                                pj.Setup(player, aim * VL_TweakConfig.Pan_Q_Speed.Value, -1f, hd, null, null);
                                Traverse.Create(pj).Field("m_skill").SetValue(ValheimLegends.DisciplineSkill);
                            }
                        }
                        player.RaiseSkill(ValheimLegends.DisciplineSkill, .5f);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Sin stamina para la lanza");
                    }
                }
                // ----- Q : embestida con lanza -----
                else if (player.GetStamina() >= VL_TweakConfig.Pan_Q_Cost.Value)
                {
                    StatusEffect se_cd = (SE_Ability1_CD)ScriptableObject.CreateInstance(typeof(SE_Ability1_CD));
                    se_cd.m_ttl = VL_TweakConfig.Pan_Q_Cooldown.Value;
                    player.GetSEMan().AddStatusEffect(se_cd);
                    player.UseStamina(VL_TweakConfig.Pan_Q_Cost.Value);

                    float sLevel = player.GetSkills().GetSkillList()
                        .FirstOrDefault(x => x.m_info == ValheimLegends.DisciplineSkillDef).m_level;

                    bool emp = SE_Pantheon.ConsumeEmpowered();
                    float mult = emp ? VL_TweakConfig.Pan_MortalWillMult.Value : 1f;

                    // Animación de estocada con lanza
                    ((ZSyncAnimation)typeof(Player).GetField("m_zanim",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player))
                        .SetTrigger("spear_poke");

                    // Empuje físico hacia adelante (embestida)
                    Rigidbody body = Traverse.Create(player).Field("m_body").GetValue<Rigidbody>();
                    if (body != null)
                    {
                        Vector3 look = player.GetLookDir();
                        look.y = 0f;
                        look.Normalize();
                        Vector3 v = body.velocity;
                        v.x = look.x * VL_TweakConfig.Pan_Q_ChargeForce.Value * mult;
                        v.z = look.z * VL_TweakConfig.Pan_Q_ChargeForce.Value * mult;
                        v.y = VL_TweakConfig.Pan_Q_ChargeUp.Value;
                        body.velocity = v;
                    }

                    if (ZNetScene.instance != null)
                    {
                        GameObject fx = ZNetScene.instance.GetPrefab("vfx_perfectblock");
                        if (fx != null)
                            UnityEngine.Object.Instantiate(fx, player.transform.position, Quaternion.identity);
                    }

                    // Daño en cono frontal al embestir
                    float dmg = (VL_TweakConfig.Pan_Q_Damage.Value
                               + sLevel * VL_TweakConfig.Pan_Q_DamageScale.Value)
                               * mult * VL_GlobalConfigs.g_DamageModifer;

                    List<Character> chars = new List<Character>();
                    Character.GetCharactersInRange(player.transform.position,
                        VL_TweakConfig.Pan_Q_ChargeRadius.Value, chars);
                    Vector3 fwd = player.transform.forward;
                    foreach (Character ch in chars)
                    {
                        if (ch == null || ch == player) continue;
                        if (!BaseAI.IsEnemy(player, ch)) continue;
                        Vector3 d = ch.transform.position - player.transform.position;
                        d.y = 0f;
                        if (Vector3.Angle(fwd, d) > 60f) continue;

                        HitData hd = new HitData();
                        hd.m_damage.m_pierce = dmg;
                        hd.m_pushForce = 20f;
                        hd.m_point = ch.GetCenterPoint();
                        hd.m_dir = d.normalized;
                        hd.m_skill = ValheimLegends.DisciplineSkill;
                        hd.SetAttacker(player);
                        ch.Damage(hd);
                        ch.Stagger(d.normalized);
                    }

                    if (emp)
                        player.Message(MessageHud.MessageType.TopLeft, "Embestida empoderada!");

                    player.RaiseSkill(ValheimLegends.DisciplineSkill, .5f);
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Sin stamina para la embestida");
                }
            }
            // ----- E : Aegis Assault (activar, dura un tiempo) -----
            else if (VL_Utility.Ability2_Input_Down)
            {
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability2_CD".GetStableHashCode()))
                {
                    if (player.GetStamina() >= VL_TweakConfig.Pan_E_Cost.Value)
                    {
                        StatusEffect se_cd = (SE_Ability2_CD)ScriptableObject.CreateInstance(typeof(SE_Ability2_CD));
                        se_cd.m_ttl = VL_TweakConfig.Pan_E_Cooldown.Value;
                        player.GetSEMan().AddStatusEffect(se_cd);
                        player.UseStamina(VL_TweakConfig.Pan_E_Cost.Value);

                        SE_Pantheon.aegisActive = true;
                        SE_Pantheon.aegisEndTime = Time.time + VL_TweakConfig.Pan_E_Duration.Value;

                        UnityEngine.Object.Instantiate(
                            ZNetScene.instance.GetPrefab("fx_VL_HealPulse"),
                            player.GetCenterPoint(), Quaternion.identity);
                        player.Message(MessageHud.MessageType.TopLeft,
                            "Aegis ACTIVO (frente: inmune + fuego)");
                        player.RaiseSkill(ValheimLegends.DisciplineSkill, .5f);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Sin stamina para Aegis");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
            // ----- R : Aether Leap (salto físico estilo Valkyrie + impacto AoE) -----
            else if (VL_Utility.Ability3_Input_Down)
            {
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability3_CD".GetStableHashCode()))
                {
                    if (player.GetStamina() >= VL_TweakConfig.Pan_R_Cost.Value)
                    {
                        StatusEffect se_cd = (SE_Ability3_CD)ScriptableObject.CreateInstance(typeof(SE_Ability3_CD));
                        se_cd.m_ttl = VL_TweakConfig.Pan_R_Cooldown.Value;
                        player.GetSEMan().AddStatusEffect(se_cd);
                        player.UseStamina(VL_TweakConfig.Pan_R_Cost.Value);

                        // Salto físico: mismo enfoque que Class_Valkyrie.Leap
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player))
                            .SetTrigger("knife_secondary");

                        Vector3 velVec = player.GetVelocity();
                        Rigidbody playerBody = Traverse.Create(player).Field("m_body").GetValue<Rigidbody>();
                        inFlight = true;
                        Vector3 playerHorVec = Vector3.zero;
                        playerHorVec.z = playerBody.velocity.z;
                        playerHorVec.x = playerBody.velocity.x;
                        float jump = VL_TweakConfig.Pan_R_JumpHeight.Value;
                        playerBody.velocity = (velVec * 2f) + new Vector3(0, jump, 0f) + (playerHorVec * 3f);

                        if (ZNetScene.instance != null)
                            UnityEngine.Object.Instantiate(
                                ZNetScene.instance.GetPrefab("vfx_perfectblock"),
                                player.transform.position, Quaternion.identity);

                        player.RaiseSkill(ValheimLegends.DisciplineSkill, .8f);
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Sin stamina para el salto");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
        }

        // Impacto al aterrizar: daño + stun en área. Escala con Strength (EpicMMO).
        // 'altitude' = altura caída (más alto = más daño, igual que Valkyrie).
        public static void Impact_Effect(Player player, float altitude)
        {
            inFlight = false;
            fallImmuneUntil = Time.time + 1.5f; // gracia anti fall-damage

            int str = VL_EpicMMOBridge.GetStrength();
            float radius = VL_TweakConfig.Pan_R_AoeRadius.Value;
            float stun   = VL_TweakConfig.Pan_R_StunDuration.Value
                         + str * VL_TweakConfig.Pan_R_StunPerStrength.Value;
            float dmg    = (VL_TweakConfig.Pan_R_ImpactDamage.Value
                         + str * VL_TweakConfig.Pan_R_DamagePerStrength.Value
                         + altitude * 1.5f)
                         * VL_GlobalConfigs.g_DamageModifer;

            List<Character> chars = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, radius, chars);
            foreach (Character ch in chars)
            {
                if (ch == null || ch == player) continue;
                if (!BaseAI.IsEnemy(player, ch)) continue;
                if (!VL_Utility.LOS_IsValid(ch, player.transform.position, player.GetCenterPoint())) continue;

                Vector3 dir = (ch.transform.position - player.transform.position);
                HitData hd = new HitData();
                hd.m_damage.m_pierce = dmg;        // golpe de lanza
                hd.m_pushForce = 15f;
                hd.m_point = ch.GetEyePoint();
                hd.m_dir = dir.normalized;
                hd.m_skill = ValheimLegends.DisciplineSkill;
                hd.SetAttacker(player);
                ch.Damage(hd);

                if (ch.GetSEMan() != null)
                {
                    SE_Slow slow = (SE_Slow)ScriptableObject.CreateInstance(typeof(SE_Slow));
                    slow.m_ttl = stun;
                    ch.GetSEMan().AddStatusEffect(slow, true);
                }
                ch.Stagger(dir.normalized);
            }

            ((ZSyncAnimation)typeof(Player).GetField("m_zanim",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer))
                .SetTrigger("battleaxe_attack2");
            if (ZNetScene.instance != null)
            {
                GameObject fx = ZNetScene.instance.GetPrefab("vfx_gdking_stomp");
                if (fx != null)
                    UnityEngine.Object.Instantiate(fx, player.transform.position, Quaternion.identity);
            }
        }
    }
}